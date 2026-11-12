# TODO

> 需求台账：一行一条需求；已勾项保留一行证据（commit hash + 测试数/CI run）；巡检流水与根因叙事已剥离，全文见 git 历史与本仓 docs/review/、docs/design/ 各记录。
> 验收口径：CI windows-latest 出截图或给安装包名 + SHA256 让用户自验；「改了未验」不算完成。

## 决策记录

- SignPath Foundation 申请被拒 → 暂不推进签名，workflow 签名段与两个 secret 原样保留待命；仅 `v*` tag 通道签名、nightly 永不签（2026-10-14 用户拍板）。
- 窗口底部裁切 = 外框/客户区尺寸混用：Resize 一律补 ChromeSizePx 差值，CI 断言改锚客户区（02a5de1，run 38500538817 复证绿）。
- 断网降级通知缓存命中路径永不开火：stale 路径 severity 抬 Max(cached, Warning)，WidgetHostTests 补两例回归（批3-补50）。
- moonshot.balance 数值位补映射：`¥{value}` 固定 CNY；同轮 qwen 用尽补「已用尽」、machine.git 补 value_text（批3-补49）。
- 状态灯像素断言阈值：亚像素漂移 10~44px 属正常渲染（非呼吸调光），失效模式 0 与实测底 10 之间定标 8px（38261911354 逐像素实测）。
- reduced 档同色闪灯缺陷：TransitionFill 只在色变 tick 过渡，同色 tick 直接填充（patrol-20261013-A1 读码发现）。
- 覆盖率巡检收口：全仓 7504/7636 = 98.27%（coverlet cobertura 四测试项目合并口径，Beacon.App 不在任何测试工程引用内故不计），达标 98%；本批 bb389ea/e7dfc1c/a06c345/c4b2822/d9cefc0，并行批 2992bc0/5d4c315/b9a45fe/3924840；残余未覆盖 = Windows 专属 P/Invoke（DpapiSecretStore 39 行、MachineLocalProvider 9 行）与时序竞态/防御分支（RefreshScheduler、WidgetHost 晚到闸门等 130 行总计）（2026-11-08）。
- 覆盖率 98% 门槛复收口（2026-11-09 用户令，codecov 基线 95.31%）：本地 coverlet cobertura 四测试项目合并（逐行 hits 去重 + 跨报告 branch 并集）7647/7712 = 99.16%，codecov/project+patch 双 success（a209e87 触发 CI run 42600394019，≥98% 门槛确认过）；本批 e3e8620/ca652bf/8b383db（补 ~51 行关键路径：Json*Store 归档损坏回退、RefreshScheduler 重注册/取消/竞态、ActionRunner 连接富化、DisplayTemplates 记录面、Git 工作区真实仓库 ahead/behind、SvgPath 带符号指数、IslandMotion 牛顿→二分回退、GitHub 执行器动作类型等，四测试工程 1268+1011+54+54 全绿）；残余未覆盖 65 行/13 文件 = 平台锁死 49（DpapiSecretStore 39 + MachineLocalProvider 9 + MachineNetProvider 1，Linux 不可跑）与死码/测量伪影 16（RefreshScheduler 227/230/232 竞态窗不可确定性复现、DisplayTemplates 323-325 ArgsEqual 非空双字典不可达、LocalCommandExecutor 140/143 TryKill 竞态、SvgPathParser 289 TryParse 溢出臂（.NET Core 3.0+ 溢出返回 +∞ 非 false）、ProviderDsl 93 非映射列表项（YamlLite 解析期全拒）、Qwen 288/Kimi 472 守卫重复、JsonConfigurationStore 128/JsonNotificationRecordStore 55/HTML 401 不可达、HTML 185 while(true) 无 sequence point）；生产代码零改动、无凑数测试（2026-11-09）。
- 覆盖率巡检复验（2026-11-11 巡检单 cov2，HEAD 4b4c655 工作树干净）：四测试工程 2387 全绿（1268+1011+54+54），coverlet cobertura 四报告合并（同口径）7647/7712 = 99.16% 与上轮收口逐位一致零回归；残余未覆盖 65 行/13 文件行号逐一吻合（平台锁死 49 + 死码/伪影 16，无新增），补测试不适用——可达路径已在上轮补尽，剩余全为平台锁死与不可达伪影，硬凑违反「禁凑数」；分模块 line：Core 99.68 / Connections 99.70 / Actions 99.06 / Storage 87.72（缺口全为 DpapiSecretStore DPAPI 平台锁死）；build 0 error（16 个测试文件既有 CS86xx 可空告警为存量非本轮引入）；生产代码零改动（2026-11-11）。
- 审核修复批收口（2026-11-11 审计报告 audit-batch4-6repos beacon 段，任务单 beacon-audit-fix，2026-11-12 完成）：P0×3 = ActionRunner 重试闸扩 TimedOut/Cancelled 返回值（ActionRunner.cs:116，0ad8233，回归 2 例对旧逻辑负验）、JsonConfigurationStore.ReplaceAll 落盘前 Sanitize（0a20085）、布局落盘批量 UpsertWidgets 单次写（3277d3d，IConfigurationStore 新接口 + PinnedHostWindow.cs:267/SettingsWindow.ComponentStyles.cs 同步收口）；P1×12 每项单 commit = ShellStateStore.SaveCapsuleLayout 读改写保岛/看板段（6d7289d）、GitHubApiClient 403 分流读本响应头（451e89a）、ClaudeUsageProvider 扫描逐文件逐行可取消（8ed81e3）、NotificationEngine 出口失败撤销门槛记账（1c0ab0f）、HeraldFeedProvider 已见基线 4096 上限裁剪（6e1f836）、WidgetHost skip 三值化全组跳过也退避（f8fad1b）、LocalCommandExecutor powershell 双引号 fail-loud 闸（50ac341）、GitWorkspaceProvider ArgumentList 直传+双管道并发读（00e338b）、RefreshScheduler 等待后重取组快照（117caea）、AppConfigFile Save 留 .bak+损坏 corrupt 留档恢复（ce684b0+9694aef 根目录注入测试隔离）、QuickPanelWindow 段重建按调度拍合并（c7c1e97）、BeaconRuntime.DslActions 2s TTL（103bf5d）；MachineNetProvider 审计自降 P2 未动；测试 Core 1274 / Connections 1017 / Actions 55 / Storage 57 全绿，CI run 43119296520/43120562688/43125140068 三段全绿（2026-11-12）。

## MVP 待验/未修项（50 项 = 42 勾 / 8 未勾；未完成项即装机清单）

