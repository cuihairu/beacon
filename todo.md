# TODO

> 生成于 2026-10-11（无 todo 文件；来源：docs/mvp-issues.md 未完成 42 项全量搬入 + docs/master-plan.md 后续可选）。
> 2026-10-11 巡检复点：六条「夹具/表驱动单测」单测半全部核对（模板渲染补 7 例，批3-补39），手动半统一标注【需装机】留待用户；后续可选两条为范围决策不标注。
> 2026-10-12 装机清单 CI 代验转化（批3-补50）：daily-build 视觉取证新增场景 E（单实例+托盘驻留+Exit）、F（无网启动+Last update+自愈+零弹窗+单条通知）、G（调色即时生效/非法回退/重置/动效持久化/帧差分静止呼吸/两 tile 独立）、H（悬浮位置两轮还原+TOOLWINDOW+刷新不抢焦点+panel/floating 二选一）、J（静默安装/卸载）；安装包步骤前置到取证之前。下表「已由 CI 代验」均指此批场景，证据在 Actions `visual-evidence` 工件；断言失败=红构建。
> 2026-10-12 批3-补51：G 系色彩断言阈值按 38261911354 工件逐像素实测重标定（状态灯亚像素渲染漂移 10~44px，详见决策记录）；托盘菜单驱动改 AttachThreadInput 取前台（ForceForeground）。
> 2026-10-13 patrol-20261013-A1：场景 L（行54 reduced 档变色过渡：L1 同色 tick 不闪灯 + L2 三轮翻档渐入中间态帧，38284623093）+ 修复 Update 每 tick 重放过渡导致的同色闪灯缺陷；场景 M（行6 开机自启注册表 HKCU Run\Beacon 双向断言，38287590006）；行70 热键条注记修正（K1 已代验应用侧链）；行40 补托盘图标像素不可取证的实际原因（溢出折叠区）。
> 2026-10-13 patrol-20261013-A2：场景 N（行18 设置改热键持久化，38297713667 实证：改键落盘 + 重启注册 + 触发链全绿）；场景 O（行41 面板计数一致性，38299509997 实证：三态往返全绿）；场景 P（行52 收起态还原：panel 模式 collapsed=true 细条宽启动 + 重启还原 + 展开对照，38303355372 实证全绿 P1/P2；首跑 38302150211 红）；场景 Q（行42 胶囊计数/离线横幅：2 ok ↔ 1 ok · 1 err ↔ Last update + stale 2 warn，38303355372 实证全绿）；场景 R（行20 胶囊位置还原：shell-state.json 预设 TopLeft 40,40 落位 ±4 + 重启一致，38303355372 实证全绿）。另记：38297713667/38299509997/38302150211 场景全绿但发布步 403——仓库 default workflow permissions 被置 read、workflow 级 contents:write 抬升对 release 端点不生效（nightly release/tag 被删悬空）；已改回 write（生效有传播延迟），38303355372 发布断言通过（nightly 重建、SHA265SUMS 一致），诊断用临时 probe 工作流已删（cfcd97c），详见卡点回报。
> 2026-10-13 patrol-20261013-A3：场景 S（行43 Recent 事件流一致，38315240633 实证全绿）：结构化 UIA 树遍历（x:Name→AutomationId 锚定 RecentList/AttentionList 各自子树取行文本，滤 HH:mm:ss 时间串——事件行与状态行同出 MakeStateRow，平铺 Name 列表分不开两区）；S1 基线「已用 38%」事件行在列 → S2 注入 Error 后 RecentList 现事件行（95% 行位次新于 38% 行=新→旧）且 AttentionList 同现 95%（两区同源）→ S3 恢复后状态区摘掉 95% 而 RecentList 保留 95% 并补回 38%（状态区=当前、事件流=历史）。**随行读码发现并修复缺陷**：QuickPanelWindow.OnStateChanged 原只更新摘要/chip/RECENT、RebuildStatus（需要关注/正在运行区）唯一触发点在 OnWidgetsInvalidated——面板常驻期间状态区永不随状态刷新（开面板前旧影）；修复=OnStateChanged 现场重建状态区（S2 断言在旧代码上必红，即回归门）。行45 复核：L3 开启唯一入口=ItemClick（DetailRequested 全仓唯一订阅在 App.xaml.cs，两个 handler 均 ItemClick），UIA 无法可靠触发 ItemClick，仍点击门；L3 内部 Retry/Cancel/Open 均 Button（Name 可 UIA Invoke），开启面一通即可整链转化。
> 2026-10-14 patrol-20261014-A1：场景 U 已建（行 70 L3 内动作链 [Retry] 全链路）：mock 增配 GitHub failed run（id 12345 固定，GET runs + rerun-failed-jobs POST 两路径，POST 落 marker 文件=服务端回执、档位文件可翻 500）+ Find-UiaByName 助手（ContentDialog 按钮无 AutomationId）；U1 失败 run 进需要关注 → U2 AttentionList 行 Invoke 开 L3（run_id 12345 数据行 + Retry 按钮在窗）→ U3 确认弹窗可关（取消 → mock 零 POST 负证）→ U4 确认全链（marker 在 + L3 结果「✓ 已重跑 run 12345」）→ U5 失败反馈（mock 500 → 「✗ GitHub 服务端错误」）。**38434164526 实证全绿（U1-U5 全过），行 32/38/46 半/70 已勾。**与行 70 原设想的差异：「RecentList 现动作事件行」改为 L3 结果文本 + 服务端 marker 双证据——面板 RECENT 语义=WidgetStateChanged（QuickPanelWindow 不订 ActionExecuted），不为取证加产品特性。
> 2026-10-14 patrol-20261014-A2：行 71 注入面已建（Core DebugInjection：BEACON_DEBUG_SEVERITY=critical → WidgetHost 刷新成功路径改判 Critical/Failed（摘要/负载不动），DebugInjectionTests 8 例；App ScheduleDebugCrashInjection：BEACON_DEBUG_CRASH=<秒> → UI 线程延迟抛未处理异常，预期走 XAML UnhandledException Handled=true 三兜底）+ 场景 V（reduced 档 + 38% 健康数据注入 Critical）：V1 Critical 默认色 #ff3b30 像素硬断言 ≥8px + Error #f85149 负控 ≤4px（tol12 分离：G/B 通道差 22/25）/ V2 Critical 脉冲 Scale 1.0→1.35 帧差分尽力取证（不成 ::warning 转装机，G7c 同口径）/ V3 崩溃兜底全链硬断言：日志「XAML unhandled exception」+ 堆栈「   at 」+ 进程数 1 + 零 #32770 + tile 窗仍在 + 崩溃行后仍有 refreshed ok（刷新循环不中断）。已知风险留档：DispatcherQueue.TryEnqueue 回调异常在 WinUI 3 是否路由至 Application.UnhandledException 未经装机实证，V3 红即行 24 真实发现。待 nightly 实证后勾行 24 注入半/55/61 Critical 半与行 71。
> 2026-10-14 patrol-20261014-A3：场景 V 首跑（38459944253）：V1 绿（Critical 色 #ff3b30 52px 硬断言、Error 负控 0px 无渗漏）+ V2 绿（Critical 脉冲帧差分 85px）= 行 55/61 Critical 半实证；**V3 红出真实发现**——BEACON_DEBUG_CRASH 裸 TryEnqueue 派发回调异常后进程退出：WinUI 3 无 DispatcherQueue.HandlerException 事件（Windows App SDK 1.6 参考程序集 API 面实证，事件仅 ShutdownStarting/Completed 与 FrameworkShutdownStarting/Completed 四个），回调异常不经 Application.UnhandledException 路由，RFC §10「不闪退」在派发面缺口。修复：Core `DispatchGuard.Wrap`（纯包装：异常转交回调、兜底自身异常亦吞，4 例单测）+ App `DispatcherQueueGuard.EnqueueGuarded`（TryEnqueue 兜底版：异常落应用日志「DispatcherQueue unhandled exception」+ CrashLog，进程存活），全仓 23 处 UI 派发改走 guard（App 19 + UiDispatcher/QuickPanel/FloatingTile/PinnedHost 各 1）+ 唯一 DispatcherTimer.Tick（SettingsWindow 窗口尺寸落盘，同骑派发循环同缺口）；V3 断言改锚 guard 日志串。随批：mock 端口冲突连红两晚根因修复（Ensure-MockServer，4f21ab7）后 U1-U5 全绿（行 70 链回归通过）；Toast 快捷方式缺失检测 Debug.WriteLine（release 无调试器=空操作）改 _logger.LogWarning（490df54）。待 nightly 复证 V3 绿后勾行 24/71。
> 2026-10-14 patrol-20261014-A4：行 41 自愈半场景化 F5t——F 断网窗口预置删除 Beacon.App.lnk（Stop 后删，无竞态）→ 首发送由 toolkit 重建（AUMID 自愈）→ 30s 轮询 .lnk 硬断言；无头会话 toast 基础设施不可用时按「Toast 发送失败」日志降级 ::warning 转装机（G7c 同口径），零失败日志且 .lnk 不在 = 首发送链真回归红。顺修行 41 注记笔误（「SendInput」→ 实际四执行器 Http/LocalCommand/OpenUrl/Webhook，grep 实证无 SendInput）。待 nightly 实证后更新行 41。
> 2026-10-14 patrol-20261014-A5：F5t 首跑（38470370548）红出两因——①快捷方式名不匹配：app 侧 `Path.ChangeExtension(AppDomain.FriendlyName="Beacon.App", ".lnk")` 把 ".App" 当扩展名替换 → 实际文件名 "Beacon.lnk"（run 1/2 日志实证），F5t 查 "Beacon.App.lnk"（永不存在的文件名）→ 预删除空操作 + 检查必失；②级联红：F5t throw 跳过 F4 的 `Ensure-MockServer`（mock 停摆）→ G1/H1/K 全挂（三场景均不自备 mock）。修复：路径改 "Beacon.lnk"（51dd184）+ F5t 块移到 F5b 之后（F4 必先于任何 throw 执行）+ G/H/K 各加 `Ensure-MockServer` 防御。另：发布步 403 仍在（`gh release create` 403 on releases API，repo default=write + workflow contents:write 均在位但 token 无 release 端点权限——独立卡点，待查）。
> 2026-10-14 patrol-20261014-A6：F5t 三跑仍红（38488861749，仅剩 F5t 一项）挖出**前提错误**：toolkit 7.1.3 根本不建开始菜单 .lnk——Show() 源码只有 `CreateToastNotifier().Show(notif)`（ToastContentBuilder.cs:430），「AUMID 自愈」真身 = Initialize() 首次触碰 ToastNotificationManagerCompat 时写 `HKCU\Software\Classes\AppUserModelId\<aumid>` 注册键（含 CustomActivator）。此前 A5 的「文件名不匹配」修法（查 Beacon.lnk）是错上修错。机制澄清：app 侧 CheckShortcutHealth（查 .lnk、每次启动必告警假阳性）改 CheckAumidHealth（查注册表 CustomActivator 键）；F5t 断言改预删 AUMID 注册键 → 重启后 30s 内 Initialize() 重建硬断言（Test-AumidKeyPresent），「Toast 发送失败」降级分支不变；RFC-001 §技术选型与 acceptance-checklist D1 同步更正。**复证绿（38494525200）：场景 F5t 通过——预置删除注册键后 Initialize() 重建 AppUserModelId 键。**
> 2026-10-14 用户四单收口（批3-补53）：看板默认全部+chip 点击过滤（活动 chip 实心高亮+概览头带源，过滤态可见）；本仓原无 i18n（grep 零命中），建 UiStrings 唯一机制（en/zh 双字典、空串=跟随系统）覆盖看板/托盘/设置新增项；贴边收起开关默认开（保持已发布行为），收起态点击细条=粘性展开；托盘 Open Beacon/Notifications 语义分化（B-503 意图出处 mvp-issues.md:266+原 App.xaml.cs:174）。e905a48+23660f8，1078 测试绿，run 38549671960 场景 W1-W4+J 全绿；三跑红皆脚本/取证锚非产品（面板失焦即隐藏、缓存 UIA 元素失效、Border 无 UIA peer 需补 SetName），修复 35bcfe2/b2f4bdd/23660f8。
> 2026-10-15 patrol-20261015-P1014C：场景 X（行 55/60 右键 Pin 与两入口一致）——X1 面板 RecentList 事件行上 SendInput 合成右键（ContextFlyout）弹菜单，MenuFlyoutItem 按 Name「Unpin from desktop」UIA 命中后 Invoke，断言 widgets.json GLM pinned 翻转 + 悬浮 tile 移除；X2 重启 --settings 后先 Invoke module-bigmodel 行现出 widget 行，钉开关（pin-toggle-{id}）读态断言 Off 再 Toggle On，断言 widgets.json 落库 true + GLM tile 回归；X3 再重启同路读态断言 On + 行文本带「已钉」+ tile 回建——两入口同一落库链（Config.UpsertWidget + ReloadWidgets）实证，勾行 55/60。三跑红皆脚本锚非产品：InvokeableListItem 不报 ListItem ControlType（Find-PanelRow 改 TrueCondition+Name 匹配+向上限深找 ListItem 祖先）；悬浮模式 unpin 即注销 widget（面板无行可再右键，反向必须由设置入口驱动=更强的两入口一致性证据）。修复 cdd05ad/7822aae，复证 run 38590456994 全绿。
> 2026-10-15 patrol-20261015-P1014D：场景 Y（行 61 非准入类型两入口不可钉）：播种未登记类型 unknownghost.usage（pinned:true、连接指 zhipu）——Y1 主机 Resolve=null「No provider」skipped 日志 + pinned:true 零 tile、Y2 面板右键 5s 无 Pin 菜单项、Y3 设置 scope 过滤整行不产（无行无开关）、Y4 widgets.json pinned 恒 true 零改写——「无入口」证据面勾行；随批 X1 抗 flake 加固（38602176751 红因=flyout 开启动画期 InvokePattern.Invoke 被吞：菜单命中+Invoke 已调+配置不翻，失败截图实证 tile 仍在；改真实点击 ClickAt + 整链两轮重试，重试前重找行防刷新 tick 重建失效），复证 38608625892 X1-X3+Y1-Y4 全绿。803329a+67f144f。
> 2026-10-15 patrol-20261015-P1015R3（场景 Z 收口，勾行 80）：导出→导入往返 picker 双驱全绿（run 38725138151，204b884）。六轮红根因链全部落锁：①现代通用对话框「文件名」框是 ComboBoxEx32 **自绘**面，其子 Edit（同 id 1148）是**无绘制缓冲镜像**——SetWindowTextW/GetWindowTextW 只动缓冲（复核「成功」、截图框内恒空、IDOK 读空名 no-op），第 7 轮「真 Edit」与第 6 轮「ghost Edit」的矛盾由 8× 放大像素取证裁定；②CBE_SETEDITTEXT(0x1640) 亦 0 墨迹（38725138151 实证），最终生效路径=**真实键盘输入**：点框聚焦+Ctrl+A+SendInput KEYEVENTF_UNICODE 逐字键入（墨迹 376 px ≥ 阈值 60）——K1 悬案（CI 会话键盘注入可达性）就此解决，KEYEVENTF_UNICODE 可达；③提交按钮真实标题是「&Open」带 & 加速符，旧正则 ^(?:Save|Open|保存|打开) 永不匹配 → BM_CLICK 兜底从未真正触发过，修正则+DlgItemOf(dlg,1) 双保险；④WM_COMMAND IDOK 的 wParam=1 须入 LOWORD（65536 置 HIWORD 无效，ebf9f5e 已修）。断言口径：窗口文本不采信，墨迹像素（Count-BoxInk 裁框计暗像素）为唯一写 verdict。
> 2026-10-15 patrol-20261015-P1014E：8b4b82b（E1b 气泡探测 + V4 通知记录断言）首跑 38619441591 红两处：①**V4 红出冷却竞态**——V1 绿（tile #ff3b30 382px，注入确达 L0）后 2s 即红「GLM Critical 记录 0 条」，60s 轮询 2s 即退出=记录存在但 severity 非 critical；读码锁因：connection-degraded 冷却按 (规则,widget) 键且跨级别共享（NotificationEngine._lastNotified），首启实例 GLM 首跳刷新若撞 Ensure-MockServer 重启后的 bind 窗（连接被拒），PublishStale 先发一条 Warning 记录并武装 15min 冷却（WidgetHost:174 无缓存亦发 Warning；F5 同 run 绿证引擎链本身无恙且 F5 从不断言 severity——camelCase「critical」期望首由 V4 落地），随后注入 Critical 被去重吞掉。修法=V4 独立重启自证：mock 探针确认热态 + notifications.json 删净 + 新引擎冷却表空 → 首个 GLM 事件即注入 Critical，确定性落盘；V4 移至 V2 后（V1/V2 同享首启实例），V3 崩溃基线随 V4 重启移动（首启实例 +70s 内被收、其 +90s 注入未及触发），失败信息带记录 severity dump。②**X 红出 UIA 矩形 NaN**——19:29:40 炸于 X1 行右键落点 `[int]($rect.X+$rect.Width/2)`（W4 绿后 24s、X0 基线已过=行已找到），RecentList 行被虚拟化/刷新 tick 重建中途 UIA BoundingRectangle 返 NaN 矩形，转换炸且无断言消息。修法=新增 Get-ClickPoint 助手（GetClickablePoint 优先——不可点抛 PointUnavailableException 而非返 NaN，退回矩形中心，NaN/空/解析失败返 null），X1 两处落点 NaN 按落空走整链重试；W3/W4 chip 点击同型隐患一并收编。③E1b 二次启动气泡 12s 轮询未现于 UIA → 按设计降级 ::warning 转装机（无头会话 toast 基础设施不可用，F5t/G7c 同口径），E1 进程半已绿，行 31 呈现半仍装机。CI 文件改动门禁：全量 1078 测试绿。二轮复证跑 38633490811（f42f822）又红两处均脚本面非产品：①V4 前置裸探针 5 败假红——本会话 PS 5.1 Invoke-WebRequest 恒抛（无 -UseBasicParsing、IE 解析引擎缺；对照绿跑 38608625892 同款全程 11 次「已监听」假重启=探针从未成功过，f42f822 首个以探针 200 为门的设计必假红），修法=前置改 Ensure-MockServer（作业态 Running 校验）替代裸探针门，既有探针一字不改（其恒抛正是每场景无谓重启常态的组成部分）；②W3 chip 点击落空——失败截图实证浮 tile 窗 z 序压住 chip 行（UIA 树含被遮挡元素、落点也准，SendInput 打最上层 tile，过滤永不触发），修法=新增 WinEnum.MoveNoFocus（SWP_NOZORDER|NOSIZE|NOACTIVATE）点击前挪走全部 tile 至面板正下方；X 面同跑全绿=Get-ClickPoint 防护复证过。三轮复证跑 38643228326 3m34s 即红：本批 W3 挪窗两行裸参数（`[WinEnum]::RectOf $panel`——::Method 参数必须入括号，否则 ParserError 启动即死），15b265e 改括号参数，并建本地 PS 双门（dotnet tool PowerShell：ParseFile 全文解析 + Add-Type here-string 本地编译，两门 CLEAN 才推；注意 workflow 步 shell: pwsh 只做 parse+自重启、正文实际跑 Windows PowerShell 5.1，pwsh-7-only 语法本地过 5.1 炸仍需人工对照）。
> 2026-10-15 patrol-20261015-F：批3-补58 修法后连跑三轮同签名红（38633490811 / 38644793130 / 38646163259，最后一条是 schedule 自动跑的 13a5905，排除人工因素），签名恒为「GLM Critical 通知记录 0 条（期望恰 1，实为 1 条 severity=critical critical）」，且每轮 V1 都绿（注入确达 L0）。读码锁出**批3-补58 只修了一半**：(a) 轮询退出条件是「首条 GLM 记录出现」、severity 判定放在循环外，首快照若恰是 PublishStale 的 Warning 记录（首跳失败无条件投递 + 15min 冷却按 (规则,widget) 跨级别武装），Critical 必被去重吞掉，此竞态独立于 mock 热态；(b) Ensure-MockServer 校验的是作业态 Running，HttpListener 的 bind 完成与作业态不原子；(c) Remove-Item -SilentlyContinue 失败无任何信号，旧记录会混进断言快照。修法 74f9f32 三处加固：轮询改「出现 critical 记录才退出」再断言恰 1 条；Ensure 之后补纯 socket 预检（TcpClient 连 127.0.0.1:18081、20s 超时 500ms 轮询，失败即红 V-FAIL-mock-probe——本会话 Invoke-WebRequest 恒抛不可当探针，socket 无该依赖）；删 notifications.json 后 Test-Path 校验未删净即红 V-FAIL-notif-clear。取证面同时升级：V4 失败把 notifications.json 原样与 app 日志拷进 evidence（此前唯一 app 日志归档点在 F 场景 1415 行，V4 现场零日志，每轮只能靠截图猜），severity dump 改 severity@id（id 含规则+时间戳+序号，可直接判定冷却键）。未解一处：签名「1 条 severity=critical critical」的空格拼接，pwsh 7 本地穷举只有两种形态能给出（单记录 severity 为 2 元素数组，或 [[rec,rec]] 双层数组走成员枚举使 $_.severity 枚举出 2 项、-eq critical 全不命中而 .Count 仍报 1），但 .NET 侧只有一条一级写入路径（SaveAsync：lock + WriteAllText(temp) + File.Replace）无法产出嵌套 JSON，疑似 PS 5.1 ConvertFrom-Json 对扁平数组的管线枚举行为与 pwsh 7 不同；本批原样归档让复证跑若仍红时可直接读文件判定。门禁：本地全量 1078 绿（Core 368 / Actions 27 / Connections 652 / Storage 31）+ parse/Add-Type 双门 CLEAN + BOM 在 + brace/paren 增量持平；另修 docs/troubleshooting.md 的 ../README.md 出集链接为绝对 URL（13a5905），mkdocs strict 由恒 abort 转绿（纯文档改动故只跑 mkdocs、不跑编译）。nightly 38654763928 复证中，绿则行 68 转已代验。
> 2026-10-15 patrol-20261015-P1015：批3-补59 三处加固后 38654763928 仍红（第四轮），取证面升级即兑现价值——读归档 notifications-V4-fail.json 原样：内容是**扁平数组 2 条记录**（GLM severity="critical" 标量、DeepSeek severity="critical" 标量），GLM critical 记录确实在案，断言本该通过却报 0。签名升级为「GLM 记录 1 条：critical critical@id=builtin.connection-degraded-…-2 …-1」，三个观测值（Count=1、severity 枚举成 "critical critical"、id 枚举成两条、-eq 全不命中）只有 [[r1,r2]] 嵌套一层读法能同时满足——pwsh 7 本地对同款嵌套 JSON 直接摊到 count=2 不需处理，PS 5.1 保留外层 count=1，**同型 JSON 两版本解析结果不同**，即批3-补59「未解」疑点落定。app 侧不可能写嵌套（SaveAsync(IReadOnlyList<NotificationRecord>) 经 System.Text.Json 只产扁平数组，归档原样亦扁平）=PS 5.1 读侧行为、非产品缺陷。修法 316c121：ConvertFrom-Json 后检测「外层为数组且首元素亦为数组」即摊平一层，severity 判定由 "$($_.severity)" -eq "critical" 改 @($_.severity) -contains "critical"（标量与枚举两态皆命中）。取证顺带澄清 V4 现场真发生的是 connection-degraded 跨界（app 日志 00:05:06.139 启动→.194 双 widget 各发 1 条 connection-degraded critical→.250/.253 刷新全 refreshed ok），即注入 Critical 落在连接降级通知上=V4 要验的「恰 1 条 critical」，故四轮「0 条 critical」从来不是产品缺失而是断言读法错。门禁：parse/Add-Type 双门 CLEAN + BOM 在 + brace/paren 增量持平 + 全量 1078 绿（Core 368/Actions 27/Connections 652/Storage 31）；新逻辑对归档扁平 JSON 与人工构造嵌套 JSON 两种输入在 pwsh 7 下均得 vRecords=1/vCrit=1。nightly 38661655290（316c121）复证中，绿则行 68 转已代验。
> 2026-10-15 patrol-20261015-P1015R2：38661655290 全链绿到 X 前——V1/V2/V3/V4 全部通过（V4 恰 1 条 critical 记录=行 70 Toast 引擎链半转已代验，V3 崩溃注入复证绿），X1 首红即锁出脚本面缺陷：**BEACON_DEBUG_* 环境变量成功路径泄漏**。V1/V2 模式 Start-Process 后立即 Remove-Item Env:\BEACON_DEBUG_SEVERITY/CRASH（3403-3407），V4 只在 catch 清（3601-3602），V4 首绿把注入漏给后续每个 app 实例：W/X/Y 全部刷新被改判 Critical/Failed 且每实例武装 +90s 崩溃注入（X1 前 15s 轮询期 GLM 已武装崩溃定时器，01:20:24 红时点距启动 ~20s 未到触发，但 NEEDS ATTENTION 的 GLM 38% 健康值与在案 critical connection-degraded 通知都是强制 Critical 生效的实证）。相关时序吻合：V4 首绿前 X1 三跑全绿（38590456994/38602176751/38608625892），V4 后首轮即红。修法：V4 Start-Process 后补同款两行 Remove-Item（注入只给本实例，子进程已继承）。随批 X 失败取证补点：X 块起头定义 $xLogPath/$xWidgetsPath，X1 两个失败点（widgets.json 未翻 / tile 未消失）把 widgets-X1-fail.json 与 beacon-X1-fail.log 原样拷进 evidence——X 场景此前零 app 日志归档（V4 三轮盲区同型的 X 侧补点），失败信息指引读两份归档定因。门禁：parse/Add-Type 双门 CLEAN + BOM 在 + brace delta 0 / paren +7 平衡 + 全量 1078 绿。X1 复证由后续 nightly 承担（新修复若在，X 面应全绿）。
> 2026-10-15 patrol-20261015-P1015R3：X1 复证绿落账——nightly 38669041567（819b257）conclusion=success，V4 复证绿 + X1/X2/X3 全过（Unpin 落盘 + tile 移除/回归 + 重启持久化两入口读态一致）+ Y 面全绿，env 泄漏修后 X 面如期全绿，批3-补61 锁因闭环。行 60 右键 Pin 落库半维持已代验（本次为泄漏修复后的复证，非新开卡）。

