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

## 决策记录

- **SignPath Foundation 申请被拒 → 暂不推进签名，保持现有链路待命**（2026-10-14 用户拍板选 A）：SignPath 官方回信说明 Foundation 需要项目已具备公开可见度（stars / forks / 贡献者、外部文章、社区讨论、机构背书），本仓未达标，非质量问题，欢迎有影响力后再申请。**处置**：`daily-build.yml` 的签名段与 `SIGNPATH_API_TOKEN` / `SIGNPATH_ORGANIZATION_ID` 两个 secret **原样保留不删**（链路已搭好，只缺有效凭据），仅在版本 tag 通道签名、nightly 永不签的口径不变；等有对外正式发版需求时再决定是否走付费订阅（docs.signpath.io/change-subscription）或届时重新申请。**连带作废**：此前待用户处理的三件（Submitters 加人、GitHub App 安装授权、Artifact Configuration slug 回传）随之失效，不再追踪。未签名的影响仅限 Windows exe 触发 SmartScreen「未知发布者」警告，不影响构建与其它平台产物。

- **tile/胶囊底部裁切 = 外框/客户区尺寸混用（AppWindow.Resize 直喂客户区需求）**（2026-10-14 用户缺陷单「底部进度条显示不全（还是一样的问题）」，run 38488861749 工件逐像素取证）：AppWindow.Resize 设的是窗口外框，PinnedHostWindow 面板/细条、FloatingTileWindow、CapsuleWindow、QuickPanelWindow 四处全把客户区需求尺寸（行×32 DIP / 内容实测）直接喂 Resize——无边框 OverlappedPresenter 仍保留 ~6px 非客户区 resize 边框，每窗底部 ~6px 落进不可见区。缺陷形态：tile 设计 32px、实测客户区 26px，底部 4 DIP 进度条整条不可见（A 场景 GLM tile 26px 无条带、D 场景 5 悬浮窗全 26px、H 场景面板末行 tile 26px 无条而首行完整 32px 带条——「首行完整」正坐实窗口底部裁切而非 bar 渲染缺失）；胶囊（同为 32 DIP 高）与快捷面板同型底部裁字。同型先例：DetailWindow c80c244（2026-10-11 装机「没有修复」根因）。修复：ChromeSizePx（GetWindowRect−GetClientRect，取不到兜 0）口径收编 NativeMethods 供四窗共用（DetailWindow 私有副本同收），Resize 一律补回差值；CapsuleWindow._sizePx 改记外框尺寸（RestorePosition/SavePosition 锚点数学同口径，save/restore 往返不漂移）；QuickPanel 定位按外框口径保 24/140 DIP 可见边距。CI 断言同步去 vacuous：Count-BarPixels 旧实现裁外框、底部 6 行落在窗框外桌面区，「465/507 像素」全是桌面边缘（空转通过实证）——改锚客户区（Assert-ClientHeight 量高用 ClientRectOf、条带裁图用 ClientScreenRectOf：GetClientRect 返回窗内坐标，裁屏图必须经 ClientToScreen 换算屏坐标，4ae613d 补齐；期望高按窗 DPI 缩放，口径与 DpiLayoutMath.ToPx 同取整）+ 新增 Assert-ClientHeight 硬断言（客户区高=设计值 ±1：A/D 逐悬浮窗 32 DIP、H5 面板行高×行数整数倍）；亚像素阈值 ±1 口径同行 21 状态灯决策。GLM tile 空态收起（state.Progress 为空 bar 收起）与有值路径同一容器/同一行高预算，收起只藏 bar 不改窗高——本缺陷在窗口层不在 tile 层，两态尺寸预算一致（行 21「GLM tile 无进度条」实为空态收起+本裁切叠加的观测）。SettingsWindow 构造期 Resize 同型未在本单修（默认标题栏样式、症状=设置窗记忆尺寸逐轮矮一截，留后续行）。
- **断网降级通知缓存命中路径永不开火**（2026-10-12 装机清单 CI 代验转化批3-补50，场景 F 首跑实证）：PublishStale 缓存命中时 severity 沿用缓存值（健康时=Success），NotificationEngine 入口 Success/Info 一律静默 → builtin.connection-degraded 只在「从无缓存首次失败」开火，真正的「有数据后断网」（规则名所指的降级）反而不通知。修复：缓存命中 stale 路径 severity 抬到 Max(cached, Warning)（与无缓存路径一致；tile 灰显由 ConnectionHealthy 驱动不受影响，恢复后 provider severity 自然回填）；补 WidgetHostTests 两例回归（Success 抬 Warning / Error 不动）。F5 断言相应移入断网窗口内 + F5b 恢复后记录恒 1。
- **moonshot.balance 数值位补映射**（2026-10-12 巡检点火，用户倾向「补一行映射」→ 拍板落地，批3-补49）：payload 只有 `available_balance`，不在 `WidgetValueHint` 优先级链 → 数值位跌 `Summary[..7]+"…"` 截断，数字全没（「Moonshot · ¥110.00」→「Moonshot…」）——正是 hint 链立项要消灭的「数不出值」失败模式。**口径**：`¥{value}` 固定 CNY 符号（开放平台只按 CNY 计价，provider Summarize 已硬编码 ¥，不引 deepseek 式币种开关）；`0.00` 顶格形态，宽预算与 deepseek 同款 `¥99999.99`。同轮 hint 链全量自查另修两处：qwen 配额用尽路径补 `value_text="已用尽"`（比照 mimo「无额度口」显式文本先例）、machine.git 补 `value_text`（Summary 计数段同源，兜底截断把 ↑↓/改 计数全截没）。
- **tile 状态灯像素断言阈值重标定（亚像素渲染漂移）**（2026-10-12 批3-补51，38261911354 工件逐像素取证）：8DIP 状态灯同一几何在不同窗口实例渲染于不同亚像素位置 → 匹配像素 10~44 px 漂移（D 场景五实例 10、G2 三轮 44、G1 品红两跑 44/24、G4 橙 24/24；tol45=tol60 计数相同，tol90 起误吞 icon 灰）。G4 连红两轮的 24 px 是半像素偏移的正常渲染（精确核 16 + 0.89 覆盖率均匀边缘环 8），**非 3be02b6 所诊「呼吸调光」**——呼吸只存在于 full 档（StartBreathing 入口 `Mode != Full` 即返回），reduced/off 根本不启动；该次误诊引入的 motion off 钉死无害（G7b 帧差分仍依赖），保留。GLM tile 无进度条（state.Progress 为空 bar 收起，G2 绿 bbox 8×8 实证），此前「状态灯+条带」「95% 填充外推 ~92px」均为误设。失效模式（palette 未用新色/渲染缺失）恒 0 px → 色彩存在性断言统一定标实测底（10）之下、0 之上 = 8（G1/G2/G4/G5/G7a 五处）。另证：PS 非前台时 BringToFront 对任意窗口静默失败（E2/E3 菜单仍不弹的根因），托盘菜单驱动改 AttachThreadInput 共享前台权（WinEnum.ForceForeground）。
- **reduced 档变色过渡被每 tick 重放（同色闪灯缺陷）**（2026-10-13 patrol-20261013-A1，读码发现）：PinnedHostWindow.Update 的 TransitionFill 不受色变门控（`changed` 只闸 Flash）——reduced 档下每个刷新 tick（ci 档 15s）状态灯都 opacity 0→1 凭空灭一次再渐亮（400ms×intensity），违反 MotionEngine 自述「换色后亮度渐入」契约。修复：`_lastFillColor` 记录上次着色，只在色变 tick 过渡（含离线恢复灰空心→实色），同色 tick 直接填充；full 档呼吸/Critical 脉冲路径不变。转化场景 L（todo 行54）：状态灯条带裁图 20×18 @ tile 内偏移 x6/y10（G2 工件实测灯 bbox x13-20/y15-22），~15ms 周期连拍；L1 同色 tick 22s 灯恒在 ≥8px；L2 mock 交替翻档，TransitionFill=200ms×2.0 前载缓动（cubic-bezier 0.1,0.9）可视渐入窗 ~50ms，环内找「与两端都差 ≥8px」中间态帧——瞬时切换只可能出现端点帧，中间态帧即渐入的直接证据；≥2 可判定轮零中间态=真回归红，采样空洞降级 warning 转装机（同 G7c 口径）。