- [x] 双击 exe 二次启动不重复进程，既有实例收到唤起气泡提示（场景 E1 CI 代验 + 2026-10-17 真机双半实证，TrayIconService.cs:99-110）
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出（场景 E2/E3 已代验）
- [ ] 跨 DPI 拖动无错乱：需真实双 DPI 显示器，当前环境缺（位置/普通拖动已有场景 R/R3 证据 38750239155）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 任意前台应用下热键生效；可在设置改键并持久化（场景 K1/N + WM_HOTKEY 直驱已代验）
- [x] 开关即时生效（重启验证）；卸载清理（场景 J 静默安装卸载 + M HKCU Run 双向断言已代验）
- [x] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口（场景 T2 + V3 崩溃注入已代验，38494525200）
- [x] 无网启动：界面有数据且标注 Last update，随后自动刷新（场景 F1-F4 已代验）
- [x] 真实 PAT 手动验证连接测试成功/失败路径（本地 HttpListener 桩走完整 HTTP 栈替代，GitHubConnectionHttpStubTests 2 例，commit 34c91b1）
- [x] 夹具单测：计数/映射正确（GitHubPullRequestsProviderTests 18 例）
- [x] 真实仓库手动验证 PR 计数与亮黄随刷新走（公共 API 匿名验替代，GitHubPublicApiLiveTests.Item2，BEACON_LIVE_API=1 opt-in，commit ce8a6ca）
- [x] 状态映射表驱动单测（GitHubActionsProviderTests 9 例）
- [x] 真实 workflow 手动验证（公共 API 匿名验替代，GitHubPublicApiLiveTests.Item3，commit ce8a6ca）
- [x] 路由/确认/超时单测（ActionRunnerTests 9 例）
- [x] 失败在 UI 有可见反馈（场景 U5 已代验：mock 500 后 L3 失败反馈文本硬断言）
- [x] 模板渲染单测（TemplateRendererTests 11 例）
- [x] URL 拼接后端到端打开验证：拼接已有 TemplateRendererTests 覆盖；真机半 2026-11-06 在 .5 实测——看板时间线行 → L3 详情 → 点「Open」→ 默认 Edge 打开 `open.bigmodel.cn`（窗口标题「智谱丨BigModel 平台」，标签页 13→14），证据 `docs/acceptance-evidence/url-detail-window.png`、`url-browser-opened.png`、`behaviour-verify.txt`（B1-url PASS；构建 2026.11.6+56e4f27…）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] API 封装单测（GitHubApiClientTests 10 例 + GitHubWorkflowExecutorTests 6 例）
- [x] 真实仓库手动验证运行中到终态全程可见（可造数据桩驱动 provider 渲染翻转替代，GitHubPublicApiLiveTests.Item4，commit ce8a6ca）
- [x] 超时终止生效；输出落盘（LocalCommandExecutorTests 2 例）
- [x] 确认弹窗可关（场景 U3/U4 已代验）
- [ ] Toast 发送、点击、激活路由实机验证：发送+呈现半 2026-11-06 openclaw 实测 PASS——二次启动→Balloon dispatched（beacon-20261106.log 13:12:47）→横幅实拍 `openclaw-20261106/banner-t2.png`「Beacon 已在运行。」+通知中心两条 Beacon 通知 `notification-center.png`；AUMID 自愈已有场景 F5t 证据 38494525200；本机补强批（GLM Failed 态 OS 横幅 PRESENT 12:04→MISS 12:05 相邻帧 + 通知中心两条在栏 + 详情窗 Open→浏览器落 bigmodel.cn/usage，裁剪版归档 `bm2-toast-tray/`，台账 2026-11-08-bm2-toast-tray.md）；**点击→激活路由本轮复现：Failed+无效 detailUrl 状态已本地 mock 稳定触发并投递 OS（无 403 依赖），imac 系统侧压制横幅显示（直发注册 AUMID toast 亦无横幅）→点击步 BLOCKED，原因与所需动作见 docs/acceptance-evidence/2026-11-08-toast-activation.md（真人点击在能显示横幅的机器上验，装机清单面 12）**
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 规则引擎表驱动单测含冷却（NotificationEngineTests 17 例）
- [x] 注入 Failed 状态收到且仅收到一条 Toast（NotificationEngineTests 17 例 + 场景 F5 恰 1 条硬断言）
- [x] 托盘图标颜色/状态像素与刷新周期实机验证：2026-11-06 openclaw 实测——同位两态像素差分 Success 绿 RGB(62,185,80)↔Warning 琥珀 RGB(209,152,33)（其余托盘图标逐像素不变，`openclaw-20261106/tray-green.png` vs `tray-e2.png`）；刷新周期 30s 档实测（state-logtail.txt 两次刷新间隔+Last update 随刷新走）；图标在托盘可见区直接显示不进溢出；tile 更新已有场景 G7a 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 断网显示 Offline+时间；计数与实际 Widget 状态一致（场景 F2/O/Q 三面已代验）
- [x] 打开 <100ms（缓存优先无网络等待）；键盘可完整操作关闭（BeaconRuntime.cs:27,113 + QuickPanelWindow.cs:116-118，run 38743014862）
- [x] 计数/事件与状态一致；右键 Pin 落库（场景 O/S/X1 已代验，run 38590456994）
- [x] 从 L2 事件一键打开对应详情；[Retry] 全链路可用（场景 S4 + U1-U5 已代验）
- [ ] 跨进程点击穿透/焦点行为实测：焦点面 2026-11-06 openclaw 真实点击实测 PASS——点胶囊唤起看板前台不变（fg 保持 wsl.exe，`capsule-test.txt`+`board-opened.png`）、ESC 关面板（RFC §6.2.7 ②真机复证）；结构面 PASS（无 WS_EX_APPWINDOW、状态翻转前台不变，.5 批）；**仍 OPEN：tile 收起态点击穿透到下层窗口场景**（行 69 已裁定不自加整窗穿透）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab（场景 G7a + H WS_EX_TOOLWINDOW 已代验）
- [x] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一（悬浮形态为主 2026-10-18 裁定；场景 H/H6 已代验，38750239155）
- [x] 两种入口（设置开关/右键 Pin）行为一致并持久化（场景 X1-X3 已代验，run 38590456994）
- [x] 非准入类型在两处入口均不可钉（场景 Y1-Y4 已代验，run 38608625892）
- [x] 重启后位置/收起态还原；拔显示器再接回 tile 不丢（PinLayoutMathTests 回归锁 commit b08cbf7；场景 H/P 已代验；拔显示器物理事件待真机）
- [ ] 跨 DPI 拖放与降分辨率越界回收：需多显示器/虚拟显示驱动
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈（场景 F3 对话框数 0 + F4 恢复 90s 自愈已代验）
- [x] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出（场景 V1/V2/V4 已代验，38459944253/38661655290）
- [ ] RFC §6.2.7 四项真人操作与真实网络环境验收：②不抢焦点 2026-11-06 openclaw 真实点击复证（点胶囊唤起看板前台不变，`openclaw-20261106/` 批）；①下钻 PASS（UIA Invoke 时间线行→L3）④不进任务栏/Alt-Tab PASS（.5 批）；③动作面清单已补（14 面逐条动作定义+铁律对照+缺口，`docs/acceptance-evidence/2026-11-08-l0-action-surfaces.md`）；真人操作仍待装机（6 面清单见该文档）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] tile 与胶囊改色/重置托盘图标同步：2026-11-06 openclaw 实测——`severityColors.warning=#ff00ff` 重启后 warning 态托盘圆点渲染品红 RGB(206,0,206)（`openclaw-20261106/mag-tray-8.png` 16 帧稳定），删键重置后默认色表恢复（`tray-after-reset.png`）；B-706 运行时绘制链真机复证；改色/重置已有 G4/G5、38953986133 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 某 Widget 设 colorOverride 仅该 tile 变色，优先级正确（场景 G1 已代验）
- [x] 非法颜色值回退默认色不崩溃；重启后保持（场景 G2 已代验）
- [x] Success 到 Error 状态变化有平滑变色过渡（reduced 档）（场景 L1/L2 已代验，38284623093）
- [ ] 视觉观感与强度裁量真人验收：档位/强度/帧差分已有 G6/G7b/G7c/V2 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 动效不抢焦点、不发声；隐藏/收起时无动画循环（场景 H1 + AudioApiAbsenceTests 全源 206 文件断言 + 循环面代码审计；真机听声目检可装机复核）
- [x] 导出文件全文无 token；导入+重录密钥后行为与原机一致（场景 Z，204b884，run 38725138151）
- [ ] 真人全流程录屏归档：需录屏能力与真人（CI 截图已有 visual-evidence 工件）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [ ] B-803 干净 Windows 11 VM 下载运行验收：当前环境为 Linux 无 Windows VM
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 调色/动效改动即时预览并持久化，重启生效（场景 G4/G6/G6b 已代验）
- [x] 重置默认一键还原；off 档预览即全静止（场景 G5/G7b 已代验）
- [x] tile 底部进度条显示不全缺陷修复（外框/客户区混用，02a5de1，run 38500538817 复证绿）

