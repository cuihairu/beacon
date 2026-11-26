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

### 真机已核（岛面顶角/底角）

副屏岛窗（saved `islandLayout.monitor=\\\.\DISPLAY2`，虚桌面 x=8024）分层合成，`CopyFromScreen`
抓得纯白 377B（PrintWindow 亦白）——换主屏复现：
临时把 `islandLayout.monitor` 改 `\\.\DISPLAY1` + `showCapsule=true/capsuleIsland=true`，
interactive task 启 app（session 1），`CopyFromScreen` 拿到岛窗 98KB 有效 PNG：
`imac-realmachine/island-primary.png` + 3 倍放大 `island-primary-zoom.png`。

逐像素判定（按截屏坐标，岛 rect 2319,-3–2801,125，截图起点 2289,-33）：
- 顶两角：从上缘起左右各约 12px 逐行切出斜边（黑岛边 x 起点 32→38→44、右端 511→505→499），
  **非直角硬切**；
- 底两角：底部两角平滑圆弧收口（左下黑边 x=34→64，右下黑边 x=507→477），
  弧外直接是壁纸色；
- 岛矩形范围内近白像素 0（白字在 y=25–41，非外围），**底角白外围已消除**；
- 取证结束已恢复用户原 `config.json` / `shell-state.json`（DISPLAY2 岛布局 + 关闭岛态）。

### 设置页与组件中心真机复核（2026-11-26，session 1 UIA）

沿用 `IMAC\cui` interactive scheduled task 通道，在 imac 实机跑 UIA 断言（不是 CI 模拟）：

- **搜索跳转（SET-UX-08）**：搜「检查频率」回车后 `FocusedElement.AutomationId=poll-interval-box`，
  头行「0/23 个模块匹配 · 1 设置项」；`SG-jump-poll.png` 可见焦点框与过滤计数。
- **来源状态三态（SET-UX-09）**：连接 chip 初始「已保存 · 待验证」→ 点「测试连接」（`127.0.0.1:18099`
  无监听）→ chip「✗ 测试失败：HTTP 接口不可达…积极拒绝」+ 反馈行同因可见，真实原因未被通用文案吞掉。
- **刷新档词汇（SET-UX-10）**：`poll-interval-box` 读名「默认刷新间隔（未单独设置的组件）」。
- **主题生效标注（SET-UX-11）**：`theme-box` 读名「主题（重启生效）」。
- **开关列对齐（SET-UX-ROW-1）**：`module-toggle-github/jenkins/herald` 右缘全 733，极差 0px。
- **画廊按面过滤（SET-UX-ROW-2）**：http-a.status 组件，应用文案「应用到桌面」↔「应用到看板状态行」随面变；
  row 面 `statusline` 可用，其余 6 项置灰带原因（`ROW2-gallery-row.png`）。
- **桌面页三档显隐（SET-UX-ROW-3）**：关闭档仅 `board-toggle`；胶囊档加 `desktop-opacity-slider`；
  岛档 `desktop-layout-box/numeric-floating-toggle/edge-collapse-toggle/desktop-opacity-slider/board-toggle`
  五项全显（`ROW3-off.png`、`ROW3-capsule.png`、`ROW3-island.png`）。
- 取证后已把 config 恢复 `showCapsule=false/capsuleIsland=false`（用户原态），并停止取证实例。

### 键盘走查真机取证（2026-11-26，session 1 合成键）

SET-UX-11 键盘面：四路径全部真机键盘闭环（`imac-realmachine/kbdwalk.txt` 原始日志；
焦点可达性另见 `imac-realmachine/tabwalk.txt`，51a9c3f）：

- **搜索**：`settings-search` SetValue「检查频率」→ Enter → `FocusedElement=poll-interval-box`；
- **切换显示**：`status-bar-island/capsule/off` 逐个 SetFocus + Space，config 三档逐拍落盘
  （岛 `showCapsule=True/capsuleIsland=True` → 胶囊 `True/False` → 关 `False/False`）；
- **选样式**：`style-button-http-a.status` SetFocus + Enter → 「Beacon · 组件中心」窗出现；
- **接入**：`test-http-a` SetFocus + Space → chip 翻「✗ 测试失败：HTTP 接口不可达：由于目标
  计算机积极拒绝，无法连接。(127.0.0.1:18099)」（端点无监听，真实原因可见）。

工具学注记（取证通道，非产品缺陷）：SendInput 合成键在 WinUI 控件激活路径不生效
（Tab 导航与 TextBox KeyDown 通，Button/RadioButton Space/Enter 不响应），改用
`keybd_event` 注入全通——三次对照实验（v5 SendInput 全败 / v6 keybd_event 首拍即翻 /
v7 全链 KeybdPair 全绿）定谳。另发现：组件中心画廊窗（SampleGalleryWindow，997 行）
无 Esc/KeyDown 处理，键盘 Esc 关不掉画廊（QuickPanelWindow 有 B-601 Esc accelerator，
画廊窗没有）——与走查四路径结论无关，记录备查。

取证后 config 已恢复用户原态（showCapsule=false/capsuleIsland=false）。

### 待补

- **用户装机亲验仍为硬门**，BEACON-FIX-1126 保持未勾。
