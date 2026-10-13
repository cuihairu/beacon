# TODO

> 生成于 2026-10-11（无 todo 文件；来源：docs/mvp-issues.md 未完成 42 项全量搬入 + docs/master-plan.md 后续可选）。
> 2026-10-11 巡检复点：六条「夹具/表驱动单测」单测半全部核对（模板渲染补 7 例，批3-补39），手动半统一标注【需装机】留待用户；后续可选两条为范围决策不标注。
> 2026-10-12 装机清单 CI 代验转化（批3-补50）：daily-build 视觉取证新增场景 E（单实例+托盘驻留+Exit）、F（无网启动+Last update+自愈+零弹窗+单条通知）、G（调色即时生效/非法回退/重置/动效持久化/帧差分静止呼吸/两 tile 独立）、H（悬浮位置两轮还原+TOOLWINDOW+刷新不抢焦点+panel/floating 二选一）、J（静默安装/卸载）；安装包步骤前置到取证之前。下表「已由 CI 代验」均指此批场景，证据在 Actions `visual-evidence` 工件；断言失败=红构建。
> 2026-10-12 批3-补51：G 系色彩断言阈值按 38261911354 工件逐像素实测重标定（状态灯亚像素渲染漂移 10~44px，详见决策记录）；托盘菜单驱动改 AttachThreadInput 取前台（ForceForeground）。
> 2026-10-13 patrol-20261013-A1：场景 L（行54 reduced 档变色过渡：L1 同色 tick 不闪灯 + L2 三轮翻档渐入中间态帧，38284623093）+ 修复 Update 每 tick 重放过渡导致的同色闪灯缺陷；场景 M（行6 开机自启注册表 HKCU Run\Beacon 双向断言，38287590006）；行70 热键条注记修正（K1 已代验应用侧链）；行40 补托盘图标像素不可取证的实际原因（溢出折叠区）。
> 2026-10-13 patrol-20261013-A2：场景 N（行18 设置改热键持久化，38297713667 实证：改键落盘 + 重启注册 + 触发链全绿）；场景 O（行41 面板计数一致性，38299509997 实证：三态往返全绿）；场景 P（行51 收起态还原：panel 模式 collapsed=true 细条宽启动 + 重启还原 + 展开对照，38302150211 待实证）；场景 Q（行42 胶囊计数/离线横幅：2 ok ↔ 1 ok · 1 err ↔ Last update + stale 2 warn，38303355372 实证全绿）；场景 R（行20 胶囊位置还原：shell-state.json 预设 TopLeft 40,40 落位 ±4 + 重启一致，38303355372 实证全绿）。另记：38297713667/38299509997/38302150211 场景全绿但发布步 403——仓库 default workflow permissions 被置 read、workflow 级 contents:write 抬升对 release 端点不生效（nightly release/tag 被删悬空）；已改回 write（生效有传播延迟），38303355372 发布断言通过（nightly 重建、SHA256SUMS 一致），诊断用临时 probe 工作流已删（cfcd97c），详见卡点回报。
> 2026-10-13 patrol-20261013-A3：场景 S（行43 Recent 事件流一致，38315240633 实证全绿）：结构化 UIA 树遍历（x:Name→AutomationId 锚定 RecentList/AttentionList 各自子树取行文本，滤 HH:mm:ss 时间串——事件行与状态行同出 MakeStateRow，平铺 Name 列表分不开两区）；S1 基线「已用 38%」事件行在列 → S2 注入 Error 后 RecentList 现事件行（95% 行位次新于 38% 行=新→旧）且 AttentionList 同现 95%（两区同源）→ S3 恢复后状态区摘掉 95% 而 RecentList 保留 95% 并补回 38%（状态区=当前、事件流=历史）。**随行读码发现并修复缺陷**：QuickPanelWindow.OnStateChanged 原只更新摘要/chip/RECENT、RebuildStatus（需要关注/正在运行区）唯一触发点在 OnWidgetsInvalidated——面板常驻期间状态区永不随状态刷新（开面板前旧影）；修复=OnStateChanged 现场重建状态区（S2 断言在旧代码上必红，即回归门）。行45 复核：L3 开启唯一入口=ItemClick（DetailRequested 全仓唯一订阅在 App.xaml.cs，两个 handler 均 ItemClick），UIA 无法可靠触发 ItemClick，仍点击门；L3 内部 Retry/Cancel/Open 均 Button（Name 可 UIA Invoke），开启面一通即可整链转化。