## 后续可选（master-plan.md，5 项全勾）

- [x] L3 内动作链（[Retry] 全链路）CI 场景化（U1-U5 全绿 38434164526；L3 炸窗修复 896758c，38999120347 复证绿）
- [x] Critical 与崩溃注入机制（debug 门控）（DebugInjectionTests 8 例 + DispatcherQueueGuard 兜底；38494525200 V1-V3 全绿）
- [x] 目标厂商私有 API Provider：缺厂商名称/端点文档/凭据（Jenkins 标准 API Provider 2026-10-11 已落位）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
  2026-11-06 用户令「开源的就自己搭」：docker beacon-jenkins-test（jenkins/jenkins:lts-jdk17，Jenkins 2.541.3，宿主口18087）真机端到端——API Token（真实端点 jenkins.security.ApiTokenProperty/generateNewToken，旧资料 jenkins.api.ApiTokenProperty/createToken 实测 404）走 Basic 打连接 /api/json 与 JobTree 组件端点，返回结构与 Provider 解析逐键对齐（blue #1→Success、404→Offline、401→Unauthorized）；JenkinsLiveTests 4 例 opt-in 全绿 + 死端点负控 4 例如期红，四测试项目 673/956/27/41=1697 全绿（f40a6e7、6f2c8d2），容器与自建网络已清理、镜像保留
- [x] SettingsWindow 构造期 Resize 同型外框/客户区混用（f4b0eb1）
- [x] AI Usage Provider（11 家已落地，可靠性三档；条件门控满足后关闭，见 master-plan §13）

## MVP 验收标准（master-plan §14，4 项 = 2 勾 / 2 未勾，需真实装机验证）

- [x] explorer 登录会话调度实机验证：Run 键已有 M3 证据 38761946476；登录循环 2026-11-06 openclaw 实测——注销（4647 @12:39:31）→交互登录（4624 type=2 @12:56:37）→explorer 9392 拉起 Beacon pid 208（logon-probe.txt：`beacon=208 parent=explorer(9392) session=2`，命令行即 Run 键值）；证据 `docs/acceptance-evidence/openclaw-20261106/logon-probe.txt`、`2026-11-06-openclaw-realmachine.md` §1（首条探测 beacon=none 系 ONLOGON 探测早于 Run 键发射，如实记录）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [ ] 更长时稳定与胶囊视觉观感实机观察：需 Windows 实机持续运行（托盘驻留与 15min 浸泡已有 E2/E3、AD 证据 39057049259）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 全局快捷键正常打开悬浮面板（场景 K1/N + WM_HOTKEY 直驱硬断言已代验）
- [ ] 真实 GitHub PAT 与目标仓库全链验收：待用户提供凭据或明确授权（凭据类输入，非测试机缺口；三台 Windows 真机 openclaw/imac/.5 在位）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)

## 用户四单（2026-10-14，4 项全勾）

- [x] 事件看板默认全部 + 点击过滤且过滤态可见（活动 chip 实心高亮 + 概览头带源；e905a48+23660f8，1078 测试绿，run 38549671960）
- [x] 看板中文 + 设置页语言切换（UiStrings en/zh 双字典唯一机制，即时生效落盘；e905a48，1078 测试绿，run 38549671960）
- [x] 悬浮框边缘坍缩加开关（默认开）+ 收起态点击细条粘性展开（e905a48，1078 测试绿，run 38549671960）
- [x] 托盘 Open Beacon 与 Notifications 语义分化（意图出处 B-503 = mvp-issues.md:266；e905a48，run 38549671960）

## 外部评审 2026-10-22 展示组件选择机制落地（2026-10-23 立，4 项全勾）

> 来源评审：docs/review/2026-10-22-display-component-selection.md；裁定：先架构闭环再扩模板库，跟踪表 12 项清零前不新增模板种类（细目见评审文「落地跟踪」表）。

- [x] P0 架构闭环第一批：空状态断链/旧对象覆盖/52%% 双百分号/样式恢复/槽位化替换（f9a9aac，DisplayTemplateTests 905 绿，CI run 40599680681）
- [x] P1 架构闭环第二批：缺失值区分 + 能力匹配 + 语义描述（f9a9aac）
- [x] P1 第三批：DetailWindow 完整源映射渲染 + 组件中心实时预览与参数表单（f9a9aac）
- [x] P2 收尾：模板感知尺寸防裁切真机确认 + 全局 progressStyle 与实例样式优先级统一（代码半已落 f9a9aac，裁切观感装机复核）。2026-11-08 收口=imac 真机探针（数字卡模板 48 档 vs 32 对照同框）确认防裁切生效，探针顺带抓出并修掉两缺陷（槽位面列定义缺一叠印 ff708ec、caption 无截断硬裁 c7b0cb2，CI 42400083245/42400558637 绿），全局 pool 下实例 bar 生效实证，证据 docs/acceptance-evidence/2026-11-08-p2-tile-sizing.md；待用户装机确认。
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- 验收口径：P0 各项须有 Windows CI 场景或等价端到端断言，不得只跑 DisplayTemplates 纯函数单测收工。

## 文档双语补全（2026-10-16 用户令，2 项全勾）

- [x] docs/ 根目录实质文档 16 篇全部成对（13 篇完整译本 + master-plan/mvp-issues/mission-excerpt 3 篇过长留「暂无译版」说明；本目录链接 0 断链，mkdocs build --strict 绿；b3f32e0）
- [x] docs/design/ 7 篇与 docs/rfc/ 1 篇双语配对（7 篇完整译本 + redesign-plan-2026-10-16 留说明；23 对行数/标题/表行/围栏计数全等；mkdocs strict 绿；51492ae）

