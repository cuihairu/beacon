# RFC §6.2.7 ③ 动作面清单（2026-11-08 补，todo :60 第三项收口）

> §6.2.7 四铁律：①L0 只显状态+点击下钻 ②L0 不自发弹窗/Toast/声音 ③L0 不带需确认动作（至多零风险
> open-link）④不抢焦点/不进 Alt-Tab/不进任务栏。四真人操作项中 ①②④ 已有 PASS 证据，③动作面清单
> 本文档补齐：逐面给动作定义（代码接线行号）、铁律条款对照、当前证据与缺口。

## 动作面总表（代码实枚举，接线处 App.xaml.cs）

| # | 面 | 真人操作 | 接线动作 | 铁律对照 | 证据现状 | 缺口 |
|---|---|---|---|---|---|---|
| 1 | L0 悬浮数字 tile（FloatingTileWindow） | 左键点击（无位移） | `TileActivated`→`ToggleBoard` 下钻 L2（:272，经看板载体开关） | rule 1 ✓（下钻）；无右键菜单无确认动作（rule 3 ✓） | 代码链 PASS；本机批 BM2 帧悬浮 tile 在场渲染 | 真人点击 tile→看板弹出截图（待装机） |
| 2 | L0 悬浮数字 tile | 左键拖动 | `DragSession` 移动+落点持久化（非动作，位置自由） | rule 1（形态操作非业务动作） | 拖动面 imac/.5 既有取证 | — |
| 3 | L0 钉选 tile 行（PinnedHostWindow/PinTile） | 左键点击行 | `TileActivated`→`ToggleBoard` 下钻 L2（PinnedHostWindow.cs:547、App.xaml.cs:291） | rule 1 ✓ | 代码链 PASS；AF 场景 UIA Invoke 行锚定自动化可点（run 40903469089 首绿） | 真人点击行→看板弹出截图（待装机） |
| 4 | L0 钉选 tile 行 | 拖动 / 悬停收起态 | 换位+四边吸附+贴边收起；悬停临时展开（edgeCollapse） | rule 1（形态操作） | 拖动吸附/收起展开 imac 既有取证（08/06 系列） | — |
| 5 | L1 胶囊（CapsuleWindow 经典态） | 左键点击 | `OpenPanelRequested`→`ToggleBoard` 下钻 L2（App.xaml.cs:449 同款开关） | rule 1 ✓ | **②不抢焦点 openclaw 真机 PASS**（点胶囊唤起看板前台不变） | — |
| 6 | L1 胶囊 | 右键 | `SettingsRequested`→设置窗（CapsuleWindow.Drag.cs:175、App.xaml.cs:450） | 设置窗是显式唤起非 popup（rule 2 ✓）；无确认动作（rule 3 ✓） | 代码链 PASS | 真人右键→设置窗截图（待装机） |
| 7 | L1 胶囊 | 悬停 | 来源预览展开（ShowPreview，收起还原） | 形态操作；无自发弹窗（rule 2 ✓） | 展开态有取证（取证钩子程序触发——锁屏会话注入不了指针） | 真人 hover 展开（待装机） |
| 8 | 悬浮岛（CapsuleWindow 岛形态） | 点击 | `OpenPanelRequested`→`ToggleBoard`（App.xaml.cs:449） | rule 1 ✓ | imac 04-board-window.png 看板完整打开（唤起路径 UIA/热键）；岛点击本体链同胶囊 | 岛本体真人点击→看板（待装机） |
| 9 | 悬浮岛 | 徽标显示 | 未读/异常计数被动显示（UpdateBadge） | rule 2 ✓（只变色不弹窗不发声） | imac 08-island-critical.png 红态徽标实拍 | — |
| 10 | 托盘（TrayIconService，shell 相邻面） | 左键 | `OpenPanelRequested`→`ToggleBoard`（TrayIconService.cs:240、App.xaml.cs:167） | 托盘非 L0 窗体（rule 4 不适用）；动作=下钻（rule 1 ✓） | openclaw 托盘批（两态像素差分/30s 刷新/不进溢出） | 菜单逐项真人点击截图（部分待装机） |
| 11 | 托盘 | 右键菜单 | 看板/设置/通知中心/全部刷新/组件中心/Keep panel open/退出七项（Menu ID :17-:99） | 全部直接执行无确认弹窗（rule 3 ✓；退出亦无二次确认=shell 惯例） | tray-e2.png / t78.png 菜单在场帧 | 逐项真人点击（待装机） |
| 12 | OS toast 横幅（NotificationEngine） | 点击横幅 | detailUrl 激活路由（浏览器 open-link） | rule 3 明文允许的零风险 open-link | 呈现面 PASS（本机横幅实拍 bm2-toast-tray/ + openclaw banner-t2.png）；**点击→激活 BLOCKED**：Failed+无效 detailUrl 已本地 mock 稳定触发+投递 OS（无 403 依赖），imac 系统侧压制横幅显示（直发注册 AUMID toast 亦无横幅）→点击步无横幅可点（2026-11-08-toast-activation.md） | 真人点击横幅→浏览器落点（待装机/复现） |
| 13 | L2 看板行（对照面，非 L0） | 单击/Invoke | `DetailRequested`→L3（App.xaml.cs:180） | L2/L3 允许业务动作 | **①下钻 PASS**（UIA Invoke 时间线行→L3） | — |
| 14 | L2 看板行（对照面） | 右键 | Pin to desktop→widgets.json（PinWidget） | L2 允许 | 右键钉选链 CI 场景有覆盖 | — |

## 铁律符合性小结

- 动作集合封闭性：全部 L0 面的动作集 = {下钻 L2/L3、拖动/悬停/收起形态操作、零风险 open-link}——
  **无 Deploy/Restart 类确认动作、无自发 Toast/声音**（rule 1/2/3 代码面静态符合；rule 4 由
  WS_EX_NOACTIVATE/WS_EX_TOOLWINDOW + ④ PASS 证据承担）。
- L0 唯一对外唤起链 = 显式用户点击 → `ToggleBoard`（经看板载体开关，关=不可唤起），符合「只显状态
  支持下钻」。

## 证据缺口汇总（装机清单，全为「真人操作面」）

1. 悬浮数字 tile 真人点击 → 看板弹出（面 1）
2. 钉选 tile 行真人点击 → 看板弹出（面 3）
3. 胶囊真人右键 → 设置窗（面 6）
4. 胶囊真人 hover → 来源预览展开（面 7）
5. 岛本体真人点击 → 看板弹出（面 8）
6. OS toast 横幅真人点击 → detailUrl 激活路由（面 12；复现台账见 2026-11-08-toast-activation.md）

装机后按上表逐项截图即全清；①②④ 既有 PASS 不重验。