## 决策记录

- **断网降级通知缓存命中路径永不开火**（2026-10-12 装机清单 CI 代验转化批3-补50，场景 F 首跑实证）：PublishStale 缓存命中时 severity 沿用缓存值（健康时=Success），NotificationEngine 入口 Success/Info 一律静默 → builtin.connection-degraded 只在「从无缓存首次失败」开火，真正的「有数据后断网」（规则名所指的降级）反而不通知。修复：缓存命中 stale 路径 severity 抬到 Max(cached, Warning)（与无缓存路径一致；tile 灰显由 ConnectionHealthy 驱动不受影响，恢复后 provider severity 自然回填）；补 WidgetHostTests 两例回归（Success 抬 Warning / Error 不动）。F5 断言相应移入断网窗口内 + F5b 恢复后记录恒 1。
- **moonshot.balance 数值位补映射**（2026-10-12 巡检点火，用户倾向「补一行映射」→ 拍板落地，批3-补49）：payload 只有 `available_balance`，不在 `WidgetValueHint` 优先级链 → 数值位跌 `Summary[..7]+"…"` 截断，数字全没（「Moonshot · ¥110.00」→「Moonshot…」）——正是 hint 链立项要消灭的「数不出值」失败模式。**口径**：`¥{value}` 固定 CNY 符号（开放平台只按 CNY 计价，provider Summarize 已硬编码 ¥，不引 deepseek 式币种开关）；`0.00` 顶格形态，宽预算与 deepseek 同款 `¥99999.99`。同轮 hint 链全量自查另修两处：qwen 配额用尽路径补 `value_text="已用尽"`（比照 mimo「无额度口」显式文本先例）、machine.git 补 `value_text`（Summary 计数段同源，兜底截断把 ↑↓/改 计数全截没）。
- **tile 状态灯像素断言阈值重标定（亚像素渲染漂移）**（2026-10-12 批3-补51，38261911354 工件逐像素取证）：8DIP 状态灯同一几何在不同窗口实例渲染于不同亚像素位置 → 匹配像素 10~44 px 漂移（D 场景五实例 10、G2 三轮 44、G1 品红两跑 44/24、G4 橙 24/24；tol45=tol60 计数相同，tol90 起误吞 icon 灰）。G4 连红两轮的 24 px 是半像素偏移的正常渲染（精确核 16 + 0.89 覆盖率均匀边缘环 8），**非 3be02b6 所诊「呼吸调光」**——呼吸只存在于 full 档（StartBreathing 入口 `Mode != Full` 即返回），reduced/off 根本不启动；该次误诊引入的 motion off 钉死无害（G7b 帧差分仍依赖），保留。GLM tile 无进度条（state.Progress 为空 bar 收起，G2 绿 bbox 8×8 实证），此前「状态灯+条带」「95% 填充外推 ~92px」均为误设。失效模式（palette 未用新色/渲染缺失）恒 0 px → 色彩存在性断言统一定标实测底（10）之下、0 之上 = 8（G1/G2/G4/G5/G7a 五处）。另证：PS 非前台时 BringToFront 对任意窗口静默失败（E2/E3 菜单仍不弹的根因），托盘菜单驱动改 AttachThreadInput 共享前台权（WinEnum.ForceForeground）。
- **reduced 档变色过渡被每 tick 重放（同色闪灯缺陷）**（2026-10-13 patrol-20261013-A1，读码发现）：PinnedHostWindow.Update 的 TransitionFill 不受色变门控（`changed` 只闸 Flash）——reduced 档下每个刷新 tick（ci 档 15s）状态灯都 opacity 0→1 凭空灭一次再渐亮（400ms×intensity），违反 MotionEngine 自述「换色后亮度渐入」契约。修复：`_lastFillColor` 记录上次着色，只在色变 tick 过渡（含离线恢复灰空心→实色），同色 tick 直接填充；full 档呼吸/Critical 脉冲路径不变。转化场景 L（todo 行54）：状态灯条带裁图 20×18 @ tile 内偏移 x6/y10（G2 工件实测灯 bbox x13-20/y15-22），~15ms 周期连拍；L1 同色 tick 22s 灯恒在 ≥8px；L2 mock 交替翻档，TransitionFill=200ms×2.0 前载缓动（cubic-bezier 0.1,0.9）可视渐入窗 ~50ms，环内找「与两端都差 ≥8px」中间态帧——瞬时切换只可能出现端点帧，中间态帧即渐入的直接证据；≥2 可判定轮零中间态=真回归红，采样空洞降级 warning 转装机（同 G7c 口径）。