## MVP 待验/未修项（50 项 = 25 勾 / 25 未勾；原样搬自 docs/mvp-issues.md，未完成项即装机清单）
- [ ] 双击 exe 二次启动不重复进程，既有实例收到唤起气泡提示（CI 已代验进程半：场景 E1 二次启动进程数恒 1；唤起气泡属 OS Toast 面，需装机半）
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出（场景 E2 关窗进程驻留 + E3 托盘菜单 Exit 退出已代验）
- [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱（位置还原半已代验：场景 R 预设坐标落位 ±4 且重启一致；手势注入与跨 DPI 拓扑需装机半）
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
- [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭（CI 已代验缓存无白屏 + 面板可关：场景 K2/K3；Esc 键盘注入与 100ms 精确计时需装机半）
- [ ] 计数/事件与状态一致；右键 Pin 落库（计数半场景 O + Recent 事件流半场景 S 已代验；右键 Pin 无 UIA 入口，需装机）
- [x] 从 L2 事件一键打开对应详情；[Retry] 全链路可用（场景 S4 UIA Invoke 开详情 + U1-U5 Retry 全链已代验）
- [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面（刷新不打断已代验：场景 H1 前台句柄不变；空白点击穿透需装机半）
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab（场景 G7a 两 tile 独立 + H WS_EX_TOOLWINDOW 置位已代验）
- [ ] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一（还原与二选一已代验：场景 H 两轮重启精确落位 + panel 模式悬浮窗归零；拖放手感需装机半）
- [ ] 两种入口（设置开关/右键 Pin）行为一致并持久化（右键 Pin 在 L2 面板内无 UIA 锚点，需装机）
- [ ] 非准入类型在两处入口均不可钉（需真实右键交互，CI 不可达）
- [ ] 重启后位置/收起态还原；拔显示器再接回 tile 不丢（位置半场景 H + 收起态半场景 P 已代验；拔显示器物理事件需装机半）
- [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区（需物理或虚拟显示栈，CI 单屏不可驱动）
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈（场景 F3 对话框数 0 + F4 恢复 90s 内读数回归已代验）
- [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出（L0 变色+脉冲半已代验：场景 V1/V2，38459944253；「无打断」观感与 Toast 照常发出的通知引擎链需装机半）
- [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录（四条含真实网络环境与真人操作流，需装机）
- [ ] 设置改级别色后 L0 tile、胶囊、托盘图标同步变色（tile 半已代验：场景 G4 改色即时生效；胶囊与托盘像素不可脚本取证，需装机半）
- [x] 某 Widget 设 colorOverride 仅该 tile 变色，优先级正确（场景 G1 已代验：品红 ≥8px + DeepSeek 零渗 ≤4px）
- [x] 非法颜色值回退默认色不崩溃；重启后保持（场景 G2 两轮启动已代验：进程存活 + 非法值零渗 + 默认绿 ≥8px）
- [x] Success 到 Error 状态变化有平滑变色过渡（reduced 档）（场景 L1/L2 已代验：38284623093 三轮渐入中间态帧 + 同色 tick 不闪灯）
- [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调（四半已代验：G7b off 静止 + G7c full 呼吸 + G6 强度落盘 + V2 脉冲帧差分；观感与强度裁量需装机半）
- [ ] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）（焦点半已代验：场景 H1 前台不变；发声与隐藏期循环需装机半）
- [ ] 导出文件全文无 token；导入+重录密钥后行为与原机一致（导出无 token 已单测覆盖；导入依赖 WinUI 文件选择器，CI 会话不稳，需装机半）
- [ ] 全流程录屏/截图归档至 docs/；发现问题全部修复或开 Issue（截图已归档至 visual-evidence 工件；真人全流程录屏需装机半）
- [ ] 干净 Windows 11 虚拟机：下载后运行并完成 B-803 脚本（用户验收终局；CI 是构建证明不是验收替代）
- [x] 调色/动效改动即时预览并持久化，重启生效（场景 G4 改色即时 + G6 档位落盘 + G6b 重启 UIA 选中态恢复已代验）
- [x] 重置默认一键还原；off 档预览即全静止（场景 G5 重置回默认 + G7b off 三帧差分 ≤12px 已代验）
- [x] tile 底部进度条显示不全（2026-10-14 缺陷单「还是一样的问题」）（02a5de1 修复，38500538817 复证全绿：client 32px/frame 38px chrome 6px + 条带 620/676 真像素，见决策记录同名条）

## 后续可选（master-plan.md）
- [x] L3 内动作链（[Retry] 全链路）CI 场景化（38434164526 实证 U1-U5 全绿；解锁行 32/38/46）
- [x] Critical 与崩溃注入机制（debug 门控）（DebugInjection 8 例单测 + DispatcherQueueGuard 兜底；38494525200 实证 V1/V2/V3 全绿，V3 红出真实发现见决策记录）
- [ ] 厂商私有打包工具 Provider（需真实厂商 API 与认证字段；开源标准 API 的 Jenkins Provider 已落位 2026-10-11）
- [x] SettingsWindow 构造期 Resize 同型外框/客户区混用（f4b0eb1 修复：记忆尺寸存客户区 DIP 直喂外框，每次开窗矮一标题栏）
- [x] AI Usage Provider（11 家已落地，可靠性三档；条件门控满足后关闭，见 master-plan §13）

## MVP 验收标准（master-plan §14，需真实装机验证）
- [ ] Windows 登录后按用户设置启动（需真实登录会话启动链，CI 无登录桌面）
- [ ] 托盘与状态胶囊稳定运行（驻留半已代验：场景 E2/E3 关窗驻留 + Exit 唯一退出；长时稳定与胶囊观感需装机半）
- [x] 全局快捷键正常打开悬浮面板（场景 K1 注册 + N 改键重启注册无失败 + WM_HOTKEY 直驱面板硬断言已代验）
- [ ] 面板展示真实 GitHub 状态（需真实 PAT 与真实仓库）

验收：CI windows-latest 出截图或给安装包名 + SHA256 让用户自验；「改了未验」不算完成。

- [ ] 事件看板：默认全部 + 可点击过滤（来源：用户 2026-10-14）
  - 要求：看板**默认显示全部事件**；同时**可以点击对应项只看该类过滤后的结果**
  - 验收：默认态是"全部"，点击某类后只显示该类，再点可取消回到全部；过滤状态在界面上可见（当前在看哪一类不能是隐式的）
- [ ] 看板与多语言：缺中文、设置里无切换选项（来源：用户 2026-10-14）
  - 现象：**看板没有中文**；且**多语言设置中没有切换选项**
  - 验收：看板文案纳入既有 i18n 体系（先核本仓 i18n 机制怎么做的，**不另起一套**）；**设置页要提供语言切换项**，切换后看板同步生效

- [ ] 悬浮框边缘坍缩：难用且无关闭开关（来源：用户 2026-10-14）
  - 现象：**悬浮框靠近桌面边缘就坍缩**，**没有关闭该行为的开关**；**坍缩之后很难再点开**
  - 风险定性：这是**可用性缺陷**——功能默认开启且无法关闭，导致用户失去对悬浮框的控制
  - 要求：**加开关让用户可关闭边缘坍缩**（默认值由你判断并说明理由）；同时**修坍缩后难以展开的问题**——坍缩态必须有可靠的展开入口（点击/悬停/快捷键任一，且不要求像素级精度）
  - 验收：设置中能看到并切换该开关；关闭后悬浮框靠近边缘**不再坍缩**；坍缩态下**能稳定展开**（给出可复现的操作步骤）

- [ ] 语义澄清/缺陷：菜单中「Open Beacon」与「Notifications」效果相同（来源：用户 2026-10-14）
  - 现象：托盘/菜单里 **`Open Beacon` 与 `Notifications` 两个入口效果一样**，用户表示**很困惑**
  - 要求：
    1. **先查清当初的设计意图**（读代码与文档，给 file:line 与出处），说明两者**本应**是什么关系
    2. 既然效果相同，**必须二选一处理**：要么让两者**语义分化**（打开主界面 vs 打开通知面板），要么**合并/去掉其中一个**——**不允许保留两个同义入口**
    3. 若本仓已有 i18n，两个入口的文案都要能被翻译（与上面那条多语言需求一并处理）
  - 验收：给出设计意图的出处；处理方案与理由；真实操作验证两个入口**不再等价**（或只剩一个）