## 决策记录

- **SignPath Foundation 申请被拒 → 暂不推进签名，保持现有链路待命**（2026-10-14 用户拍板选 A）：SignPath 官方回信说明 Foundation 需要项目已具备公开可见度（stars / forks / 贡献者、外部文章、社区讨论、机构背书），本仓未达标，非质量问题，欢迎有影响力后再申请。**处置**：`daily-build.yml` 的签名段与 `SIGNPATH_API_TOKEN` / `SIGNPATH_ORGANIZATION_ID` 两个 secret **原样保留不删**（链路已搭好，只缺有效凭据），仅在版本 tag 通道签名、nightly 永不签的口径不变；等有对外正式发版需求时再决定是否走付费订阅（docs.signpath.io/change-subscription）或届时重新申请。**连带作废**：此前待用户处理的三件（Submitters 加人、GitHub App 安装授权、Artifact Configuration slug 回传）随之失效，不再追踪。未签名的影响仅限 Windows exe 触发 SmartScreen「未知发布者」警告，不影响构建与其它平台产物。

- **tile/胶囊底部裁切 = 外框/客户区尺寸混用（AppWindow.Resize 直喂客户区需求）**（2026-10-14 用户缺陷单「底部进度条显示不全（还是一样的问题）」，run 38488861749 工件逐像素取证）：AppWindow.Resize 设的是窗口外框，PinnedHostWindow 面板/细条、FloatingTileWindow、CapsuleWindow、QuickPanelWindow 四处全把客户区需求尺寸（行×32 DIP / 内容实测）直接喂 Resize——无边框 OverlappedPresenter 仍保留 ~6px 非客户区 resize 边框，每窗底部 ~6px 落进不可见区。缺陷形态：tile 设计 32px、实测客户区 26px，底部 4 DIP 进度条整条不可见（A 场景 GLM tile 26px 无条带、D 场景 5 悬浮窗全 26px、H 场景面板末行 tile 26px 无条而首行完整 32px 带条——「首行完整」正坐实窗口底部裁切而非 bar 渲染缺失）；胶囊（同为 32 DIP 高）与快捷面板同型底部裁字。同型先例：DetailWindow c80c244（2026-10-11 装机「没有修复」根因）。修复：ChromeSizePx（GetWindowRect−GetClientRect，取不到兜 0）口径收编 NativeMethods 供四窗共用（DetailWindow 私有副本同收），Resize 一律补回差值；CapsuleWindow._sizePx 改记外框尺寸（RestorePosition/SavePosition 锚点数学同口径，save/restore 往返不漂移）；QuickPanel 定位按外框口径保 24/140 DIP 可见边距。CI 断言同步去 vacuous：Count-BarPixels 旧实现裁外框、底部 6 行落在窗框外桌面区，「465/507 像素」全是桌面边缘（空转通过实证）——改锚客户区（Assert-ClientHeight 量高用 ClientRectOf、条带裁图用 ClientScreenRectOf：GetClientRect 返回窗内坐标，裁屏图必须经 ClientToScreen 换算屏坐标，4ae613d 补齐；期望高按窗 DPI 缩放，口径与 DpiLayoutMath.ToPx 同取整）+ 新增 Assert-ClientHeight 硬断言（客户区高=设计值 ±1：A/D 逐悬浮窗 32 DIP、H5 面板行高×行数整数倍）；亚像素阈值 ±1 口径同行 21 状态灯决策。GLM tile 空态收起（state.Progress 为空 bar 收起）与有值路径同一容器/同一行高预算，收起只藏 bar 不改窗高——本缺陷在窗口层不在 tile 层，两态尺寸预算一致（行 21「GLM tile 无进度条」实为空态收起+本裁切叠加的观测）。SettingsWindow 构造期 Resize 同型未在本单修（默认标题栏样式、症状=设置窗记忆尺寸逐轮矮一截，留后续行）。
- **断网降级通知缓存命中路径永不开火**（2026-10-12 装机清单 CI 代验转化批3-补50，场景 F 首跑实证）：PublishStale 缓存命中时 severity 沿用缓存值（健康时=Success），NotificationEngine 入口 Success/Info 一律静默 → builtin.connection-degraded 只在「从无缓存首次失败」开火，真正的「有数据后断网」（规则名所指的降级）反而不通知。修复：缓存命中 stale 路径 severity 抬到 Max(cached, Warning)（与无缓存路径一致；tile 灰显由 ConnectionHealthy 驱动不受影响，恢复后 provider severity 自然回填）；补 WidgetHostTests 两例回归（Success 抬 Warning / Error 不动）。F5 断言相应移入断网窗口内 + F5b 恢复后记录恒 1。
- **moonshot.balance 数值位补映射**（2026-10-12 巡检点火，用户倾向「补一行映射」→ 拍板落地，批3-补49）：payload 只有 `available_balance`，不在 `WidgetValueHint` 优先级链 → 数值位跌 `Summary[..7]+"…"` 截断，数字全没（「Moonshot · ¥110.00」→「Moonshot…」）——正是 hint 链立项要消灭的「数不出值」失败模式。**口径**：`¥{value}` 固定 CNY 符号（开放平台只按 CNY 计价，provider Summarize 已硬编码 ¥，不引 deepseek 式币种开关）；`0.00` 顶格形态，宽预算与 deepseek 同款 `¥99999.99`。同轮 hint 链全量自查另修两处：qwen 配额用尽路径补 `value_text="已用尽"`（比照 mimo「无额度口」显式文本先例）、machine.git 补 `value_text`（Summary 计数段同源，兜底截断把 ↑↓/改 计数全截没）。
- **tile 状态灯像素断言阈值重标定（亚像素渲染漂移）**（2026-10-12 批3-补51，38261911354 工件逐像素取证）：8DIP 状态灯同一几何在不同窗口实例渲染于不同亚像素位置 → 匹配像素 10~44 px 漂移（D 场景五实例 10、G2 三轮 44、G1 品红两跑 44/24、G4 橙 24/24；tol45=tol60 计数相同，tol90 起误吞 icon 灰）。G4 连红两轮的 24 px 是半像素偏移的正常渲染（精确核 16 + 0.89 覆盖率均匀边缘环 8），**非 3be02b6 所诊「呼吸调光」**——呼吸只存在于 full 档（StartBreathing 入口 `Mode != Full` 即返回），reduced/off 根本不启动；该次误诊引入的 motion off 钉死无害（G7b 帧差分仍依赖），保留。GLM tile 无进度条（state.Progress 为空 bar 收起，G2 绿 bbox 8×8 实证），此前「状态灯+条带」「95% 填充外推 ~92px」均为误设。失效模式（palette 未用新色/渲染缺失）恒 0 px → 色彩存在性断言统一定标实测底（10）之下、0 之上 = 8（G1/G2/G4/G5/G7a 五处）。另证：PS 非前台时 BringToFront 对任意窗口静默失败（E2/E3 菜单仍不弹的根因），托盘菜单驱动改 AttachThreadInput 共享前台权（WinEnum.ForceForeground）。
- **reduced 档变色过渡被每 tick 重放（同色闪灯缺陷）**（2026-10-13 patrol-20261013-A1，读码发现）：PinnedHostWindow.Update 的 TransitionFill 不受色变门控（`changed` 只闸 Flash）——reduced 档下每个刷新 tick（ci 档 15s）状态灯都 opacity 0→1 凭空灭一次再渐亮（400ms×intensity），违反 MotionEngine 自述「换色后亮度渐入」契约。修复：`_lastFillColor` 记录上次着色，只在色变 tick 过渡（含离线恢复灰空心→实色），同色 tick 直接填充；full 档呼吸/Critical 脉冲路径不变。转化场景 L（todo 行54）：状态灯条带裁图 20×18 @ tile 内偏移 x6/y10（G2 工件实测灯 bbox x13-20/y15-22），~15ms 周期连拍；L1 同色 tick 22s 灯恒在 ≥8px；L2 mock 交替翻档，TransitionFill=200ms×2.0 前载缓动（cubic-bezier 0.1,0.9）可视渐入窗 ~50ms，环内找「与两端都差 ≥8px」中间态帧——瞬时切换只可能出现端点帧，中间态帧即渐入的直接证据；≥2 可判定轮零中间态=真回归红，采样空洞降级 warning 转装机（同 G7c 口径）。