## 组件化重构分期（依 docs/design/redesign-plan-2026-10-16.md，43 条 = 42 勾 / 1 未勾）

> 性质：外部设计稿，不是本仓事实；期次划分照设计稿 Phase 0-6 不自行重排。现状核验见 docs/design/redesign-2026-10-16-核验.md。

### Phase 0 现状审计与契约冻结（6 勾）

- [x] 列出现有 Provider、Widget、Action 与 Renderer 清单（映射与迁移 §1，e1229fc）
- [x] 核对 WidgetState 的事件总线、刷新调度与 ActionRunner 契约（核验表 50 条，759742b）
- [x] 识别业务状态与显示字段（映射与迁移 §2，e1229fc）
- [x] 确定新组件模型与旧配置的兼容方案（契约.md §3，409d4e7）
- [x] 保留现有已完成功能与真机验收要求（映射与迁移 §4/§5.4，e1229fc）
- [x] 冻结状态流契约与组件契约（契约.md，409d4e7；HTML 边界留问见 phase4-html-ruling-2026-10-17.md）

### Phase 1 响应式状态与数据流（5 勾）

- [x] 明确 State、Event、Command 与 Operation Result 四类概念（契约.md §1/§4，409d4e7）
- [x] 完善状态变更检测（StateStoreTests 锚定，d71bdc8）
- [x] 统一的 State Store 与 Selector（IStateStore + StateStore 15 测试，f5d1d91/24678f9）
- [x] 明确订阅、退订、刷新失败与过期状态处理（WidgetHostTests + StateStoreTests，f5d1d91）
- [x] 确保 UI 只消费状态、不直接请求外部数据（核验表 A03，759742b）

### Phase 2 设计系统与基础组件库（9 勾）

- [x] 统一设计令牌：色彩/字体/间距/圆角/阴影/动效/尺寸/状态（DesignTokens，3be4efc）
- [x] 基础组件 Text、Icon、Surface、Badge、Divider（a517594）
- [x] 基础组件 Metric、Progress、Status Indicator（bf95dde）
- [x] 基础组件 Key-Value、Status Card、List（1db70de）
- [x] 交互组件 Action Button、Action Menu、Link（bb86927）
- [x] 布局组件 Stack、Grid、Section、Conditional（8caf0b5）
- [x] 通用状态 Empty、Loading、Error、Offline（5489352）
- [x] 统一主题、尺寸与排版体系（as-built 文档 docs/design/redesign-2026-10-16-组件库落地.md，f7ffb12）
- [x] 独立视觉示例页 SampleGalleryWindow（e623445）

### Phase 3 组件组合与数据绑定（6 勾）

- [x] 定义 Component Descriptor（Core 校验锚定，5e076e6；App 渲染消费 5d7efdf/7ef9fdb）
- [x] 定义 Widget Instance 与 Binding 配置（Core 21 例锚定，e065107；渲染接线 5d7efdf）
- [x] 实现 Selector 到组件属性的绑定（Core 12 例锚定，c9e0b0e；渲染期取值 5d7efdf）
- [x] 实现多个数据源组合（Core 17 例 + Storage 1 例，dde7b14；跨组件路由与样例闭环 5d7efdf/7ef9fdb/9976081）
- [x] 支持不同组件消费同一份状态（Core 8 例锚定，65efa84；宿主依赖路由 5d7efdf）
- [x] 保证修改布局不触发无意义的数据请求（Core 3 例锚定，fd9f2d1；零取数分流 d8ff4b6/332716f）

### Phase 4 HTML 小屏幕（6 勾，2026-10-17 用户裁定「这个设计下吧」）

- [x] 实现 HTML/CSS 模板加载（受控子集 + 原生渲染，b1893a1/f362d4c）
- [x] 实现受控数据桥接（f362d4c/4c39c3d）
- [x] 实现 Action ID 桥接（bcd950e，CI 编译门 run 39042835027 绿）
- [x] 实现模板错误回退、实例生命周期与安全限制（HtmlTemplateParserTests 62 例 + HtmlTemplateSanitizeTests 4 例，f362d4c/4c39c3d）
- [x] 实现主题令牌与基本样式适配（f362d4c/bcd950e，CI run 39042835027 绿）
- [x] 验证数据变化时可以增量更新（样例常青门，CI run 39043157904 绿，f362d4c/9f6dfd3）

### Phase 5 完善组件目录与用户体验（5 勾）

- [x] 时间线、表格、图表与趋势组件（ComponentDataTests 27 例，四项目 1274 全绿，98f7a2d）
- [x] 更丰富的布局与条件显示（ComponentVisibilityTests 27 例，1247 全绿，af22955）
- [x] 组件实例编辑、尺寸调整与桌面布局配置 + 可视化树编辑器（1328 全绿，016ba97/de528f9）
- [x] 统一的空数据、离线与错误状态（WidgetFeedbackTests 19 例，1220 全绿，986ea52）
- [x] 主题、密度与可访问性完善（UiDensityTests 29 例；场景 AA run 38953986133 全绿；f3c2bf9/6330885/178efce/671c947/ac19cb3）

### Phase 6 回归与真机验收（6 条 = 5 勾 / 1 未勾）

- [x] 动作端到端命令执行半（场景 AF run 40903469089，cf43001；URL 开浏览器半归 A-2 BLOCKED）
- [x] 验证多窗口订阅与取消订阅（EventBus 5 例 + 三窗 provider 测试；场景 AB run 39057049259 全绿）
- [ ] 重启恢复的显示器切换与 DPI 拖动：需第二块显示器与解锁的 Windows GUI（位置/收起还原已有两轮实机记录）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] HTML 安全边界与错误恢复（HtmlTemplateParserTests 62 例 + Sanitize 4 例；样例导入 CI run 39043157904 全绿）
- [x] 验证缓存、断网、凭据错误与操作失败（四支路测试面代验，1328 全绿，de528f9/75f4a85）
- [x] 验证现有 Provider、AI 额度显示与通知规则不回归（Connections 662 全绿 + 真机 356 次刷新零失败，de528f9/75f4a85）

## 面板升级看板（2026-11-05 用户令）

