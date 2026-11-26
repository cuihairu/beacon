# BEACON-FIX-1126 验收取证状态（2026-11-26）

## CI / Core（已绿）

- Core 测试：ci run 45752403468 全绿。
- nightly run 45754679256（head dfeb789）三道新门全绿：
  - AH5d-B region 直读：四角全出 region、顶缘 Top=3 贴顶；
  - SG 病灶 C：图标-文字间距 10px（≥9）；
  - SG 窄窗：600px 窗搜索/过滤/清词复原全链；
  - SH 病灶 D：刷新档下拉宽 220px（≥218）。
- 资产：beacon-nightly-windows-x86_64.zip sha256 ee4efd3b8ba4e4d89efbd0e171ca11d77d3bf6234175f851f3b1bb88b8fda7a9（70,435,439 bytes，2026-11-26T06:02:04Z 发布）。

## imac 取证通道（已打通）

imac 从 2026-11-25 起「启动即崩 0xC000027B」的真因**不是装机环境退化**，是取证通道
选错了会话：ssh 落 **session 0（services）**，而 WinUI 3 不支持在 session 0 跑——
微软官方在 microsoft-ui-xaml#10163 的结论（godlytalias，该 issue 关闭者）：
「Normally these issues happen while running WinUI apps in Session 0 which is not
supported」。

崩溃本体：WinDbg `!analyze -v`（`cdb-analyze-20261126.txt`）给出
`STOWED_EXCEPTION_80070057_...!RefreshRateInfo::RegisterForPowerNotification`
（`RefreshRateInfo.cpp:174`），崩在 `Application.Start` 原生初始化、
beacon 托管代码零执行——与本次 C# 改动无因果关系；改动前源码（d67ef48）同崩可证。

修正做法：用 scheduled task（`New-ScheduledTaskPrincipal -UserId 'IMAC\cui' -LogonType Interactive`）
落 **console session 1** 启动，稳定存活。取证产物见 `imac-realmachine/`。

### 真机已核（设置页四病灶）

`imac-realmachine/settings-scr-1.png`（常规页，1786x1351 客户端区）：
- 病灶 C 图标-文字：模块行图标框与标题/副标题之间有清晰间隙，未贴死；
- 侧栏「配置中心 · 23 个模块」+ 四入口（常规/桌面显示/外观/组件中心）+
  数据来源分组（GitHub/Jenkins/本机/Herald/智谱 GLM/火山方舟），搜索框在顶。

`imac-realmachine/source-page.png`（数据来源页）：
- 连接行右对齐「测试连接」+ 状态 chip「✓ 测试通过」+ 开关列在窗右缘，文字列未被挤压；
- **病灶 D 布局**：表单底部右下「保存连接 / 取消编辑」两按钮同排、位于表单下方
  而非左上角；
- 「刷新档」列头在组件向导表头完整显示（无截断）。

`imac-realmachine/fullscreen.png`：主屏 2560x1440 桌面全形（200% 缩放）。

### 待补

- 岛态窗（`Beacon Status`）视觉截图：区域在副屏（虚拟桌面 x=8024），
  岛窗为分层/region 窗，`CopyFromScreen` 抓得全白（虚/实桌面截图均复现）；
  顶角圆弧 + 底角无白外围需改用 PrintWindow 分层合成或在用户屏幕内验。
- 岛态需先设 `showCapsule=true`+`capsuleIsland=true`（本次取证已恢复原配置
  `showCapsule=false`，不影响用户日常使用）。
- **用户装机亲验仍为硬门**，BEACON-FIX-1126 保持未勾。