## MVP 待验/未修项（50 项 = 31 勾 / 19 未勾；原样搬自 docs/mvp-issues.md，未完成项即装机清单）
- [x] 双击 exe 二次启动不重复进程，既有实例收到唤起气泡提示（CI 已代验进程半：场景 E1 二次启动进程数恒 1；E1b 气泡呈现探测已建、无头会话不可见降级 ::warning（38619441591）；**2026-10-17 真机实证双半**：本机双击（os.startfile 同路径）二次启动进程数恒 1，主实例日志 `Balloon dispatched: Beacon - Beacon 已在运行。`（Shell_NotifyIconW 返回 true=Windows 已接受气泡）+ `Secondary instance exit.` 双锚；随批给 ShowBalloon 补日志锚——原实现丢弃 Shell_NotifyIconW 返回值，装机复测无取证点，现成功落 Information/失败落 Warning（TrayIconService.cs:99-110））
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出（场景 E2 关窗进程驻留 + E3 托盘菜单 Exit 退出已代验）
- [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱（位置还原半已代验：场景 R 预设坐标落位 ±4 且重启一致；拖动手势半已代验：R3 SendInput 拖 +50/+40 窗随动 + shell-state.json offsetDips 落盘 90,81 + 重启还原闭环（38750239155）；跨 DPI 拓扑需装机半）
- [x] 任意前台应用下热键生效；可在设置改键并持久化（场景 K1 注册成功 + N 改键落盘重启新键注册 + WM_HOTKEY 直驱面板出现已代验）
- [x] 开关即时生效（重启验证）；卸载清理（场景 J 静默安装卸载 + M HKCU Run 双向断言已代验）
- [x] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口（场景 T2 外部故障注入 + V3 崩溃注入已代验，38494525200；V3 曾红出真实发现并建 DispatcherQueueGuard，见决策记录）
- [x] 无网启动：界面有数据且标注 Last update，随后自动刷新（场景 F1-F4 已代验：缓存数据 + Last update 45s 标注 + 恢复后 90s 自愈）
- [ ] 真实 PAT 手动验证连接测试成功/失败路径（需真实 GitHub PAT；mock 链路已由场景 D 代验）
- [x] 夹具单测：计数/映射正确（GitHubPullRequestsProviderTests 18 例）
- [ ] 真实仓库手动验证（PR 计数与亮黄随刷新走）（需真实 PAT 与真实仓库；API 层已 18 例假 Handler 单测覆盖）
- [x] 状态映射表驱动单测（GitHubActionsProviderTests 9 例）
- [ ] 真实 workflow 手动验证（成功/失败/运行中）（需真实 PAT 与真实仓库 workflow 状态流；映射层已表驱动覆盖）
- [x] 路由/确认/超时单测（ActionRunnerTests 9 例）
- [x] 失败在 UI 有可见反馈（场景 U5 已代验：mock 500 后 L3 失败反馈文本硬断言，U4 成功反馈同链对照）
- [x] 模板渲染单测（TemplateRendererTests 11 例）
- [ ] 手动验证浏览器打开正确 URL（默认浏览器属用户环境；URL 拼接已模板渲染单测覆盖）
- [x] API 封装单测（假 Handler 单测：GitHubApiClientTests 10 例 + GitHubWorkflowExecutorTests 6 例）
- [ ] 真实仓库手动：运行中到终态全程可在 Beacon 看到（需真实仓库 workflow 触发与真实 PAT）
- [x] 超时终止生效；输出落盘（LocalCommandExecutorTests 两例：超时杀进程仍写日志、真实命令捕获输出写日志）
- [x] 确认弹窗可关（按 Action 配置）（场景 U3 已代验：弹窗可关 + 取消后 mock 零 POST 负证；U4 确认走通）
- [ ] 发送/点击/激活路由全链路手动通过；快捷方式缺失可自愈（自愈半已代验：场景 F5t 预删 AUMID 注册键后 Initialize 重建，38494525200；点击/激活路由属 OS Toast 面，需装机半）
- [x] 规则引擎表驱动单测（含冷却）（NotificationEngineTests 17 例）
- [x] 注入 Failed 状态收到且仅收到一条 Toast（NotificationEngineTests 17 例覆盖引擎去重 + 场景 F5 断网降级恰 1 条硬断言）
- [ ] 聚合状态变化 ≤1 个刷新周期内反映到托盘（托盘图标像素不可脚本取证，见决策记录；tile 层已代验：场景 G7a 38% 到 95% 75s 内反映）
- [x] 断网显示 Offline+时间；计数与实际 Widget 状态一致（场景 F2 tile + O 面板 + Q 胶囊三面已代验）
- [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭（CI 已代验缓存无白屏 + Esc 键盘关闭面板：场景 K3 Esc 改硬断言全链实证，根因 INPUT.type 误写 0（INPUT_MOUSE）静默丢键已修 84f9aad，run 38743014862；旧「键盘注入 CI 不可达」判断系 type 缺陷自证；100ms 精确计时需装机半）
- [x] 计数/事件与状态一致；右键 Pin 落库（计数半场景 O + Recent 事件流半场景 S + 右键 Pin 落库半场景 X1 已代验：右键 ContextFlyout 点 Unpin 后 widgets.json 翻转 + 悬浮 GLM tile 移除，run 38590456994）
- [x] 从 L2 事件一键打开对应详情；[Retry] 全链路可用（场景 S4 UIA Invoke 开详情 + U1-U5 Retry 全链已代验）
- [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面（刷新不打断已代验：场景 H1 前台句柄不变；空白穿透宿主裁决已代验：H5b WM_NCHITTEST tile 行=HTCLIENT、外框空白环带=HTTRANSPARENT（38750239155），真实鼠标点击落到下层窗观感需装机半）
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab（场景 G7a 两 tile 独立 + H WS_EX_TOOLWINDOW 置位已代验）
- [ ] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一（还原与二选一已代验：场景 H 两轮重启精确落位 + panel 模式悬浮窗归零；拖放功能链已代验：H6 SendInput 拖 +80/+60 窗随动 + widgets.json floatingX/Y 落盘 200,180 + 重启还原闭环（38750239155）；手感需装机半）
- [x] 两种入口（设置开关/右键 Pin）行为一致并持久化（场景 X 已代验：X1 面板右键 Unpin 落库 + tile 移除；X2 重启后设置钉开关读态 Off 再 Toggle On 同链落库 + tile 回归；X3 再重启读态 On + 行标已钉，run 38590456994）
- [x] 非准入类型在两处入口均不可钉（场景 Y 已代验：播种未登记类型 pinned:true，Y1 主机 Resolve=null skipped 日志+零 tile，Y2 面板右键无 Pin 菜单项，Y3 设置 scope 过滤整行不产，Y4 widgets.json pinned 恒 true 零改写，run 38608625892）
- [ ] 重启后位置/收起态还原；拔显示器再接回 tile 不丢（位置半场景 H + 收起态半场景 P 已代验；拔显示器物理事件需装机半）
- [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区（需物理或虚拟显示栈，CI 单屏不可驱动）
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈（场景 F3 对话框数 0 + F4 恢复 90s 内读数回归已代验）
- [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出（L0 变色+脉冲半已代验：场景 V1/V2，38459944253；Toast 引擎链半已代验：场景 V4 注入 Critical 恰 1 条 critical 记录（connection-degraded 跨界），38661655290；「无打断」观感与 Toast 呈现面需装机半）
- [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录（四条含真实网络环境与真人操作流，需装机）
- [ ] 设置改级别色后 L0 tile、胶囊、托盘图标同步变色（tile 与胶囊半已代验：场景 G4/G5 改色后 tile 与胶囊灯同步换橙、重置回绿，run 38561621450；其间 d71bdc8 状态去重曾断悬浮窗改色重绘链（既有窗不重放状态），920e23c 补重放后 run 38953986133 重证 380px/57px；托盘图标像素取证需装机半）
- [x] 某 Widget 设 colorOverride 仅该 tile 变色，优先级正确（场景 G1 已代验：品红 ≥8px + DeepSeek 零渗 ≤4px）
- [x] 非法颜色值回退默认色不崩溃；重启后保持（场景 G2 两轮启动已代验：进程存活 + 非法值零渗 + 默认绿 ≥8px）
- [x] Success 到 Error 状态变化有平滑变色过渡（reduced 档）（场景 L1/L2 已代验：38284623093 三轮渐入中间态帧 + 同色 tick 不闪灯）
- [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调（四半已代验：G7b off 静止 + G7c full 呼吸 + G6 强度落盘 + V2 脉冲帧差分；观感与强度裁量需装机半）
- [x] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）（焦点半：场景 H1 前台不变；发声半：常青门 AudioApiAbsenceTests 全源 206 文件 ×10 发声 API 词断言红绿自证，新增发声面即红，通知链显式 Sound:false、OS toast 声属用户规则面非动效面；循环半代码审计：Forever 循环仅 MotionEngine.StartBreathing/StartPulse 两入口，消费面仅 PinTile.ApplyLoops 与设置页预览（显式 stop），收起帘控 PinnedHostWindow.ApplyLayout strip→SuspendMotion 全 tile、悬停/拖拽/Reconcile 全路径经 ApplyLayout，悬浮窗只挂一次性 Flash/SlideIn 且 stale 窗 Close 销毁非隐藏常驻；真机听声目检仍可装机复核）
- [x] 导出文件全文无 token；导入+重录密钥后行为与原机一致（场景 Z picker 双驱代验：导出→导入往返反馈计数逐字段对齐+重录密钥提示在案+导出全文无密钥真值；204b884，run 38725138151）
- [ ] 全流程录屏/截图归档至 docs/；发现问题全部修复或开 Issue（截图已归档至 visual-evidence 工件；真人全流程录屏需装机半）
- [ ] 干净 Windows 11 虚拟机：下载后运行并完成 B-803 脚本（用户验收终局；CI 是构建证明不是验收替代）
- [x] 调色/动效改动即时预览并持久化，重启生效（场景 G4 改色即时 + G6 档位落盘 + G6b 重启 UIA 选中态恢复已代验）
- [x] 重置默认一键还原；off 档预览即全静止（场景 G5 重置回默认 + G7b off 三帧差分 ≤12px 已代验）
- [x] tile 底部进度条显示不全（2026-10-14 缺陷单「还是一样的问题」）（02a5de1 修复，38500538817 复证全绿：client 32px/frame 38px chrome 6px + 条带 620/676 真像素，见决策记录同名条）

## 后续可选（master-plan.md，5 项 = 4 勾 / 1 未勾）
- [x] L3 内动作链（[Retry] 全链路）CI 场景化（38434164526 实证 U1-U5 全绿；解锁行 32/38/46；98f7a2d 后 U 连红根因=ComponentText.Body MaxWidth=NaN 恒 E_INVALIDARG 致 L3 详情窗必炸：896758c 修复 + 0df60a8 取证（38976398309 拿到栈）+ e9f393c 场景加固，38999120347 复证全绿）
- [x] Critical 与崩溃注入机制（debug 门控）（DebugInjection 8 例单测 + DispatcherQueueGuard 兜底；38494525200 实证 V1/V2/V3 全绿，V3 红出真实发现见决策记录）
- [ ] 厂商私有打包工具 Provider（需真实厂商 API 与认证字段；开源标准 API 的 Jenkins Provider 已落位 2026-10-11）
- [x] SettingsWindow 构造期 Resize 同型外框/客户区混用（f4b0eb1 修复：记忆尺寸存客户区 DIP 直喂外框，每次开窗矮一标题栏）
- [x] AI Usage Provider（11 家已落地，可靠性三档；条件门控满足后关闭，见 master-plan §13）

## MVP 验收标准（master-plan §14，8 项 = 5 勾 / 3 未勾，需真实装机验证）
- [ ] Windows 登录后按用户设置启动（Run 值可执行半已代验：M3 把注册表原样字符串交 ShellExecute——登录时 explorer 处理 Run 键同一 API——拉起 Beacon 进程（38761946476）；真实登录会话由 explorer 调度属 OS 行为，需装机半）
- [ ] 托盘与状态胶囊稳定运行（驻留半已代验：场景 E2/E3 关窗驻留 + Exit 唯一退出；长时稳定与胶囊观感需装机半）
- [x] 全局快捷键正常打开悬浮面板（场景 K1 注册 + N 改键重启注册无失败 + WM_HOTKEY 直驱面板硬断言已代验）
- [ ] 面板展示真实 GitHub 状态（需真实 PAT 与真实仓库）

验收：CI windows-latest 出截图或给安装包名 + SHA256 让用户自验；「改了未验」不算完成。

- [x] 事件看板：默认全部 + 可点击过滤（QuickPanelFilter 四区统一过滤；活动 chip 实心高亮 + 概览头「OVERVIEW · 源」=过滤态可见，再点回全部；e905a48+23660f8，1078 测试绿，38549671960 场景 W3 实证）
  - 要求：看板**默认显示全部事件**；同时**可以点击对应项只看该类过滤后的结果**
  - 验收：默认态是"全部"，点击某类后只显示该类，再点可取消回到全部；过滤状态在界面上可见（当前在看哪一类不能是隐式的）
- [x] 看板与多语言：缺中文、设置里无切换选项（核实本仓原无 i18n，建 UiStrings 唯一机制覆盖看板/托盘/设置新增项；设置语言下拉即时生效+落盘，zh 系统开箱即中文；e905a48，1078 测试绿，38549671960 场景 W1/W2 实证）
  - 现象：**看板没有中文**；且**多语言设置中没有切换选项**
  - 验收：看板文案纳入既有 i18n 体系（先核本仓 i18n 机制怎么做的，**不另起一套**）；**设置页要提供语言切换项**，切换后看板同步生效

- [x] 悬浮框边缘坍缩：难用且无关闭开关（开关默认开=保持已发布行为，关=拖边不收起且已收起立刻弹回；收起态点击细条=粘性展开，步骤：拖贴死边收成细条、点细条任意处展开保持、拖离边缘恢复普通展开；e905a48，1078 测试绿，38549671960 场景 W4 + 场景 P 收起/展开宽度回归）
  - 现象：**悬浮框靠近桌面边缘就坍缩**，**没有关闭该行为的开关**；**坍缩之后很难再点开**
  - 风险定性：这是**可用性缺陷**——功能默认开启且无法关闭，导致用户失去对悬浮框的控制
  - 要求：**加开关让用户可关闭边缘坍缩**（默认值由你判断并说明理由）；同时**修坍缩后难以展开的问题**——坍缩态必须有可靠的展开入口（点击/悬停/快捷键任一，且不要求像素级精度）
  - 验收：设置中能看到并切换该开关；关闭后悬浮框靠近边缘**不再坍缩**；坍缩态下**能稳定展开**（给出可复现的操作步骤）

- [x] 语义澄清/缺陷：菜单中「Open Beacon」与「Notifications」效果相同（意图出处=B-503：docs/mvp-issues.md:266「托盘菜单含常用 Action 与 Notifications 入口」+ 原 App.xaml.cs:174 注释「Notifications 入口落 L2 通知中心视图」；缺陷=两事件同接 Toggle；分化：Open Beacon=开合面板、Notifications=直达通知列表（Show+滚动+焦点），两文案入 UiStrings 可译；e905a48，run 38549671960 场景 W1/W2 文案面实证）
  - 现象：托盘/菜单里 **`Open Beacon` 与 `Notifications` 两个入口效果一样**，用户表示**很困惑**
  - 要求：
    1. **先查清当初的设计意图**（读代码与文档，给 file:line 与出处），说明两者**本应**是什么关系
    2. 既然效果相同，**必须二选一处理**：要么让两者**语义分化**（打开主界面 vs 打开通知面板），要么**合并/去掉其中一个**——**不允许保留两个同义入口**
    3. 若本仓已有 i18n，两个入口的文案都要能被翻译（与上面那条多语言需求一并处理）
  - 验收：给出设计意图的出处；处理方案与理由；真实操作验证两个入口**不再等价**（或只剩一个）

## 文档双语补全（2026-10-16 用户令；2 项全勾）

- [x] docs/ 根目录实质文档全部成对，当前 16 篇（13 篇真实完整译本：index / mission / positioning / configuration / solution-structure / troubleshooting / privacy-policy / store-submission / 审计-文档一致性 / acceptance-checklist / acceptance-B705-L0-behavior / acceptance-B803-e2e-walkthrough / todo-triage-2026-10-16；3 篇过长只留切换器与「暂无译版」说明：master-plan 1134 行 / mvp-issues 1371 行 / mission-excerpt 1579 行；16/16 双份第 1 行语言切换器且在首屏；命令、路径、代码块、专有名词与台账 ID 原样保留不译；本目录链接 0 断链；mkdocs build --strict 绿）
- [x] docs/design/ 6 篇与 docs/rfc/ 1 篇双语配对（口径同根目录；6 篇真实完整译本：collapsed-progress / redesign-2026-10-16-契约 / 映射与迁移 / 核验 / 组件库落地 / RFC-001-technical-design，6/6 行数与标题/表行/围栏/勾选计数全等（如 RFC-001 487 行 17+18 标题 83 表行 22 围栏）；1 篇过长只留切换器与「暂无译版」说明：redesign-plan-2026-10-16 1395 行；7/7 双份第 1 行语言切换器；代码跨度内源注释/模板串与 `已代决①-⑩` 等台账 ID 原样保留；链接 0 断链；mkdocs build --strict 绿；`51492ae`）

## 组件化重构分期（2026-10-16 用户执行单采纳，依 `docs/design/redesign-plan-2026-10-16.md`；现状核验见 `docs/design/redesign-2026-10-16-核验.md`）

> 性质：外部设计稿，不是本仓事实。核验结论——设计稿关于「已有实现」的断言约六成在仓内确有其事；「响应式状态系统」「显示组件库」两章的基本前提在仓内大面积查无；「HTML 小屏幕」一章查无，且仓内已有反向记录（`docs/master-plan.md:769`）。查无只说明从未写下来，不证明从未设计过，不得当作全新需求立项。
> 期次划分照设计稿 Phase 0 到 Phase 6，共 7 期 43 条，**不自行重排**。落地一条勾一条，括号内补落地 commit hash。
> 进度：43 条 = 39 勾 / 4 未勾（2026-10-17 复算：Phase 0/1/2/3/4/5 全清——Phase 3 六行、Phase 4 六行（2026-10-17 用户裁定「这个设计下吧」，b1893a1 起五提交落地）与 Phase 5 五行均已落地；未勾 = Phase 6 四条，均余装机半）。

### Phase 0 现状审计与契约冻结（6 条 = 6 勾，期次完成）

- [x] 列出现有 Provider、Widget、Action 与 Renderer 清单（23 类型键 + 17 连接 + 7 执行器 + 6 窗口 + 47 Core 文件全量盘点：docs/design/redesign-2026-10-16-映射与迁移.md §1，e1229fc）
- [x] 核对 `WidgetState` 的事件总线、刷新调度与 ActionRunner 契约（50 条核验表 A03 A04 B03 B04 B05 B11 逐条核过：docs/design/redesign-2026-10-16-核验.md，759742b）
- [x] 识别现有状态模型中哪些字段是业务状态、哪些只是显示字段（WidgetState 12 字段逐一定性 + 配置字段三分类：映射与迁移 §2，e1229fc）
- [x] 确定新组件模型与旧配置的兼容方案（已代决④：映射与迁移 §5 四条照此冻结，docs/design/redesign-2026-10-16-契约.md §3，409d4e7）
- [x] 保留现有已完成功能与真机验收要求（装机 71 条清单与 CI 视觉取证场景列为 Phase 6 验收底座：映射与迁移 §4/§5.4，e1229fc）
- [x] 冻结状态流契约与组件契约（四类概念映射 + IStateStore 签名 + 变更检测口径 + 兼容四条冻结，已代决①②③④ 落决策记录：docs/design/redesign-2026-10-16-契约.md，409d4e7；HTML 边界留问已裁定关闭：docs/design/phase4-html-ruling-2026-10-17.md）

### Phase 1 响应式状态与数据流（5 条 = 5 勾，期次完成）

- [x] 明确 State、Event、Command 与 Operation Result 四类概念（四类概念逐项映射仓内落点与语义，Operation Result 目标五态已定待 Phase 5 施：契约.md §1/§4，409d4e7）
- [x] 完善状态变更检测（去重默认启用：内容键排除 FetchedAt、stale/健康翻转必发布、回退开关 suppressUnchanged=false；默认态与翻转行为各有单测锚定 StateStoreTests，d71bdc8）
- [x] 增加或整理统一的 State Store 与 Selector（Store 契约+实现+15 测试：Abstractions/IStateStore.cs + Services/StateStore.cs，WidgetHost 发布点改经 Store 且事件契约不变（f5d1d91）；Selector 收编：QuotaWindows / WidgetValueHint / QuickPanelSections / QuickPanelFilter 归位 Beacon.Core.Selectors 命名空间，签名不动（24678f9））
- [x] 明确订阅、取消订阅、刷新失败与过期状态的处理方式（订阅/退订沿 IEventBus、写入经 IStateStore（接口注释写明，Abstractions/IStateStore.cs）；刷新失败转 ConnectionHealthChanged + 陈旧重发布、过期保 FetchedAt（RFC §6.2.6/§12，WidgetHostTests + StateStoreTests 锚定），f5d1d91）
- [x] 确保 UI 只消费状态、不直接请求外部数据（核验表 A03 已确证：单向数据流 RFC-001:54-63、凭据只经 ConnectionContext 注入、UI 侧经 IWidgetProvider 抽象，759742b）

### Phase 2 设计系统与基础组件库（9 条 = 9 勾，期次完成）

- [x] 统一设计令牌：色彩、字体、间距、圆角、阴影、动效、尺寸、状态（DesignTokens：字号 8 档 + 圆角 6 档 + 间距刻度，C# 四窗 47 处字面量清零、XAML 圆角走 App.xaml 资源镜像；色彩/状态/动效/尺寸委托 UiPalette/ThemeColors/MotionEngine/DpiLayoutMath 单一来源，值零变化；bespoke Thickness 组合待组件化逐面收编，3be4efc）
- [x] 基础组件 Text、Icon、Surface、Badge、Divider（src/Beacon.App/Components/ 五工厂，字号/圆角/间距全走 DesignTokens；Text+Icon 已收编 MakeStateRow 状态行/事件行（值逐项等价），Surface/Badge/Divider 备组合件逐面收编（a517594））
- [x] 基础组件 Metric、Progress、Status Indicator（ComponentStatus 已收编 DetailWindow 头灯、空心离线变体同胶囊口径；ComponentProgress 静态条 + 档位判定下沉 Core ProgressTiers 单源（10 例测试，PinnedHost BarFillColor 改委托、值零变化）；ComponentMetric 候 Phase 3 绑定收编，bf95dde）
- [x] 基础组件 Key-Value、Status Card、List（ComponentList.Vertical + ComponentKeyValue.Row 收编 DetailWindow 详情表（容器行距/等宽行值逐项等价），Status Card 词汇件组合 Surface.Card 待示例页收编，1db70de）
- [x] 交互组件 Action Button、Action Menu、Link（ComponentAction.Button 收编 DetailWindow 动作区（内边距走令牌、触发链等价）、Item+Menu 收编 QuickPanel 行右键 pin 菜单，Link 词汇件待示例页，bb86927）
- [x] 布局组件 Stack、Grid、Section、Conditional（ComponentLayout：Row 收编 QuickPanel 三行与 Detail 头行/动作行、Columns 收编 PinnedHost 主行六列、SectionLabel 收编 Detail 两区段签、When 收编 QuickPanel 空区收起（值逐项等价、像素断言全绿），Column 词汇件，8caf0b5）
- [x] 通用状态 Empty、Loading、Error、Offline（ComponentFeedback 四词汇件：Empty 居中说明/Loading/Error 换行正文/Offline 空心灯+说明行（同胶囊横幅口径）；现 UX=空区隐藏+XAML 横幅故无调用面，供示例页与 Phase 3 收编，5489352）
- [x] 统一主题、尺寸与排版体系（DesignTokens 单源（字号/圆角/间距）+ App.xaml 圆角镜像同步规则 + 色彩/状态/动效/尺寸委托 UiPalette/ThemeColors/MotionEngine/DpiLayoutMath + 14 工厂排版几何全走令牌；体系 as-built 文档 docs/design/redesign-2026-10-16-组件库落地.md（f7ffb12），本地 mkdocs 门过）
- [x] 独立视觉示例页（SampleGalleryWindow：14 工厂全量实样（含词汇件证明面），DPI/边框补差同 Detail 口径，入口=设置关于区「组件示例页」单实例；纯静态不接数据，e623445）

### Phase 3 组件组合与数据绑定（6 条 = 6 勾，期次完成）

- [x] 定义 Component Descriptor（ComponentDescriptor(Id?,Type,Props,Children) 挂 WidgetConfig.Components 面键开集；嵌套允许 + 8 层/32 子/256 节点上限；结构非法整树弃用回传统渲染 + 诊断进 LoadErrors；形状代决记契约.md §4 已代决⑤（Core 13 例 + Storage 1 例锚定），5e076e6）；运行时关联：App 渲染面消费该形状——ComponentSurfaceRenderer 三面多源渲染与缺源显示（5d7efdf/7ef9fdb），真机观感待装机
- [x] 定义 Widget Instance 与 Binding 配置（ComponentBinding(Field,Source) 随节点进树，源三态 state/selector/const（state 路径受支持字段集封闭 + payload:key，校验前移到加载/解析期）；WidgetInstance 面键解析 fail-closed（无面树/非法树→null→传统渲染），树与 Config 只读共享、解析无 IO；代决记契约.md §4 已代决⑥（Core 21 例锚定），e065107）；运行时关联：声明式面绑定取值即此模型——渲染器按 WidgetInstanceResolver 实例渲染（5d7efdf），真机观感待装机
- [x] 实现 Selector 到组件属性的绑定（ComponentBindingEngine 纯函数解析：state 受支持字段集 + payload:key、selector 注册表（value-hint/quota-windows = Core 既有纯函数）、const 三态，取不到值回空串 fail-closed；输出键 = {节点路径}.{字段} 与校验诊断同口径；源名保留键 "" = 自身状态；无 IO 无 Provider 依赖（行 6 不变量构造保证）；代决记契约.md §3.7/§4 已代决⑦，Core 12 例锚定，c9e0b0e）；运行时关联：渲染期 ResolveBinding 取值（含 selector 注册表）已接三面（5d7efdf），真机观感待装机
- [x] 实现多个数据源组合（WidgetConfig.Sources 面级「源名→widgetId」增量可选字段（存量 widgets.json 零迁移可读、ConfigVersion 不动）；源名大小写敏感精确匹配、保留键 ""=自身状态、每面 16 源上限；声明非法整面弃用附加源（自身状态恒可用）+ 诊断进 LoadErrors（口径同组件树）；WidgetInstanceResolver.ResolveSources 从状态快照按 widgetId 查取构建运行时源映射（查不到的源缺席、绑定侧回空串）；组合在组件层聚合多个 WidgetId，WidgetState 单 ConnectionId 的 Provider 口径不动；代决记契约.md §3.8/§4 已代决⑧，Core 17 例 + Storage 1 例锚定，dde7b14）；运行时关联：附加源跨组件路由已接 tile/行/详情（StateLookup/RefreshSources，5d7efdf/7ef9fdb）+ 可导入样例闭环（9976081），运行时验收待装机
- [x] 支持不同组件消费同一份状态（ComponentSourceIndex：ConsumedWidgetIds = 实例所需 widgetId 全集（自身 Id 在前、附加源按声明顺序、同 widgetId 去重）；SourcesOf 反查 widgetId→消费实例集合；解析纯函数只读状态、按引用共享不复制（内容相等 + Assert.Same 锚定），索引按需重建不落缓存无 IO；代决记契约.md §3.9/§4 已代决⑨，Core 8 例锚定，65efa84）；运行时关联：宿主依赖路由按 ConsumedWidgetIds 分流 Update/RefreshSources（5d7efdf），真机待装机
- [x] 保证修改布局不触发无意义的数据请求（组件层纯函数边界：五个类型全静态类无缓存面，公共方法不接收任何运行时服务（Provider/执行器/Store/事件总线/Clock 等）；状态查取次数 = 自身 1 + Sources 数、与布局形状无关（1 节点 vs 200 层同 2 次），引擎拿到映射后零查取；反射签名守 + 查取计数守双锚定；代决记契约.md §3.10/§4 已代决⑩，Core 3 例锚定，fd9f2d1）；运行时关联：展示面保存走 ReloadWidgetRegistrations 零取数分流；受影响注册收口（d8ff4b6/332716f）

### Phase 4 HTML 小屏幕（6 条 = 6 勾，期次完成；2026-10-17 用户裁定「这个设计下吧」）

- [x] 实现 HTML/CSS 模板加载（2026-10-17 用户裁定「这个设计下吧」关闭留问：docs/design/phase4-html-ruling-2026-10-17.md 裁受控 HTML/CSS 子集+原生渲染、无 JS 无 WebView2，与 master-plan.md:769 反向记录和解；HtmlTemplateParser 静态纯函数（守已代决⑩）封闭词汇 12 元素映射既有渲染注册表类型键、style 块须先于元素、解析失败 null fail-closed；ComponentLayerInvariantTests 入列守界；测试面绿，b1893a1/f362d4c）
- [x] 实现受控数据桥接（{{state.f}}/{{payload.k}}/{{selector.n}} 插值脱糖为既有 ComponentBinding、绑定引擎零改动，白名单取引擎自身封闭集（ComponentBindingEngine 新增 StateFieldNames/SelectorNames 静态暴露）；文本内容混合插值拆 literal/绑定子节点序列、属性值仅整体单插值可绑、混合即解析错宁报错不猜；展开点在 WidgetInstanceResolver.Resolve 纯函数内（ExpandCarrier 后过既有防爆校验），tile/row/detail 消费面零改动；测试面绿，f362d4c/4c39c3d）
- [x] 实现 Action ID 桥接（data-action 只受静态字面（插值/空白解析错，远端数据不许挑动作）；App 侧 ComponentActionContext(Actions, Run) 窄桥按 actions.json+连接 DSL 全集查 ID 并过 ActionVisibility，缺席或不可见出诊断占位不渲染可点按钮，存在且可见走既有执行链 RequireConfirmation 照常；tile/row 面不承载动作（null 上下文同诊断）；DetailWindow 声明式详情面已接线；App 改动经 CI 编译门 run 39042835027 绿实证，bcd950e）
- [x] 实现模板错误回退、实例生命周期与安全限制（安全=封闭词汇表即白名单：script/iframe/img/a/on* 事件属性、实体与裸 & 符、字面色在语法上不可表达（不是过滤输入），未知元素/属性/专有属性误挂与越防爆半径（8 层/32 子/256 节点约束合成树）均解析错；回退=解析失败 null 回传统渲染，加载侧 SanitizeWidgetSurfaces 展开校验坏模板加载即报错进 LoadErrors 不等首渲；生命周期=既有 WidgetInstance 生命周期零新形态无定时器；HtmlTemplateParserTests 62 例 + Storage HtmlTemplateSanitizeTests 4 例全绿，f362d4c/4c39c3d）
- [x] 实现主题令牌与基本样式适配（style 块只收一类一规则 .class 形，prop 白名单 tone（info/ok/warn/bad）/pad/gap 各 sm/md/lg，字面色 #hex/rgb()/具名 CSS 颜色一律解析错即模板在语法上不可能硬编码颜色，浅深高对比自动适配；App 渲染侧换算：Tones 表映射 Severity 档位色（经 UiPalette 主题自适应）、sm/md/lg 映射 DesignTokens.Space4/8/12、attr 覆盖类令牌、卡片 pad 兜底 Space10；App 改动经 CI 编译门 run 39042835027 绿实证，f362d4c/bcd950e）
- [x] 验证数据变化时可以增量更新（模板解析一次每配置修订，数据到达=既有绑定对同一棵合成树按新快照重求值，HTML 文本不参与数据更新路径；Core 锚定事实：同实例两份状态快照绑定值不同而 Root 对象不变；样例常青门 DeclarativeSampleTests 保 htmltemplate 面可导入且 data-action 全在包内动作库；CI run 39043157904 绿实证，f362d4c/9f6dfd3）

### Phase 5 完善组件目录与用户体验（5 条）

- [x] 时间线、表格、图表与趋势组件（三诊口径落地：Core TrendSeries（payload 键 trend 不变文化数字串，非有限值/坏项弃全序列 fail-closed、归一化 [0,1] 全等回 0.5 平线、方向死区 10% 跨度）+ TimelineEntries（引擎记录旧→新翻展示序新在前、截 8 条）+ WidgetFeedback.DetailRows（表格行物料单源，跳过数值位提示键）；App 三工厂 ComponentTimeline/Table/Chart（sparkline 归一化单源 Core 不重算）+ DetailWindow 接线（详情表收编、TREND 区按 payload 画线、按 SourceWidgetId 过滤的最近事件区）+ 示例页三标本；ComponentDataTests 27 例并修 UiStringsTests 静态语言跨类竞争（三例收尾复位 + 两班同 [Collection] 串行，20 连跑零失败）；四测试项目 1274 全绿，CS 人工核待 CI 编译门，98f7a2d）
- [x] 更丰富的布局与条件显示（ComponentVisibility 纯函数条件判定收编「此时要不要画」：封闭语法 stale/offline/has:字段/字段 op 值（severity 枚举序、progress 数值、其余字符串等值），字段表复用 ComponentBindingEngine.ResolveStatePath 单源；缺省=可见、声明解析不出=隐藏；ComponentVisibilityTests 27 例 + 不变量守入列 + 四测试项目 1247 全绿；Column 词汇件收编 DetailWindow 主列调用面、示例页补 Column+When 状态驱动标本，CS 人工核待 CI 编译门，af22955）
- [x] 组件实例编辑、尺寸调整与桌面布局配置（写入面已设防：UpsertWidget 与加载同 fail-closed 口径，坏树/坏源不落盘 + 诊断进 LoadErrors，UpsertSanitizeTests 4 例 + Storage 37 全绿，016ba97；实例面第一片已落：设置页组件行「组件树」钮 → JSON 编辑器（载入/清空=回传统渲染/保存），ComponentTreeValidator 前置校验非法整次拒绝 + UpsertWidget 写入面二次防呆双闸，Components init-only 走克隆整体替换，删除行同步失效编辑目标防 upsert 复活；Storage 37 绿，CI 编译 b06efd2 实证（首跑 672af03 CS7036 Feedback 必填参，补 error: false 修复）；第二片已落：尺寸档（Config["sizeTier"] 三档词汇/系数沿 UiDensity 单源 compact 24/regular 32/spacious 40，只缩高不缩宽防裁字，fail-open 缺省恒等 G 锚不动；PinTile 实例级高 + 宿主行高/悬浮窗尺寸/读数列字宽预算四处联动）+ 桌面布局区（锚点/偏移/收起/显示器，写入口径同 PersistLayout 双写 widgets.json+pins.json，PinnedHostWindow.ReloadLayout 即时重排，偏移非有限数拒绝落盘）+ 设置页实例面重排（载入回填守卫、同值幂等），TileSizingTests +9 例；第三片已落：可视化树编辑器（TreeView 结构化投影——JSON 框是落盘真源、树只改工作副本再回写 JSON，保存仍走同一校验落盘链不分叉；类型词汇=ComponentSurfaceRenderer.KnownTypes 注册表单源、面键 tile/row/detail 契约三面；节点 Type/Id 重建+Props 换表、添加子节点/删除/添加面/从 JSON 刷新，未知类型插「（未注册）」首项防误改、Props 解析失败不落改 fail-closed）；四测试项目 1328 全绿，CI 编译门待验；GUI 本机启动验证：新构建启动/刷新无异常（锁屏挡设置窗视觉截屏，控件级断言待解锁/CI 场景）；de528f9+本提交）
- [x] 统一的空数据、离线与错误状态（WidgetFeedback 纯函数四态判定 错误&gt;离线&gt;空数据&gt;无；LastUpdate 时戳单源收编胶囊横幅/tile 值与工具提示/通知消息三处口径；DetailWindow 空数据明示修 value_text-only 画「区段签+空表」缺陷；WidgetFeedbackTests 19 例 + 四测试项目 1220 全绿；App 三窗接 ComponentFeedback 词汇件，CS 人工核待 CI 编译门，986ea52）
- [x] 主题、密度与可访问性完善（Core UiDensity 三档单源（redesign-plan §6.1 紧凑/标准/宽松齐套：常规恒等/紧凑 ×0.75/宽松 ×1.25 取整 AwayFromZero，缺省/未知回常规 fail-open）+ AppearanceConfig.Density（默认 regular）+ 往返断言，UiDensityTests 29 例 + Core 590 全绿，6241649/85b4ab9；App 三片：设置页「界面密度」下拉三档（构造期实时读口径同 progressStyle）+ DesignTokens.Spacing 收编口 + L3 五处/L2 三处/L0 tile 主行（PinTile 经 UiPalette.Density，悬浮窗同享）间距随档缩放，regular 恒等 G 系像素锚不动，f3c2bf9/6330885；场景 AA 五断言 CI 代验「可调可存可恢复可回切+宽松档」，run 38953986133 全绿，178efce/671c947；可访问性审计收口：设置页全控件 Header 即 UIA Name、chip 行 SetName、搜索框补显式 Name ac19cb3、tile/详情文本隐式 Name；CI 编译 b06efd2/85b4ab9/2bb3707 全绿实证）

### Phase 6 回归与真机验收（6 条）

- [ ] 验证刷新、状态更新与命令执行闭环（刷新/状态半已代验：本机硬杀重启两轮（taskkill /F 12:43/13:12）后 356 次刷新全 ok 零失败零未处理异常，日志 beacon-20261017.log；命令执行闭环测试面 Actions 27 绿（BuildStartInfo 三壳/HMAC 签名/环境注入），真机端到端执行半待装机）
- [ ] 验证多窗口订阅与取消订阅（测试面已代验：EventBus Subscribe/Unsubscribe/快照隔离 5 例 + bigmodel/ark/kimi 三窗 provider 测试全绿，真机日志见 GLM/Kimi 多窗值渲染（5h/周/月）；连接停用/组件删除的取消订阅重建链（SettingsWindow PinsChanged→ReloadTiles）装机半待验）
- [ ] 验证显示器切换、DPI、窗口拖动与重启恢复（重启恢复半真机已验：硬杀两轮后 pins.json 布局（monitor/anchor/offset/collapsed=true）完整恢复、面板按锚点重现；显示器切换需第二块屏、DPI 切换/拖动需解锁桌面+GUI 自动化——本会话锁屏挡视觉取证，装机半待验）
- [ ] 验证 HTML 安全边界与错误恢复（测试面已代验：封闭词汇表安全边界 script/iframe/img/a/on* 事件/实体/字面色/越防爆半径全解析错 + 错误回退解析 null 回传统渲染、加载侧坏模板进 LoadErrors（HtmlTemplateParserTests 62 例 + Storage HtmlTemplateSanitizeTests 4 例）+ 样例常青门导入闭环（run 39043157904 绿）；真机导入样例看诊断占位与回退观感装机半待验）
- [x] 验证缓存、断网、凭据错误与操作失败（四支路测试面代验：JsonCacheStoreTests 缓存往返 + OutageRecoveryIntegrationTests 断网停服恢复（PointConnectionAtServer 探针）+ DpapiSecretStoreTests 凭据 DPAPI 面 + Actions 失败路径（未知壳抛错/签名不匹配），四测试项目 1328 全绿，de528f9/75f4a85 批回归）
- [x] 验证现有 Provider、AI 额度显示与通知规则不回归（测试面+真机双证：Connections 662 全绿（全部 provider）+ NotificationEngineTests 阈值带/单次触发/冷却/禁用规则全绿；真机日志 GLM 26%/Kimi 多窗额度持续刷新 356 次零失败——尺寸档/树编辑器两批改动后无回归，de528f9/75f4a85）