- [x] PANEL-UPGRADE-1 [P1] 快捷面板升级为看板：默认尺寸放大（目标 ~720x560，可调可记忆），事件动态（时间线）成为主区——时间/级别/来源/摘要逐行可读，条数放宽（现 8 条上限重新评估），头部 OverallSummary 与来源过滤保留；悬浮岛/胶囊点击唤起目标同步指向看板；字体与行距按可读性重排；设置项若涉尺寸记忆走 shell-state 惯例。
  - 验收: Core 侧筛选/聚合逻辑单测（TimelineEntriesTests 含 `Assert.Same(evt.State,row.State)`，72a99cb）；CI windows-latest 出看板截图（nightly run **41981480531** @`06134d1` 的 visual-evidence 工件 `K-panel-open-offline.png`：720x560 看板 + EVENT TIMELINE 主区 + 右副栏三段）；真机人工核验可读性（2026-11-06 .5 真机 `docs/acceptance-evidence/board-default-720x560.png`：客户区实测 720x560、时间线 7 行时刻/级别/来源/摘要逐行可读、25 条事件在列、头部 `2 警告 · 0 错误` 与概览过滤卡保留；UIA `Transform.Resize` 736x576→856x666、重启后尺寸/位置还原 720x560@(1160,316)）。
  - 剩余（非验收口径）：胶囊点击唤起看板 2026-11-06 openclaw 解锁会话实测 PASS（ESC 关→点胶囊→看板重开且前台不变，`openclaw-20261106/board-closed.png`/`board-opened.png`+`capsule-test.txt`）；头部把手鼠标拖动、边框鼠标拖拽两小项仍 OPEN（合成拖拽未做）；另发现右副栏（通知/需要关注）行文本右缘截断，建议后续换行/Tooltip（前轮记录不改码）。
  - 方案 B 重设计落地（2026-11-11 用户拍板主从反转、弃方案 A；原型 ~/beacon-board-redesign/board-redesign.html）：左「现在」296px 固定列（聚合灯+OverallSummary 打头、需要关注告警项列表占最大视觉权重、概览来源卡收底）+ 右时间线主区（rail 时间轴、跨日条目带日期 MM-dd HH:mm）与通知段；通知/状态三处 MaxHeight 硬顶消除改段内滚动；空态文案三处（暂无事件/通知已全部已读/无匹配+清除筛选）；把手点阵 affordance 悬停转品牌色；品牌 #d4234a 进 ThemeColors.Brand（级别色仍走 PaletteResolver）；状态脚条（运行中/正常计数+拖动/调尺寸/ESC 提示）；最小宽 360→520（固定左列）。CI/UIA 锚零迁移：AttentionList/RecentList/NotificationList/chip-*/W1 三文案全部保留，visual-evidence 断言零改动；悬浮岛唤起链（CapsuleWindow.OpenPanelRequested→quickPanel.Toggle）与设置开关即时生效路径未动。

## 真机验证批 2026-11-06（19 开项逐项处置，证据 docs/acceptance-evidence/2026-11-06-realmachine.md）

> 机器：10.240.0.5（Win11 26H2，1920×1080 @100% 单屏）；构建：`origin/main` `56e4f27` 现场 publish（`2026.11.6+56e4f27…`）。
> 续批（同日）：openclaw（10.240.0.3，Server 2022，解锁交互会话）完成 explorer 登录循环与 Toast/托盘/改色/唤起真机收尾，证据 `docs/acceptance-evidence/openclaw-20261106/` 与 `2026-11-06-openclaw-realmachine.md`；下表 :39/:43/:49/:62/:90/面板剩余 行已按续批更新。
> 取证口径：该机控制台**锁屏**（前台=Windows 默认锁屏界面）→ 整屏 `CopyFromScreen` 只得壁纸帧（红标记窗在屏而截图红像素=0）、
> 合成鼠标键盘到不了窗口；故截图一律 `PrintWindow`、交互一律 UIA/`PostMessage`。需真人解锁会话的项标 `OPEN(锁屏)`，不冒充已验。

