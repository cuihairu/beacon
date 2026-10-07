# B-705 L0 行为铁律检查（只看不弹，RFC §6.2.7）

> 状态：代码侧静态审计完成（本文档记录）；逐条真机手动验证归入 B-803 全流程走查（首台 Windows 环境执行）。

## 铁律对照

| # | 铁律 | 代码保证 | 结果 |
|---|---|---|---|
| 1 | L0 只展示状态、点击下钻（L2 或 L3） | `PinnedHostWindow.TileActivated` 唯一点击出口 → `QuickPanelWindow.Toggle()`（L2）；无其他行为入口 | ✅ 审计通过 |
| 2 | 不产生任何弹窗/Toast/声音 | `PinnedHostWindow.cs` 全文无 Toast/ContentDialog/MessageBox/Sound/Play 调用；L0 对 Alert 的唯一表达是状态灯变色（`PinTile.Update`） | ✅ 审计通过 |
| 3 | 不承载需要确认的 Action | L0 无 Action 入口（无 ActionRunner 引用）；仅可下钻 | ✅ 审计通过 |
| 4 | 永不抢焦点、不进 Alt-Tab/任务栏 | `WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW`、`IsShownInSwitchers=false`、Topmost、拖动/悬停全用指针事件（不 Activate） | ✅ 审计通过（真机复核归 B-803） |

## Critical 表达（铁律 2 补充）

- 现状：Critical 仅状态灯变红（`SeverityPalette.Color(Critical)`），无任何打断。
- 脉冲动画（≤1Hz×intensity）属 **B-707 动效系统**范围（RFC §6.2.8：reduced 档默认脉冲）；B-707 落地后本表复核「灯脉冲仍不发声不弹窗」。

## Alert 通道分离

- Alert 一律走 NotificationEngine（B-502）→ Toast/声音由规则表驱动；L0 不订阅 NotificationRaised、不接触 INotificationSink（grep 证据见上）。

## 真机手动清单（归 B-803 执行时勾选）

- [ ] 注入 Critical 状态：L0 仅变色（B-707 后为脉冲），无弹窗/声音打断；Toast 照常由通知引擎发出
- [ ] 断网：tile 变灰空心灯 + Last update（B-704），无任何异常弹窗；恢复后自动复位
- [ ] 点击 tile → L2 打开并聚焦；失焦自动关闭；ESC 关闭
- [ ] Alt-Tab 列表与任务栏均不见 L0 宿主；点击 tile 不打断当前输入焦点
- [ ] 拖动 tile 至屏边 → 收起细条；悬停展开、点击进 L2（B-703）