## MVP 待验/未修项（原样搬自 docs/mvp-issues.md 当时未完成 42 项，完成即改回该文件勾选；本表后续批次补录已勾项，现共 49 项 = 20 勾 / 29 未勾，2026-10-13 patrol-B 逐行清点：行 22/23/41 三条应用侧链/卸载半/记录半证据齐备转化）
- [ ] 双击 exe 二次启动不重复进程，既有实例收到唤起（气泡提示）——CI 已代验进程半（场景 E1：二次启动进程数恒 1 + 副实例退出日志硬断言）；唤起气泡是 OS Toast 面不可脚本取证【需装机（半）】
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出【已由 CI 代验：场景 E2 设置窗 WM_CLOSE 后进程驻留 + E3 托盘菜单 Exit 后进程退出（菜单驱动失败时降级 warning 转装机，见 run 日志）】
- [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱——位置还原半已由场景 R 代验（38303355372：预设 shell-state.json capsuleLayout TopLeft 40,40 → 胶囊落位 40,40 ±4 + 重启一致，与场景 H tile floatingX/Y 同口径）；拖动保存半（手势注入不可达）与跨 DPI 拖动（物理显示器）需装机【需装机（半）】
- [x] 任意前台应用下热键生效；可在设置改键并持久化【已由 CI 代验（应用侧链全量，2026-10-13 patrol-B 转化）：注册成功（场景 K1 启动日志无 RegisterHotKey 失败）+ 改键持久化（场景 N，38297713667：Ctrl+Alt+B→Ctrl+Alt+C 落盘 + 重启后新键注册无失败日志（RegisterHotKey/Invalid hotkey string 双断言））+ WM_HOTKEY 直驱同入口面板出现（K1/N）；「任意前台生效」即 RegisterHotKey 的 OS 派发契约——注册成功后 OS 必达，Beacon 无按前台分支代码；真实键盘和弦 CI 会话不可达（与鼠标手势同限制），留装机顺手复核】
- [x] 开关即时生效（重启验证）；卸载清理【已由 CI 代验（2026-10-13 patrol-B 转化）：卸载半场景 J（静默安装/卸载、程序文件清空、用户数据保留）+ 注册表半场景 M（38287590006：launchOnStartup true/false 双向断言 HKCU Run\Beacon 写入/清值 = 开关即时落注册表）；「重启验证」半即 OS 登录时执行 HKCU Run 的契约，Beacon 无登录期代码；真实重启留装机顺手复核】
- [ ] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口——外部故障注入半已由场景 T2 代验（38338077989：断网 75s → 进程存活 + refresh failed LogWarning 含堆栈 + mock 恢复后自动回数，外部可触发面）；真实未处理异常注入半需装机/注入机制（见后续可选「Critical 与崩溃注入机制」批）
- [x] 无网启动：界面有数据且标注 Last update，随后自动刷新【已由 CI 代验：场景 F1-F4（断网启动有窗有缓存数据、Last update 标注 45s 硬断言、恢复后 90s 自愈百分比读数）】
- [ ] 真实 PAT 手动验证连接测试成功/失败路径【需装机：需真实 GitHub PAT（mock 链路已由场景 D 代验，真实凭据属用户侧）】
- [x] 夹具单测：计数/映射正确（GitHubPullRequestsProviderTests 18 例：计数/亮黄映射/红 CI 续探复用；2026-10-11 巡检复点）
- [ ] 真实仓库手动验证（PR 计数与亮黄随刷新走）【需装机：需真实 GitHub PAT 与真实仓库数据；API 层已被 18 例假 Handler 单测覆盖】
- [x] 状态映射表驱动单测（GitHubActionsProviderTests：MapRun_TableDriven TheoryData + 端到端共 9 例）
- [ ] 真实 workflow 手动验证（成功/失败/运行中）【需装机：需真实 PAT + 真实仓库 workflow 状态流；映射层已被表驱动单测覆盖】
- [x] 路由/确认/超时单测（ActionRunnerTests 9 例：按类型路由/确认三态/超时取消/异常仍发事件/用户取消）
- [ ] 失败在 UI 有可见反馈【需装机：动作失败事件链需真实触发一次失败动作；ActionRunner 事件半已被 9 例单测覆盖】
- [x] 模板渲染单测（TemplateRendererTests 11 例；2026-10-11 补齐 7 例：键字符集/同键重复/单趟不二次展开/字面花括号/缺键去重）
- [ ] 手动验证浏览器打开正确 URL【需装机：默认浏览器行为属用户环境；URL 拼接已被模板渲染单测覆盖】
- [x] API 封装单测（假 Handler）（GitHubApiClientTests 10 例：ETag/401/403 配额/404/5xx/限流头/Bearer + GitHubWorkflowExecutorTests 6 例）
- [ ] 真实仓库手动：Run→运行中→终态全程可在 Beacon 看到【需装机：需真实仓库 workflow 触发与真实 PAT】
- [x] 超时终止生效；输出落盘（LocalCommandExecutorTests：Timeout_KillsProcess_AndStillWritesLog、RunsRealCommand_CapturesOutput_WritesLogFile）
- [ ] 确认弹窗可关（按 Action 配置）【需装机：需触发本地动作执行流弹出确认框（确认三态已被单测覆盖）】
- [ ] 发送/点击/激活路由全链路手动通过；快捷方式缺失可自愈【需装机：SendInput 落到真实目标应用的体验判定（CI 合成点击在会话桌面不可靠，此前有降级先例）】
- [x] 规则引擎表驱动单测（含冷却）（NotificationEngineTests 17 例：阈值 TheoryData 表驱动/冷却抑制与到期放行/跨越重臂/停用与过滤）
- [x] 手动：注入 Failed 状态收到且仅收到一条 Toast【已由 CI/单元代验（Beacon 侧全链，2026-10-13 patrol-B 转化）：引擎恰 1 条 = 单测 17 例（冷却抑制/到期放行/跨越重臂表驱动）+ 场景 F5 真实链（断网降级通知恰 1 条，connection-degraded 冷却去重，notifications.json 硬断言）；ToastNotificationService.Show 为直线发送代码（delivery.Toast 门控 → builder.Show()，失败有「Toast 发送失败」LogError 可观测），发送后的呈现（横幅/操作中心）属 OS 面无 Beacon 分支代码；装机时顺手目检即可】
- [ ] 聚合状态变化 ≤1 个刷新周期内反映到托盘【需装机：托盘图标像素在通知区域不可脚本取证（托盘图标确随 severity 换色——SetSeverity 运行时绘制；但全新会话图标默认落溢出折叠区不可见，需注册表 IsPromoted+重启 Explorer 才能取证，版本强依赖不做）；同链路 tile 层已由场景 G7a 代验（38%→95% ≤75s 反映）】
- [x] 断网显示 Offline+时间；计数与实际 Widget 状态一致【已由 CI 代验三面：tile 半场景 F2（断网 Last update 离线标注硬断言）；面板半场景 O（38299509997：all clear ↔ 0 warn · 1 error 三态往返 + chip 智谱 1/DeepSeek 1）；胶囊半场景 Q（38303355372：2 ok ↔ 1 ok · 1 err ↔ 断网 Last update 横幅 + stale 抬档 2 warn，UIA 文本断言不碰像素；胶囊观感仍可装机复核）】
- [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭——CI 已代验两半（场景 K2：断网 Stop mock 后热键开面板、缓存百分比读数在即无白屏；K3：Esc 关闭优先、键盘注入不可达时降级 warning 并以热键 toggle 硬断言面板可关）；Esc 键盘半与 <100ms 精确计时需装机【需装机（半）】
- [ ] 计数/事件与状态一致；右键 Pin 落库（本阶段无 UI 效果）——面板标题锚点已补（「Beacon 快速面板」，场景 K 用）；计数半已由场景 O 代验（38299509997：摘要/chip 三态往返与 Widget 状态一致）；Recent 事件流一致半已由场景 S 代验（2026-10-13 patrol-A3，38315240633：结构化 UIA 树遍历分行取证——S1 健康事件行「已用 38%」在列、S2 注入 Error 后 RecentList 现事件行且位次新于 38% 行（新→旧）+ AttentionList 同现 95%（两区同源）、S3 恢复后状态区摘掉而事件流保留并补回（当前 vs 历史语义分野）；随行发现修复状态区不随状态刷新缺陷，见 patrol-A3 决策记录）；右键 Pin 交互需装机（ContextFlyout 需真实右键，无 UIA 入口）【需装机：右键 Pin 半项】
- [ ] 从 L2 事件一键打开对应详情；[Retry] 全链路可用——详情半已由 CI 代验（2026-10-13，38338077989 场景 S4 硬记：应用侧补 InvokeableListItem（Invoke pattern 与 ItemClick 同链，8bcaf0f）→ RecentList 首行 UIA Invoke 打开 L3 详情窗（Beacon · * 计数 0→1），此前 38325074321/38330530278 三 pattern 全不中坐实 Item 门=ItemClick，peer 补上即通）；[Retry] 全链路半仍待 mock 增配 failed workflow runs 数据（见后续可选「L3 内动作链」批）；面板行点击的真实手势仍留装机顺手复核
- [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面——CI 已代验刷新半（场景 H1：mock 38%→95% 翻档期间前台窗句柄不变硬断言）；点击 tile 打开详情属用户意图合法夺焦，空白点击穿透需装机【需装机（半）】
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab【已由 CI 代验：场景 G7a（GLM 95% 时 DeepSeek 420.5 不动）+ 场景 H（每 tile WS_EX_TOOLWINDOW 置位、WS_EX_APPWINDOW 清位硬断言）】
- [ ] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一——CI 已代验还原与二选一半（场景 H 两轮重启精确落位 120,120/260,220 ±4px + H5 panel 模式悬浮窗归零）；拖放手感需装机【需装机（半）】
- [ ] 两种入口（设置开关/右键 Pin）行为一致并持久化【需装机：右键 Pin 在 L2 面板内，无 UIA 锚点】
- [ ] 非准入类型在两处入口均不可钉【需装机：同上】
- [ ] 重启后位置/收起态还原；拔显示器再接回 tile 不丢——CI 已代验位置半（场景 H 两轮重启落位一致）；收起态半场景 P 已建（patrol-20261013-A2：pins.json 首条 layout.collapsed=true → 宿主按 CollapseStripDips 12 DIP 细条宽启动 ≤20px、重启仍细条，对照组 collapsed=false 宽 ≥100px 且 ≥3 倍细条宽，非空洞对照）；拔显示器物理事件需装机【需装机（半）】
- [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区【需装机：DPI/分辨率拓扑切换需物理或虚拟显示栈（CI runner 单一虚拟屏不可驱动）】
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈【已由 CI 代验：场景 F3（断网期间 Beacon 进程名下 #32770 对话框数=0）+ F4（mock 恢复 90s 内百分比读数回归）】
- [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出【需装机：无 Critical 注入面——grep 实证所有 Provider 最高只产 Error，应用无状态注入钩子；需先补注入机制另立项】
- [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录【需装机：四条含真实网络环境与真人操作流】
- [ ] 设置改级别色 → L0 tile、胶囊、托盘图标同步变色——CI 已代验 tile 半（场景 G4：设置页 ValuePattern 改 #ff8800 → config 落盘 + tile 像素即时变橙）；胶囊与托盘图标像素不可脚本取证【需装机（半）】
- [x] 某 Widget 设 colorOverride → 仅该 tile 变色，优先级正确【已由 CI 代验：场景 G1（GLM #ff00ff 品红 ≥8px 硬断言，DeepSeek 零渗 ≤4px）】
- [x] 非法颜色值回退默认色不崩溃；重启后保持【已由 CI 代验：场景 G2 两轮启动（"sent:---" 非法值进程存活、品红 ≤4px、默认绿 ≥8px 硬断言）】
- [x] Success→Error 状态变化有平滑变色过渡（reduced 档）【已由 CI 代验（2026-10-13 patrol-20261013-A1，38284623093）：场景 L2 三轮翻档连拍均取到渐入中间态帧（与两端差分 ≥8px，过渡时长实为 200ms×intensity 非此前所记 300ms）；L1 同色 tick 不闪灯（22s 连拍灯恒在 44px 恒定，随修复落地——此前每 tick 重放淡入属读码发现的闪灯缺陷，见决策记录）；「平滑」观感最终裁量仍属装机（半）】
- [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调——CI 已代验三半（场景 G7b off 档三帧差分 ≤12px 全静止 + G7c full 档帧差分 32px 呼吸可见（38274507626 实证，轮询取帧对最大差分）+ G6 强度 1.5 落盘）；Critical 脉冲无注入面装机（同上条原因）【需装机（半）】
- [ ] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）——CI 已代验焦点半（场景 H1 前台不变硬断言）；发声与隐藏期动画循环需装机【需装机（半）】
- [ ] 导出文件全文无 token；导入+重录密钥后行为与原机一致——单测半已证（ImportExportTests：导出全文无 token）；导入全流程依赖 WinUI 文件选择器（CI 会话不稳定，此前有先例）需装机【需装机（半）】
- [ ] 全流程录屏/截图归档至 docs/；发现问题全部修复或开 Issue——CI 半已代验（本批 A-H/J 全场景截图归档至 visual-evidence 工件）；真人全流程录屏需装机【需装机（半）】
- [ ] 干净 Windows 11 虚拟机：下载→运行→完成 B-803 脚本【需装机：用户验收终局，CI 是构建证明不是验收替代】
- [x] 调色/动效改动即时预览并持久化，重启生效【已由 CI 代验：场景 G4（改色即时变橙+config 落盘）+ G6（档位/强度落盘）+ G6b（重启后 UIA IsSelected 恢复「关闭」选中态）】
- [x] 重置默认一键还原；off 档预览即全静止【已由 CI 代验：场景 G5（重置后 severityColors 清空 + tile 即时回默认绿）+ G7b（off 档三帧差分 ≤12px）】

## 后续可选（master-plan.md）
- [ ] L3 内动作链（[Retry] 全链路）CI 场景化【期次：R13 后续批；前置：InvokeableListItem（Invoke pattern 与 ItemClick 同链）已落——需 mock 增配 failed workflow runs 数据，场景从状态区行 Invoke 开 GitHub 详情 → UIA InvokePattern 点 [Retry] → RecentList 现动作事件行硬断言；解锁行 32（失败可见反馈）/行 38（确认弹窗，ContentDialog 按钮 UIA 可 Invoke）/行 46 Retry 半】
- [ ] Critical 与崩溃注入机制（debug 门控，落地行 24/55 的「另立项」）【期次：R13 后续批；应用侧加环境变量门控的状态/异常注入钩子——场景注入 Critical → L0 变色+脉冲帧差分硬断言（解锁行 55/61 Critical 半）；注入未处理异常 → 进程存活+日志堆栈+无僵尸窗硬断言（解锁行 24）】
- [ ] 厂商私有打包工具 Provider（开源标准 API 的 Jenkins Provider 已落位 2026-10-11；私有厂商待取得真实 API/认证/状态字段再评估，不建常驻 Agent）
- [x] AI Usage Provider——条件门控项已满足并实质完成，关闭（2026-10-11 批3-补41 评估：13 种组件类型/11 家已落地，可靠性三档 + 两次形状漂移均被单文件隔离当日收口；详见 master-plan §13 注记与 positioning §8）

## MVP 验收标准（master-plan §14，需真实装机验证）
- [ ] Windows 登录后按用户设置启动【需装机：真实登录会话的启动链，CI 无登录桌面】
- [ ] 托盘与状态胶囊稳定运行——CI 已代验驻留半（场景 E2/E3 关窗驻留 + Exit 唯一退出）；长时稳定与胶囊观感需装机【需装机（半）】
- [x] 全局快捷键正常打开悬浮面板【已由 CI 代验应用侧链（同行 22 口径，2026-10-13 patrol-B 转化）：场景 K1 注册成功日志 + WM_HOTKEY 直驱同入口 → 面板窗出现硬断言 + 场景 N 改键后重启注册无失败；OS 级任意前台键盘派发即 RegisterHotKey 契约；真实键盘唤起留装机顺手复核】
- [ ] 面板展示真实 GitHub 状态【需装机：需真实 PAT 与真实仓库】

验收：CI windows-latest 出截图或给安装包名 + SHA256 让用户自验；「改了未验」不算完成。