| todo 行 | 项 | 结论 |
|---|---|---|
| :33 | URL 端到端打开 | ✅ 已勾（见上） |
| :39 | Toast 发送/点击/激活 | ◐→发送+呈现半 PASS 补强（openclaw 批：Balloon dispatched + 横幅实拍 banner-t2.png + 通知中心两条 Beacon 通知；本机补强批 `bm2-toast-tray/`：GLM Failed OS 横幅 12:04 帧实拍→12:05 MISS 相邻帧 + 通知中心两条在栏 + 详情窗 Open→浏览器落 usage 页）；点击激活路由 OPEN——Failed+无效 detailUrl 已本地 mock 稳定触发+投递 OS（无 403 依赖），imac 横幅显示被系统压制→点击 BLOCKED（docs/acceptance-evidence/2026-11-08-toast-activation.md；真人点击在横幅可显示机器上验，面 12 装机清单） |
| :43 | 托盘图标颜色/刷新周期 | ✅ 已勾（openclaw 批：同位两态像素差分绿↔琥珀、30s 档刷新实测、图标不进溢出；本机补强批 `bm2-toast-tray/` 红绿两态放大 + 任务栏/溢出区对照） |
| :49 | 跨进程点击穿透/焦点 | ◐→焦点面真机 PASS 补强（openclaw 批：点胶囊唤起看板前台不变、ESC 关面板）；收起态穿透到下层窗口 OPEN |
| :62 | tile/胶囊改色→托盘同步 | ✅ 已勾（openclaw 批：warning=#ff00ff → 托盘圆点品红 RGB(206,0,206)，重置复原） |
| :90 | explorer 登录会话调度 | ✅ 已勾（2026-11-06 openclaw 真实注销→登录循环，4624 type=2 → explorer 拉起 Beacon pid 208，见 openclaw-20261106/ 批） |
| :92 | 长时稳定观察 | ◐ soak-log.csv 60s 采样（09:53 起，计划 8h）：单进程、163MB/1351 句柄平稳、无重复进程；4h 口径未满 |
| :112 | 模板尺寸裁切复核 | ⬜ 本轮未执行（组件中心 UIA 链 CI 场景 AE 已可复用） |
| :184 | 重启恢复 + DPI 拖动 | ◐ 重启还原 PASS（看板 720x560@(1160,316) 前后一致）；DPI 切换/拖动 OPEN(锁屏+单屏) |
| :73 | B-803 干净 Win11 装包 | ⬜ 本轮未执行；本机非干净快照（有 .NET SDK/scoop），干净环境半仍归 B-804 |
| :56 | 跨 DPI 拖放/降分辨率回收 | ⬜ OPEN(单屏+锁屏) |
| :60 | RFC §6.2.7 四项铁律 | ◐ ①下钻 PASS（UIA Invoke 时间线行→L3）②不抢焦点 PASS ④不进任务栏/Alt-Tab PASS；③动作面清单已补（14 面，`2026-11-08-l0-action-surfaces.md`）；剩真人操作 6 面待装机 |
| :67 | 视觉观感真人验收 | ⬜ 待用户（真机实拍已给：board-default-720x560.png、capsule.png、config-effects/*） |
| :71 | 真人全流程录屏 | ⬜ OPEN(锁屏，整屏录制无有效画面) |
| :19 | 跨 DPI 拖动（MVP 行 19） | ⬜ OPEN(单屏) |
| :83 | 目标厂商私有 API | ⬜ BLOCKED：缺厂商名/端点/凭据（2026-10-30 决策 12 输入表未收齐） |
| :95 | 真实 GitHub PAT 全链 | ⬜ BLOCKED：需用户本地录入授权测试仓库的令牌（决策 15：无用户输入保持阻塞，不用 mock 勾选） |
| 面板剩余 | 面板把手拖动/边框拖拽/胶囊唤起看板 | ◐ 胶囊唤起看板 PASS（openclaw 批解锁会话）；把手拖动/边框拖拽 OPEN，见 PANEL-UPGRADE-1 剩余行 |

## 待派工

- [x] BUG-1 概览 OpenCode 品牌图标渲染成实心方块：BrandIconFactory.cs:67 强制 FillRule.Nonzero，而 BrandIcons.cs opencode path 是同向双矩形镂空「0」形（nonzero 下内框 winding=2 被填实）——修复=工厂统一 FillRule.EvenOdd（Simple Icons 无自交图形，even-odd 等价），Core 补解析单测断言镂空语义（SvgPathParserTests.Parse_OpencodePath_IsNestedDoubleRectangle，948 Core 绿）（2026-11-05 用户截图报障；3443ad3，CI run 41956471135 绿；观感复核=装机后概览选中 opencode 来源卡看「0」形镂空）
- [x] BUG-2 概览来源卡文字截断：QuickPanelWindow.cs:363（chip 标签）与 :481-482、:548（MaxWidth=260 + CharacterEllipsis）在窄窗三列布局下把来源名截没（用户截图「文字都显示不全」）——修复=来源卡标签换行不省略+卡片 MinWidth=96+整卡 Tooltip 全名，通知标题与状态行摘要改换行优先（260 只作换行边界）（2026-11-05 用户截图报障；e3a8b3a，CI run 41956565696 绿；UI 截断不可单测，人工验收=装机后窄窗拖小面板看来源卡全名换行+悬停 Tooltip）
- [x] 悬浮岛 CapsuleWindow（display-modes-analysis §7 Phase 2，2026-11-05 用户单）：config.capsuleIsland 展示形态开关默认关保持现状；开=顶部居中锚定（CapsuleIslandMath 纯函数 6 例 Core 锁口径）+ 收起态=状态环+未读/异常徽标+品牌标 + 点击唤起看板（§1 只读契约不变）+ 拖动位置独立持久化（shell-state.json islandLayout，与经典胶囊布局互不覆盖）+ 设置页开关即时生效（capsule-island-toggle，UiStrings 双语）；ace0cdb+9a03daa，1683 测试全绿，CI run 41957917477 绿；岛观感/拖动/徽标不可单测，人工验收=设置开「悬浮岛形态」看胶囊顶部居中+徽标随未读/异常变色、拖动重启还原、点击唤起看板
- [x] 悬浮岛回归（UIA 挂死）修复：CapsuleWindow 无条件 new+Activate——showCapsule=false（CI 各场景缺省口径）也常驻一个置顶工具窗干扰 UIA，nightly Visual evidence 两次无进展被人肉取消（41958504066/41963265872；日志复核两跑各场景实际持续推进，取消点分别在 AB3 静默窗与 AF→J 过渡的静默间隙，非真挂死，但无护栏则永无出口）——修复=①胶囊窗懒实例化（App.xaml.cs EnsureCapsule 工厂：仅 ShowCapsule=true 建/显窗，设置页开关运行时切换即建/关，建窗即回放 lastStates 来源预览+聚合快照+未读徽标初值；CapsuleIsland 仍为纯形态开关不独立建窗，构造函数本就不激活/置顶，Activate 留在显式路径；WinUI 定赋值穿透局部函数链 CS0165 两轮 CI 实证：settingsWindow/hotkey 前置声明）②visual-evidence.ps1 总超时护栏（分离 watchdog 缺省 4800s=80min 强杀本脚本进程，BEACON_EVIDENCE_TIMEOUT_SEC 可调；脚本含 15min 浸泡全程实测 ~72min，不能按 15min 设）+ 开场 Beacon 窗口基线与 Stop-Beacon 残留取证落日志（a517429/55e5bc8/2a989dd）；验证=CI 41973057764 绿（1683 测试）+ 手动 nightly 41973341778 全绿（Visual evidence 步 success，132 场景断言全过含 AD 浸泡/AF/J，日志见「总超时护栏已武装」「窗口基线[script-start]」，05:22:47→06:34:26 共 71.7min 于护栏内自然完成，run 结论 success 非 canceled）（2026-11-05 悬浮岛 commit 后回归；人工验收=装机开 showCapsule 看胶囊照常右下角、设置关胶囊后置顶窗彻底消失）
- [x] DOC-GIF：为全部「开启后有效果」的开关/配置项生成 off/on 对比图与切换 GIF，落 docs「配置效果」双语页，流水线可重跑兼作回归（kode todo C-24 同款，2026-11-01 用户令全舰队推广）——2026-11-06 落地：9 组有画面效果的开关成对静帧 + 9 张 GIF（theme / pinDisplayMode / progressStyle / motion.mode / density / severityColors.warning / uiOpacity / capsuleIsland / colorOverride），页 `docs/configuration-effects.md` + `docs/zh/configuration-effects.md`（mkdocs 双导航），原帧 `docs/img/config-effects-src/`（25 文件 + manifest）；流水线 `tools/doc-gif/capture-config-effects.ps1`（Windows 抓帧，PrintWindow 窗口级）→ `tools/doc-gif/assemble.py`（任意机器合成），本轮两段各跑通一次（含 -Only 补跑与基线还原）。图外说明：`launchOnStartup`/`hotkey`/`pollIntervalSeconds` 效果是注册表值/热键/节奏（已在页内指向验收证据）；`keepQuickPanelOpen`/`edgeCollapseEnabled` 为行为类且 edgeCollapse 需真实拖拽，待解锁会话补图。
- [x] USER-RPT-1 悬浮岛视觉重做：2026-11-06 用户实测「简直无法看」——岛形态从未做过真机视觉验收即报完成。处置=deleg_b1bba043 在飞（.5 实机截图诊断→按 Dynamic Island 观感标准重做→前后对比证据→CI 绿→push）。验收口径：用户装最新包亲眼确认可看，才算闭。2026-11-08 收口=a413ec0 一次修完五缺陷（看板 DI 回归/贴顶/收起高度残留/影子暗带/启动闪帧），imac 200% DPI 十件真机取证+前后对比（收起窗 482×86→482×70 贴顶平齐），证据 docs/acceptance-evidence/2026-11-08-island-head-closeout.md，CI 42396968796 绿；待用户装机确认。
  - 交付（2026-11-06 续）：patch 落源（CapsuleWindow.cs/.xaml+ThemeColors.cs+MotionEngine.cs：近实色黑药丸+发丝白描边、健康环退场单灯点+晕、徽标异常取色压过未读、悬停展开 MorphIn 形变；评审修复两处=环缓存恒更新防切回陈旧环、MorphIn 限岛形态保经典逐字节不动）；.5 真机（10.240.0.5）现场 build 0 错误 + test **TEST_EXIT=0**（同 patch 基线 1670/0）+ publish self-contained（13:35 `publish-island`，标记门禁防无补丁假构建）；三态真机取证最终构建实际截取——收起 135×39 顶中、error 注入徽标 蓝→红、SendInput 悬停展开 162×94 顶缘锚定向下长出+收回复核 135×39，附看板唤起 736×576；vision 自查三态全过（暗底白字对比恒定/单色故事/行内三级对比清晰）。证据 `docs/acceptance-evidence/2026-11-06-island-visual.md` + `island-visual-before-after.png` 等十件。待用户装机亲验后闭。
- [x] USER-RPT-2 整体显示不正常：2026-11-06 用户实测显示异常（具体面板/形态待实机截图定位）。处置=随 USER-RPT-1 的实机取证一并排查（最新构建+看板方案 B+岛形态三态截图逐一核对）；找出全部显示缺陷一次性修，不再挤牙膏。2026-11-08 收口=实机诊断定位五缺陷一次修完（a413ec0，含看板打不开这个最大异常面），四态+看板+经典对照全取证无新增异常；待用户装机确认。
- [x] USER-RPT-4 悬浮岛效果错误：2026-11-06 21:5x 用户在已含 65dc2ac 重做的最新 Nightly（13:15 UTC 构建晚于重做提交）上实测「悬浮岛的效果是错的」——重做版视觉仍不达 Dynamic Island 观感标准，用户否决。处置=实机截图取证定位具体错面（形态/位置/动效/徽标/展开行为逐项）→ 修正 → 三态前后对比证据 → 用户亲验闭项。2026-11-08 收口=错面定位为不贴顶+影带板感+收起高度残留三项，a413ec0 全修（10x 像素级取证贴顶平齐+肩 16+底角 12+发丝描边），证据同 USER-RPT-1；待用户装机确认。
- [ ] USER-RPT-3 流程整改：用户反馈的问题未入 todo 即流失（本三条即是补记）——铁律已强化：用户每报一个问题，当场入项目 todo.md 带来源+日期，处置结果回发用户确认。
- [x] ISLAND-RING-1 岛圆环进度徽标 + 组件化两扩围 + 载体统一（2026-11-07 用户令链 03:1x–03:3x 四连发）：①岛收起态要有圆圈进度展示（「人家都有圆圈展示进度的」，BrimDeck 技法学照原生实现）②进度环/进度条收编为组件中心两个组件（勾选即变、无默认样式概念）③岛消费 hiddenComponents（勾掉的组件不进岛的状态聚合）④展示载体（悬浮岛/悬浮框/看板）统一进组件中心勾选，托盘只留设置。2026-11-08 收口核对：①②③④代码面全落（c78200e 交付+d98e674 源头过滤修正），真机证据 island-ring/ 十一件（勾掉故障组件回绿满环/只环/只条/都不勾三态）+ a413ec0 修掉其看板 DI 回归（ToggleBoard 通则四载体唤起面全通，04-board-window.png 实证）；待用户装机确认。
  - 交付（c78200e）：`RingProgressBadge`（底环+进度弧+中心数字，BrimDeck UI.Ring 技法原生落地：12 点起笔 ArcSegment 过半翻转 IsLargeArc+Counterclockwise+Round 端帽+≥.999 整圆；WinUI 无位置构造器/IsStroked 两处 CI 实证修正 e80fa89/1614560）+ `BarProgressBadge`；环/条=组件中心「悬浮岛」虚拟组件（island.progressring/island.progressbar，Core ComponentCenterCatalog 单源恒在册）；岛聚合过滤下沉 StatusAggregator 源头（SetHiddenComponentsProvider+Republish，**实机取证踩坑修正 d98e674**：消费端 lastStates 重投影有顺序竞态抹掉真实 Error，island-trace 04:12:39 raw Error:1→filtered err=0 铁证）；载体三项勾选（岛/悬浮框走旧字段零迁移、看板新增量可选字段 boardEnabled 缺键=true）+ 悬浮形态下拉收编 + 托盘菜单收编「设置+退出」（CI E2/E3 down-count 同步 0/1）+ 看板唤起单门 ToggleBoard。
  - 证据：`docs/acceptance-evidence/2026-11-07-island-ring.md` + `island-ring/` 十一件（imac 200% DPI PrintWindow 窗口级：红弧50%环+红条+中心数 → **勾掉故障组件回绿满环** → 只环/只条/都不勾三态 → 悬停展开行结构不变；并排大图 island-vs-brimdeck-side-by-side.png 重制，BrimDeck 列占位=其条带窗未渲染刘海面已如实记录）；修复后数据链 trace `ok=1 err=1 ratio=0.50 ring=#F85149` + 像素计数 red=829/green=0。imac 现场构建 0 错误+四套全绿（1001/27/673/41，行为锁红先行：IslandProgressComponent/StatusAggregatorHidden/CarrierCatalog Tests）。DPI 教训：非 PerMonitor-aware 进程 GetWindowRect 虚拟化坐标把 PrintWindow 截图裁半。
  - 待用户亲验：装机勾选组件中心「悬浮岛」组看环/条显隐即变、勾掉故障组件看岛回绿、设置→组件中心「展示载体」三项勾选窗口出现/消失，亲验后闭。

## 源码分层分析 2026-11-07

当前 HEAD：`7194f640b175afa0849785e30396408746eb63fc`。完整归纳见 `docs/source-layer-architecture.md` 与 `docs/glossary.md`。

### 抽象层清单

- 生命周期装配层：先读盘再组合进程与运行时，统一平台入口和退出清理——`src/Beacon.App/App.xaml.cs:122`、`src/Beacon.App/Services/BeaconRuntime.cs:104`。
- 持久化层：配置可恢复、凭据加密、缓存可丢、通知历史跨重启、Shell 布局独立——`src/Beacon.Storage/JsonConfigurationStore.cs:13`、`src/Beacon.Storage/DpapiSecretStore.cs:12`、`src/Beacon.Storage/JsonCacheStore.cs:9`、`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`、`src/Beacon.App/Services/ShellStateStore.cs:8`。
- 采集归一层：调度节奏、Provider 调用、晚到保护、缓存写回和状态发布分离——`src/Beacon.Core/Services/RefreshScheduler.cs:17`、`src/Beacon.Core/Services/WidgetHost.cs:196`。
- Provider 适配层：连通性测试与组件状态归一化分成两个契约，新增来源不侵入管线——`src/Beacon.Core/Abstractions/IConnectionProvider.cs:12`、`src/Beacon.Core/Abstractions/IConnectionProvider.cs:29`、`src/Beacon.App/Services/BeaconRuntime.cs:116`。
- 进程状态层：事件总线、内容去重快照和聚合状态构成共享真相源——`src/Beacon.Core/Services/EventBus.cs:6`、`src/Beacon.Core/Services/StateStore.cs:14`、`src/Beacon.Core/Services/StatusAggregator.cs:11`。
- 通知层：规则求值、平台出口、记录持久化三者分开装配——`src/Beacon.Core/Services/NotificationEngine.cs:14`、`src/Beacon.App/Services/ToastNotificationService.cs:20`、`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`。
- 动作编排层：确认、超时、重试、结果事件和来源刷新集中，具体副作用执行器窄接口隔离——`src/Beacon.Core/Services/ActionRunner.cs:18`、`src/Beacon.Core/Abstractions/IActionExecutor.cs:6`。
- 展示 Shell 层：L0/L1/L2/L3 与托盘只消费状态快照加事件，意图统一回运行时——`src/Beacon.App/Windows/PinnedHostWindow.cs:684`、`src/Beacon.App/Windows/CapsuleWindow.cs:23`、`src/Beacon.App/Windows/QuickPanelWindow.cs:40`、`src/Beacon.App/Windows/DetailWindow.cs:20`。
- 声明式渲染层：受校验的组件树经闭合原生注册表渲染，替代网页宿主——`src/Beacon.Core/Models/ComponentDescriptor.cs:9`、`src/Beacon.App/Components/ComponentSurfaceRenderer.cs:109`。

### 超大文件候选

- [x] LAY-001 [P1] 拆分 `src/Beacon.App/Windows/SettingsWindow.cs`（3124 行）——设置页同时承担导航、外观、连接 CRUD、组件 CRUD、动作库、导入导出和树编辑器，可用页控制器拆掉单类字段网（`src/Beacon.App/Windows/SettingsWindow.cs:30`）。（类加 partial、字段网留主文件 312 行；Navigation/Pages/Actions/Appearance/Connections/Widgets/ComponentTree/ComponentStyles 八个 partial 按职责分文件、峰值 478 行，commit 618a662，CI 42402811747 绿）
- [x] LAY-002 [P1] 拆分 `src/Beacon.App/Windows/PinnedHostWindow.cs`（1354 行）——窗体布局、PinTile 控件、拖拽会话与位置持久化四块可分别成组件/服务（`src/Beacon.App/Windows/PinnedHostWindow.cs:684`、`src/Beacon.App/Windows/PinnedHostWindow.cs:27`、`src/Beacon.App/Windows/PinnedHostWindow.cs:670`）。（PinTile.cs 668 行 + DragSession.cs 29 行独立成文件、宿主窗留 700 行，commit a6d297e，CI 42401372255 绿）
- [x] LAY-003 [P2] 拆分 `src/Beacon.App/Windows/CapsuleWindow.cs`（1155 行）——胶囊/悬浮岛渲染、悬停展开、来源预览、拖动和窗口位置恢复耦合在同一 partial 类（`src/Beacon.App/Windows/CapsuleWindow.cs:23`）。（主文件留字段/构造/生命周期 276 行；Drag/Positioning/IslandChrome/FullscreenAvoidance/StatusFaces 五个 partial 按方法组分文件，commit 32c8433，CI 42402811747 绿）
- [x] LAY-004 [P2] 拆分 `src/Beacon.App/Windows/QuickPanelWindow.cs`（1058 行）——看板窗口协议、三个分区渲染、通知、拖动/尺寸记忆可按职责控制器拆分（`src/Beacon.App/Windows/QuickPanelWindow.cs:40`）。（主文件留协议/门面 400 行；Drag/Sections/Notifications/InvokeableRow 四个 partial 分文件 + SeverityPalette 独立成文件，commit 9415c7f，CI 42402811747 绿）

### 目录/分类问题

- `src/Beacon.App/Windows/PinnedHostWindow.cs` 把 `PinTile` 和 `DragSession` 放在宿主窗文件里，但二者是控件/交互模型，能被独立测试和复用（`src/Beacon.App/Windows/PinnedHostWindow.cs:27`、`src/Beacon.App/Windows/PinnedHostWindow.cs:670`）。
- `src/Beacon.App/Windows/FloatingTileWindow.cs` 同时定义 `FloatingTileHost`、`FloatingTileWindow` 和位置持久化；host/window/布局落库三层混在一个文件（`src/Beacon.App/Windows/FloatingTileWindow.cs:26`、`src/Beacon.App/Windows/FloatingTileWindow.cs:262`、`src/Beacon.App/Windows/FloatingTileWindow.cs:218`）。
- `src/Beacon.Connections` 平铺了通用 HTTP、GitHub、Jenkins、本机、Herald 和 12 家厂商适配；当前 34 个源文件仍可检索，但协议客户端、Descriptor、Widget Provider、Connection Provider 混层，后续扩展建议按协议家族子目录收敛（`src/Beacon.Connections/ProviderDsl.cs:15`、`src/Beacon.Connections/GitHubApiClient.cs:12`、`src/Beacon.Connections/BigModelUsageProvider.cs:48`）。
- `AppConfigFile` 与 `JsonConfigurationStore` 都指向 `config.json` 且恢复策略不同：前者损坏即回默认，后者有诊断/备份；这是启动早期读写的历史薄封装，应作为后续收敛点，不宜新增调用方（`src/Beacon.Storage/AppConfigFile.cs:8`、`src/Beacon.Storage/JsonConfigurationStore.cs:276`）。

## 悬浮岛数据化 ISLAND-DATA-001（2026-11-08 用户令：三步流程——展示方式/展示组件/数据源/后续行为；4 项 = 4 勾 / 0 未勾）

用户反复强调的悬浮岛三步流程，现状逐条盘点（file:line 实证）：

- [x] ISLAND-D1 [P0] 第一步·选择展示方式：岛收起态展示形态可选（当前仅 CapsuleWindow.StatusFaces.cs:34-66 的 环/条 二选勾）。组件中心岛组扩为「展示方式」选择：单组件常驻 / 多组件堆叠 / 徽标+轮播 等，选项落 AppConfig 并进组件中心 UI（file: src/Beacon.Core/Services/ComponentCenterCatalog.cs:97-107）（cc9b577+89ccaf0：IslandDisplayPlan 三模式 single/stack/carousel + AppConfig.IslandDisplayMode + 设置页「展示方式」下拉 BuildIslandSection；Windows CI 42546551569 绿，Core 1170/Connections 874/Actions 54/Storage 48）
- [x] ISLAND-D2 [P0] 第二步·选择展示组件：组件中心勾选的真实 widget 可「上岛」（当前岛上零真实组件，仅虚拟环/条）。岛组列出全部已连接 widget（ComponentCenterCatalog.Group 已有全量清单）加「上岛」勾选；岛收起态按序渲染所选组件，宽度自适应收起行上限，超出堆叠/轮播（cc9b577+89ccaf0：ComponentCenterCatalog.OnboardCandidates/SetIslandOnboarded + AppConfig.IslandComponents 序 + 岛组「上岛」勾选 BuildIslandOnboardRow；同上 CI 42546551569 绿）
- [x] ISLAND-D3 [P0] 第三步·对接数据源：岛组件吃真实数据流（当前只接 Notifications.UnreadCount，App.xaml.cs:228）。26 个连接 Provider 的 widget 数据（claude/codex/copilot/kimi 用量、deepseek/moonshot 余额、herald 告警、http.status、jenkins.build、machine.net 等）按现有刷新节拍喂进岛组件渲染，数据为零/断连的组件按 data_quality 口径显示占位不造假（cc9b577+89ccaf0：App.xaml.cs RecomputeIsland 喂真实 widget 状态节拍 + IslandDisplayPlan.Compute 对空/断连/陈旧出占位 chip 不造假；同上 CI 42546551569 绿）
- [x] ISLAND-D4 [P1] 后续行为：点岛上某组件→展开看板并定位到该组件详情/配置（当前只有整岛点击开看板，无组件级下钻）；critical 数据组件点击直达对应处置入口（89ccaf0：RouteIslandClick 组件级下钻 + IslandWidgetActivated + QuickPanelWindow.RevealSource/OpenIslandTarget；同上 CI 42546551569 绿）
- 验收（用户可执行）：勾选 ≥2 个真实组件上岛 → 岛收起态截图显示真实数据（非恒 0 计数）→ 点组件跳对应详情；证据入 docs/acceptance-evidence/island-data/
