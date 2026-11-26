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
- 界面类条目走查审计（2026-11-13 任务单 beacon-ui-review）：47 条 UI 已勾项三问逐条复核（证据真伪/原始要求落实/待亲验提前勾），判真证据 27/证据不足 5/虚勾 15；虚勾 15 条改回未勾（Toast 点击激活 BLOCKED 半、P2 与 PANEL-UPGRADE-1 方案 B 后无真机、待派工 8 条装机亲验未发生、ISLAND-D1-D4 只有测试 run 无视觉取证且 island-data/ 目录不存在），计数头逐节重算；重点深查进度展示：多窗格式「5%/5h 7%/w 8%/M」已实现且有 T1e/V1 UIA 文本硬断言（真证据），进度条驱动=2026-10-15 用户令 5h 展示窗（非字面「最小额度」；ark 为最差窗，口径分歧留用户拍板）；结构性发现 nightly 零 island 视觉场景（岛类条目 CI 内无取证通道）；报告 docs/acceptance-evidence/2026-11-13-ui-audit.md。
- beacon-next 续推处置（2026-11-12 任务单 beacon-next）：todo.md 12 条未勾项逐条核对全部外部阻塞（跨 DPI 拖动/拖放 2 条=双 DPI 真机缺，重启恢复 DPI 1 条=第二显示器+解锁 GUI，Toast 点击 1 条=imac 横幅被系统压制需真人，跨进程穿透 1 条=行 69 已裁定不自加整窗穿透+AC 转用户裁定，RFC §6.2.7 真人操作、视觉观感裁量、真人录屏、B-803 干净 VM、长时稳定观察 5 条=需装机/真人/录屏，真实 GitHub PAT 1 条=凭据待用户，USER-RPT-3=回发确认待用户），无代码可做项故不凑数；本轮转交付同批发现的 nightly 真红（见上 NIGHTLY-AB2，f9580a7）。

## 设置与展示改版（SET-UX，2026-11-23，15 项 = 7 勾 / 8 未勾）

目标是让第一次用 Beacon 的人能知道接入了什么数据、在哪里看到、修改后何时生效。2026-11-23 用户看过原型后认可方向，按下列任务推进现有 WinUI 设置页改版。原型使用演示数据，实施状态以各条验收为准。

[可交互原型](docs/design/settings-redesign/prototype.html)

### 原型对应的实施任务

| 原型 | 任务 | 交付物 |
|---|---|---|
| [桌面显示](docs/design/settings-redesign/01-desktop.png) | SET-UX-02、03、04 | 状态条三选一、窗口与看板设置、组件实际去向及恢复入口 |
| [单组件样式](docs/design/settings-redesign/02-style.png) | SET-UX-05、06 | 按数据能力推荐样式、按显示位置预览和保存、单处恢复默认 |
| [数据来源](docs/design/settings-redesign/03-sources.png) | SET-UX-09、10 | 连接 → 测试 → 添加组件、选择显示位置、显示实际继承的刷新频率 |
| [同一数据的各处效果](docs/design/settings-redesign/04-surfaces.png) | SET-UX-07 | 同一组件在桌面、悬浮岛、看板与详情的呈现，以及额度、余额、状态和资源的组合 |
| 共用设置入口 | SET-UX-01、08、11、12 | 独立保存、按字段搜索、反馈与键盘操作、Windows 验收与文档 |

**实施顺序** 先做 SET-UX-01，解除改一项误写其他配置的问题。02、03 完成后接 04，统一窗口开关和组件行的状态。05 的数据能力契约先定下来，再接 06 的样式选择与 07 的跨位置呈现。08 可先落导航，字段搜索随控件迁移补齐。09 在 04 的去向规则可复用后接入，10 复用 01 的频率保存。11 的反馈与无障碍随各页交付，12 最后做跨页走查。

### 现状与设计边界

- 胶囊和悬浮岛由两个设置控制，运行时只有一个对应窗口。桌面「悬浮框」总开关也限制集中面板里的数值类组件，独立窗还受组件能力约束。设置应表达用户最终看到什么。[设置和窗口配置](src/Beacon.App/Windows/SettingsWindow.Pages.cs) · [显示策略](src/Beacon.Core/Models/WidgetDisplayPolicy.cs)
- 现有模板按百分比能力筛选，提供横条、圆环、百分比数字卡、水位和状态行。横向注水是另一条全局额度样式路径。组件树分别保存桌面卡片、看板状态行、详情，悬浮岛由独立计划投影。[样式注册表](src/Beacon.Core/Services/DisplayTemplates.cs) · [组件配置](src/Beacon.Core/Models/WidgetConfig.cs) · [岛展示计划](src/Beacon.Core/Services/IslandDisplayPlan.cs)
- 维持现有的托盘、唤起方式、位置和历史契约。桌面和岛用于扫读，点击打开看板。看板分开呈现当前状态与历史事件，详情承担明细和已有操作。原型优先表达这些已有入口。[展示形态与交互约定](docs/zh/design/display-modes-analysis.md) · [看板实现](src/Beacon.App/Windows/QuickPanelWindow.Sections.cs)

### 数据特征与组件选择

| 数据特征 | 现有实例 | 推荐组件及组合 | 缺失或不适配时 |
|---|---|---|---|
| 单个有明确上限的比例，带已用或剩余方向 | GLM 额度、自定义 HTTP 额度 | 主数值 + 横条。空间小时可用短条或圆环。圆形水位和横向注水是可选画法 | 没有可信分母或百分比时显示占位。真实 0% 照常显示 |
| 多窗口额度，各有周期、单位与重置时间 | Kimi 5 小时 / 周 / 套餐 | 注明周期的主值 + 次窗口标签。详情逐窗口列出比例、余量和重置时间 | 沿用各服务内已有主窗口规则。单窗缺失仅影响该行。不同周期保持独立 |
| 金额、计数、速率等标量 | DeepSeek 余额、打开的 PR 数、本机网速 | 值 + 单位 + 对象，必要时辅以状态词 | 缺总额时用数值卡，禁止推测百分比。真实零值正常显示 |
| 离散状态与执行生命周期 | GitHub Actions、Jenkins 构建 | 状态灯 + 「运行中 / 失败」等文本 + 对象。有可信字段时补阶段和耗时 | 无确定进度时显示状态。区分离线、未取到、过期 |
| 时间序列与多个并列指标 | CPU、内存、磁盘、网络 | 当前值 + 短趋势。不同单位并列展示。详情承载较长时间跨度 | 样本不足时显示当前值及样本状态。时间缺口不画成零 |
| 时间戳事件流 | Herald 告警、订阅、构建结果变化 | 时间线保留发生时间、来源、当时级别与摘要。极小位置只用异常或未读计数 | 区分无事件与筛选无结果。当前恢复后，旧事件仍按发生当时显示 |
| 多来源摘要 | 本机资源和 CI 并排、总体健康 | 选同一监控任务下的少量指标，标明来源。总览沿用当前聚合规则，提供分别下钻的路径 | 失效来源独立显示占位，其他项继续更新。不同单位和时点保持可区分 |

「数据来源」说明从哪里取数，「组件」是要显示的那项数据，「样式」是画法，「显示位置」是放在哪里。岛内健康环表示整体健康份额，额度环表示某项额度。环和徽标应带可读的含义。

### 同一组件在不同位置怎么变

| 位置 | 主要任务 | Kimi 额度示例 | 布局及交互 |
|---|---|---|---|
| 桌面集中面板 / 独立窗 | 看当前值 | 5 小时已用 65% + 横条，可附周已用 38% | 集中面板对齐读数。独立窗优先保住名称、周期和主值。两者共享组件身份与数据 |
| 右下角胶囊 | 看整体状态 | 总体状态和计数 | 保留已有摘要入口与点击唤起看板 |
| 顶部悬浮岛 · 收起 / 展开 | 收起扫读几项信号，展开认清来源 | 收起显示「Kimi 5h 65%」，展开补上「周已用 38%」 | 支持首项常显、并排显示和轮播。沿用现行候选顺序与最多四面的计划上限。宽度不足时以「另有 N 项」引导在展开态查看 |
| 看板 · 需要关注 / 事件动态 | 看现在哪项异常及何时发生 | 当前告警带当前值和周期。历史保留「14:20，5 小时用量曾达 80%」 | 自定义状态行只影响当前状态区。时间线保留历史快照、来源筛选、详情入口 |
| 详情 | 核对完整数据、查历史、执行已有操作 | 5 小时和周额度、分别重置时间、更新时间，有足够样本再显示趋势 | 主信息、明细、历史与操作依次展示，保留既有动作确认规则 |
| 托盘 / 通知 | 提示总体状态或事件 | 托盘显示整体异常数，通知提示一次阈值事件 | 沿用既有聚合和通知规则，换样式时不额外发送通知 |

示例用 5 小时 65%、周 38% 演示层次。实际内容以服务提供的数据和既有主窗口规则为准。第二屏、新展示载体及通知规则编辑器留在本批范围外。

### 第一批 · 配置正确性与桌面显示（4 项 = 4 勾 / 0 未勾）

- [x] **SET-UX-01 [P0] 单项保存与合法旧值回显**
  - **问题** 全局刷新配置允许 120 秒，下拉框却没有这一档。改开机启动可能把频率写回默认值。
  - **怎么做** 只保存用户改动的字段。预设外的合法值显示为「自定义 · 2 分钟」，复用原存储和即时生效路径。
  - **怎么验证** 分别加载 0、15、120 秒旧配置，单独改开机启动、快捷键或语言，断言频率和其他无关字段无差异。主动改间隔后检查调度和重启恢复。页面构造、搜索跳转时无写入。
  - **证据** 7aa1776：每控件单项保存器（SaveHotkey/SaveStartup/SaveTheme/SaveLanguage/SavePoll，构造期先赋选中再挂事件零写入）+ Core PollIntervalOptions（预设五档+非预设「自定义 · N 分钟」）26 例新单测全绿（本地 1349）；nightly run 45210161465（head f89ffb9）场景 SU1 UIA 硬断言：种子 120 秒旧配置 → poll-interval-box 回读含「自定义」→ 翻 startup-toggle → config launchOnStartup=True 且 pollIntervalSeconds 恒 120（全字段回写病灶回归门）；改间隔后调度重建与重启恢复面待装机亲验

- [x] **SET-UX-02 [P1] 统一状态条入口**
  - **问题** 常规页和组件中心分管两个开关，关闭岛时可能露出右下角胶囊。
  - **怎么做** 用「关闭 / 右下角胶囊 / 顶部悬浮岛」表达有效窗口，配位置示意。读取按岛优先、胶囊其次投影两个旧字段。主动选档时成组保存；岛关闭时折叠岛内容设置，保留其列表与顺序。
  - **怎么验证** 加载旧字段四种组合，检查初始选择、位置和窗口数量。遍历三档切换，确认即时、无残留、重启恢复一致，原岛内容和位置可恢复。
  - **证据** 7aa1776：新「桌面显示」页状态条三选一（各配显示器位置示意）+ Core StatusBarMode 岛优先/胶囊其次投影与成组保存（四组合投影=StatusBarModeTests 锁死）+ 组件中心岛内容岛关折叠；run 45210161465 场景 SU2 UIA/config 硬断言全链：双关投影「关闭」→ 岛=config 双开+岛窗在场 → 胶囊=岛关胶囊开+同窗保持 → 关=双关+窗退场 → 岛关后 island-display-mode 不在 UIA 树（折叠）；切换瞬态观感与重启恢复待装机亲验

- [x] **SET-UX-03 [P1] 分清窗口布局、数值开关和看板入口**
  - **问题** 「悬浮框」也影响集中面板数值；「悬浮形态」重启才切换；「看板」控制是否能打开。
  - **怎么做** 分三步接入。① 增加「桌面组件布局」和「在桌面显示数值和额度」，分别绑定布局和数值总开关，布局标明重启后生效。② 增加「允许打开看板」，显示当前快捷键和可用入口。③ 将不透明度与贴边收起移到窗口设置，用百分比显示不透明度并注明贴边收起只作用于集中面板。
  - **怎么验证** 用数值卡和支持独立显示的组件，在集中与独立两种布局下开关数值显示。检查透明度立即变化，关闭贴边收起后已收起面板恢复。关看板时已开即收并阻断热键、托盘和岛唤起，重开可恢复。
  - **证据** 7aa1776：桌面显示页「窗口与看板」区五件（布局档标注重启生效 / 数值总开关正名并注明两宿主同闸 / 允许看板带热键与入口提示 / 不透明度百分比读数 / 贴边收起注明只作用集中面板；改名小修 f89ffb9）；run 45210161465 场景 SU3 config 硬断言：数值+看板双关落盘 false、不透明度 RangeValue 0.85→uiOpacity≈0.85（容差）、布局选 panel→pinDisplayMode=panel（种子 floating 证真实翻转）；透明度视觉即时变化、面板贴边收起恢复、关看板阻断热键/托盘/岛唤起与重开恢复=待装机亲验

- [x] **SET-UX-04 [P1] 每个组件显示去向和阻挡原因**
  - **问题** 勾选、钉选与上岛分散，容易在列表里显示「已添加」但桌面上看不到。
  - **怎么做** 每个组件一行显示来源、桌面和悬浮岛配置及实际状态。复用既有显示策略，区分「在桌面隐藏」「从岛移除」与影响总体状态的「全局隐藏」。岛自身的进度环、条与用户组件分别说明。
  - **怎么验证** 覆盖未添加、不支持钉选、全局隐藏、总开关关闭、来源停用、岛关闭和岛溢出。验证当前状态即时更新、原因及恢复入口正确、重启保留。移出岛仅改变岛候选，全局隐藏按原规则退出聚合。
  - **证据** 2756e64：组件中心每行去向徽标与阻挡原因同源投影（桌面已显示/岛已上岛/未上岛，岛环/条/用户组件分别说明）；run 45333483412 场景 SF1 UIA 断言：GLM 桌面「已显示*」+ 岛「*未上岛*」、DS 岛「*已上岛*」；全局隐藏/溢出等全场景、即时更新与重启保留=装机亲验

### 第二批 · 数据能力、样式和组合（3 项 = 3 勾 / 0 未勾）

- [x] **SET-UX-05 [P1] 按数据能力推荐组件样式**
  - **问题** 现有筛选只显式识别百分比；数据缺失和类型不支持容易被理解为一回事。
  - **怎么做** 盘点已注册组件的字段、单位、比例方向、窗口、时点与历史能力，在现有类型描述、模板注册表和绑定上补充实际所需能力。说明不适配原因，例如「只有余额金额，无法显示百分比」。
  - **怎么验证** 用额度、余额、构建状态、资源曲线、事件流五种夹具断言推荐和禁用原因。覆盖 0%、100%、无值、NaN、金额零、单位和样本缺失。离线不改变类型能力，预览显示相应缺失状态。
  - **证据** 235ba8c：模板能力注册表按数据能力推荐/禁用（首兼容项标「推荐」，不兼容置灰带「不可用：该组件无{kind}数据」原因）；run 45333483412 场景 SF2 双夹具：GLM 7 模板全启用、恰一「横条 · 推荐」、零不可用；DS 仅状态行启用即推荐、其余 6 项置灰带禁用原因；五类数据夹具全覆盖与预览缺失态=装机亲验

- [x] **SET-UX-06 [P1] 按位置预览、应用、恢复样式**
  - **问题** 全局额度样式与实例覆盖并存，现有「恢复传统渲染」会清空该组件全部展示面。
  - **怎么做** 先选「桌面 / 看板状态行 / 详情」，再选样式。标明跟随默认、单独设置或自定义布局。预览使用同一数据；应用和恢复默认只改当前选中的位置。悬浮岛跳到其专用内容设置。外观页保留主题、全局额度样式、间距、动画，颜色和强度放入更多选项。
  - **怎么验证** 同组件三处使用不同样式及附加来源，改或恢复桌面后对比其他两处树和绑定。改全局样式时只影响继承者。检查切换预览、窄窗标题和数值的裁切、浅深主题、重启生效的主题和即时动画。
  - **证据** 2b8e1a5+b6bec7f：按位置应用/恢复（先选位置再选样式，应用与恢复只改选中位置；外观页颜色与强度收折叠区）；run 45333483412 场景 SF3：应用文案随位变（桌面↔看板状态行）+ 继承行双向翻（跟随默认↔此处已单独设置）+ 恢复只清 row；场景 AE3（按位置恢复）row 清 tile 留 + AE3（重置）逐面恢复后 components 清空；取证修复链 c4d9752（combo 卡 Expanded 态 Collapse 前置）/57648de（清唯一面后 components=null 双臂轮询）/647fe94（共享助手重取元素+3 轮重试）/c538262（catalog 重建致缓存 peer 失效改逐次重取，AE3 四步全绿复证 nightly run 45670616849）；三处树绑定对比、浅深主题与裁切=装机亲验

- [x] **SET-UX-07 [P1] 补齐多指标与跨位置组合**
  - **问题** 多窗口额度容易看错周期；多个来源组合时，一个缺值容易被误解为整体故障。
  - **怎么做** 分三步交付。① 在桌面与详情接入「额度主窗口 + 次窗口摘要」「资源主值 + 同单位趋势或并列状态」，复用组件树、附加源与绑定，各来源分别处理缺失。② 悬浮岛收起时保留来源短名、周期和主值，展开补充次窗口和溢出项。③ 看板当前区绑定最新数据，时间线保留事件发生时的读数和级别。
  - **怎么验证** 以 5h 65% / 周 38% 看桌面、岛、看板、详情的变化，再将主值变更至 80% 并验证绑定。停用附加源确认其他项正常更新。过去发生的告警在恢复后仍呈现旧值与级别。改样式无额外数据请求。
  - **证据** fd3b118+10898c5：岛收起行带周期+主值（FaceHost 可读名拼接 WidgetId+标签+读数）+ 展开溢出提示（另有 N 项）+ 看板时间线事件时点读数（MultiWindow 快照）；run 45333483412 场景 SF4：岛面可读名带「5h 38%」→ 主值 38%→95% 岛面跟随 → 看板事件行「95%/5h 15%/w 61%/M」（SendInput 和弦）；主窗口+详情跨位置组合、停用附加源独立更新=装机亲验

### 第三批 · 导航、接入与验收

- [ ] **SET-UX-08 [P1] 设置导航和字段搜索**
  - **问题** 服务、常规设置和高级配置混排，搜索页写「字段」但只匹配模块文字。
  - **怎么做** 一级入口为「常规 / 桌面显示 / 外观 / 数据来源」，备份诊断等分组。常规集中开机启动、看板快捷键、语言，并注明语言当前覆盖看板和托盘。更新搜索以匹配实际设置和旧名称，结果进入页面后展开分组、滚动并聚焦。托盘保留原入口兼容。
  - **怎么验证** 搜「透明度」「检查频率」「贴边收起」「GitHub」「组件中心」，分别断言页面与目标控件。测试键盘、无结果、清空、窄窗，以及现有 Windows UIA 搜索场景 D。
  - **证据** 6589111（分组导航+字段搜索跳转）：一级四入口不设头、Provider/动作归「数据来源」「备份与诊断」头（nav-group-*）；SearchEntries 36 条字段索引匹配实际设置词+旧名（悬浮形态/检查频率…），回车进页→ExpandAllAndFind 展折叠区→StartBringIntoView→Focus，150ms×20 拍重试泵（45356987498 实证 TryEnqueue 跨不过布局帧）；语言覆盖面注明在控件旁；nightly run 45536879866 场景 SG 全绿（五路跳转+模块级+清词复原）；/7615da0（无结果+清空补 CI 门）：无结果词断头行「0/N 个模块匹配」+ module-github 离树（模块行全滤除）、清词后头行回「N 个模块」非过滤形态（Get-WindowUiaTexts 文本锚，格式串与 ApplyModuleFilter 逐字对齐），nightly run 45697308890 SG 无结果档+清词头行全绿（evidence/SG-no-result.png）；键盘 Tab 序待装机亲验；窄窗档 CI 门已绿（/dfeb789 nightly run 45754679256：600px 窗「看板」过滤 N/M 头行+行滤除+清词复原全链，搜索词改模块命中判据见 BEACON-FIX-1126）

- [ ] **SET-UX-09 [P1] 数据来源按接入状态组织**
  - **问题** 连接表单、组件表单和高级树同时出现；「已保存」容易被当成已验证或已显示。
  - **怎么做** 分三步交付。① 将已有服务和连接集中在「数据来源」，先显示服务能读到什么、连接状态和必要字段，可选端点等按需展开。② 复用原连接测试，将未连接、已保存待验证、测试成功和失败后重试映射到对应下一步。③ 提供推荐组件与位置选择，只列支持的位置，添加成功后能跳到桌面显示确认去向。
  - **怎么验证** 用固定响应覆盖未连接、缺凭据、测试失败、重试成功、连接正常而组件取数失败。验证状态与下一步准确，按所选位置显示组件，重复提交不重复创建。证据用测试数据且无密钥。
  - **证据** 65ed9f1（按接入状态组织来源）：连接行内状态 chip 会话级三态（已保存·待验证琥珀/✓ 测试通过/失败后重试，_connectionTestStates 不落盘重启复位）；未验证不出快速添加（状态门 SH 断言）；推荐组件区随连接结论重组；source-goto-placement「查看去向」直达组件中心该组件行；nightly run 45536879866 场景 SH 全绿（种子态→测试→添加落盘→按钮禁用）；/7615da0（失败→重试补 CI 门）：mock 增 fail 档（quota/limit 回 HTTP 500）→ chip「✗ 测试失败」+ 失败不解锁快速添加 → 删档重测=重试成功「✓ 测试通过」（HttpEndpoint 500→Offline→chip failed 链与代码逐环对齐），nightly run 45697308890 SH 四拍全绿（种子→失败→重试通过→添加落盘，evidence/SH-chip-failed.png）；缺凭据/组件取数失败覆盖面待装机亲验；SH 重跑全绿（/dfeb789 nightly run 45754679256，含病灶 D 门 220px）

- [ ] **SET-UX-10 [P1] 可读的刷新频率和继承**
  - **问题** 检查频率、刷新档、检测间隔三个名字掩盖了实际继承优先级。
  - **怎么做** 统一为「默认刷新间隔」「此组件刷新频率」，显示当前继承来源及配置周期，区分手动刷新和运行期退避。维持组件覆盖、全局覆盖、组件默认策略的优先级。
  - **怎么验证** 组合全局默认/固定、组件继承/覆盖和手动档。复用 WidgetHostTests 与 RefreshSchedulerTests。Windows 用本地固定源和时间戳确认调度在修改后生效，文案与调度一致。
  - **证据** 2a6da76（统一刷新档词汇）：全局框「默认刷新间隔（未单独设置的组件）」+ 组件行「此组件刷新频率」+ 继承来源 hint（SettingsWindow.Pages.cs）；刷新优先级组件覆盖 > 全局 > 组件默认策略沿用 WidgetHost/RefreshIntervalResolver 单源；nightly run 45536879866 场景 SI 全绿（GLM=每 5 分钟·默认刷新间隔 / OVR=每 2 分钟·此组件刷新频率 + 继承链 hint + 全局档 300→15 落盘；SI 文案锚一次取不到即判死曾假红，c538262 改 15s 轮询门，45670616849 复证绿）；手动刷新与运行期退避区分、时间戳调度一致性待装机亲验

- [ ] **SET-UX-11 [P1] 生效反馈、快捷键错误和可访问性**
  - **问题** 即时与重启混在一起，部分状态只由颜色表达；快捷键注册失败反馈离控件较远。
  - **怎么做** 生效时机与影响面写在控件旁。快捷键校验失败保留原有效绑定，显示原因与修复入口。复用 UIA 标识，完善焦点、名称、可读状态和窄窗换行。
  - **怎么验证** 使用被占用的键验证旧键仍可用、错误可定位、修改后可恢复。用键盘和屏幕阅读器走搜索、切换显示、选样式、接入。Windows 检查 100% / 150% 缩放、浅深色、窄窗和默认尺寸。
  - **证据** 9bbde1b（热键占用失败保留原绑定+行内反馈）：注册失败框值与 config 均保持原键、失败提示与修复入口就近显示；生效时机/影响面提示见 Pages.cs/Desktop.cs/Appearance.cs 各控件旁；nightly run 45536879866 场景 SJ 全绿（占用态「注册失败」可见+原键保留→改键成功即时生效）；/56fe648（浅深色档补 CI 门）：theme-box Select-ComboItemByName 浅色→config App.Theme=light 落盘+选中态回读、回切深色→dark（生效时机「重启生效」标注在控件 Header，浅色实际渲染翻转属视觉亲验面），nightly run 45697308890 SJ 主题档双拍全绿；键盘/屏幕阅读器、100%/150% 缩放、浅色观感与窄窗面待装机亲验

- [ ] **SET-UX-12 [P1] Windows 真机验收与配置文档**
  - **问题** 原型使用示例数据，窗口切换、配置落盘、旧版兼容和真实渲染需要从 WinUI 入口验证。
  - **怎么做** 用本地固定夹具扩充 [visual-evidence 脚本](.github/workflows/scripts/visual-evidence.ps1)，更新中英文配置文档和过时入口说明。
  - **怎么验证** 每项记录提交、测试结果、CI run 或安装包 SHA256、前后截图和配置差异。让新用户独立完成「状态条移到顶部」「找出额度组件没显示的原因」「搜索并修改刷新频率」「只改桌面样式」，记录误点击及预期和可见结果。达成各条验收后再勾选。
  - **证据** 7aa1776（批1=桌面页/状态条/单项保存）+ 6589111（导航搜索）+ 65ed9f1（来源状态）+ 2a6da76（刷新词汇）+ 9bbde1b（热键反馈）+ b30ef69/5fa232c/55e5bc8（visual-evidence 场景扩充与护栏）；configuration.md/zh 407 行双语同构且字段口径与代码同步（含 numericFloatingResetDone 废弃注明、progressStyle 横灌语义）；四步新用户任务（状态条移顶部/找额度未显示原因/搜索改频率/只改桌面样式）待真人装机走查记录误点后闭项

- [ ] **SET-UX-ROW-1 [P1] 模块行/连接行开关位置统一 + 文字不挤压开关（2026-11-24 用户真机报）**
  - **问题** 用户真机截图原话「开关没有统一位置，文字会挤压开关导致开关显示不全」。病灶=左栏模块行（RebuildModuleRows）与连接行（MakeConnectionRow）容器为横向 StackPanel——不约束子项宽度，文字长时把 ToggleSwitch 挤出可视区，副标题长短不一又让各列开关右缘漂移。
  - **怎么做** 行容器改 Grid 三星列（Auto 图标 / * 文字 / Auto 开关）：开关 VerticalAlignment=Center + Margin(8,0,0,0) + MinWidth 40（WinUI 默认开关轨宽），文字 TextTrimming=CharacterEllipsis + TextWrapping=NoWrap；连接行同构改 Grid（* 文字，内嵌 * label + Auto 状态 chip / Auto 开关 / Auto 测试·编辑·删除按钮组）。同病排查其余 24 处横向 StackPanel 逐个判定：表单框行、按钮组、chip 组、radio 内容行均用定宽控件，非本病灶；组件行与动作行属「文字+尾部按钮」同族但开关在行首不受挤压、报障面不在此，只记报告不改（红线：只改行布局）。
  - **怎么验证** CI 硬门两条（visual-evidence 场景 D）：① 12 个带开关模块行开关右缘极差 ≤1px（方差=0 面）且每行开关右缘 ≤ 行右缘−7（完整可见，长副标题不挤开关）；② 6 个连接行开关右缘极差 ≤1px 且不出设置窗右缘。新增锚点 module-toggle-*（ToggleSwitch 自带 peer，AutomationId 即可达，与 pin-toggle-*/conn-toggle-* 同口径）。
  - **红线** 只改行布局，不碰配色/间距 token/其他页；本机编不了 App（NETSDK1100），Core 全绿 + CI 编译/断言绿为门；视觉项待用户装机亲验才闭项（重复报障单，从严）。
  - **证据** 2227955：模块行（Navigation.cs:416）与连接行（Connections.cs:461）容器改 Grid，开关 VerticalAlignment=Center + Margin(8,0,0,0) + MinWidth=40，文字 CharacterEllipsis + NoWrap；锚点 module-toggle-*/conn-toggle-*；nightly run 45536879866 场景 D 双门全绿（12 模块开关右缘极差 0px 全 510、6 连接行极差 0px 全 816），与病根「长副标题挤开关/右缘漂移」正面对撞；装机亲验待用户目检

- [ ] **SET-UX-ROW-2 [P1] 画廊模板按所选应用面过滤或标注适用面（2026-11-25 用户报）**
  - **问题** 用户原话「切换不同的显示方式，对应的设置项也需要对应改变，有些根本不是对应显示方式选项」。病灶=组件中心定向模式里应用位置（桌面/看板状态行/详情）切换只改按钮文案与继承行说明，模板清单不跟着变：清单由数据能力一次生成（SampleGalleryWindow.cs:318 WidgetStyleAdvice.List 只吃 dataKinds+targetState，无面维度），选「看板状态行」时列出的仍是桌面 tile 向模板（圆环/数字卡/圆形水位等），部分模板对该位置根本不适用，且无任何适用面标注。
  - **怎么做** 模板注册表补面适用性维度（模板声明适用 tile/row/detail 哪些面，或按几何约束如 MinTileHeightDips 推导）；WidgetStyleAdvice.List 增加面参数，清单按「数据能力 + 当前所选面」双过滤；对该面不适用的模板置灰并给原因（沿用「不可用：…」文案口径），真正不支持的不可点；面切换时清单即时刷新。
  - **怎么验证** UIA：同一夹具切三档应用面断言 style-item-* 可用集合随之变化、置灰行原因可读；选 tile 专属模板在 row 面断言不可应用；装机亲验各面行高与裁切。
  - **红线** 只动过滤与标注，不改模板树生成与渲染语义；本机编不了 App，Core 全绿 + CI 编译/断言绿为门；视觉项待装机亲验闭项。
  - **证据** 2cc66a2：DisplayTemplates 增面高度预算 RowSurfaceHeightBudgetDips=40（口径取 CI 实证：单行族 32-34DIP 行内可承载、48/60DIP 多行结构塞不下）；WidgetStyleAdvice.List 增 surface 参按「数据能力+当前面」双过滤；SampleGalleryWindow.PopulateStyleCatalog 七行全建、IsEnabled=advice.Ok（面外超预算置灰带原因）、面切换 RefreshStyleCatalog 重选首可用；UIA 锚 style-item-* 不变；Core 测试 1418/1418 全绿；场景 SF3 断言「row 面 number 条目置灰+圆环可用」；各面行高与裁切待装机亲验。

- [ ] **SET-UX-ROW-3 [P1] 桌面显示页按状态条选档联动显隐（2026-11-25 用户报）**
  - **问题** 桌面显示页状态条三选一切到「关闭」或「右下角胶囊」后，「窗口与看板」区仍五项全显：桌面组件布局/数值悬浮框/面板贴边收起是悬浮岛（面板与独立窗）家族专属（PinnedHostWindow/FloatingHostWindow 消费），窗口不透明度作用于悬浮面与胶囊（App.xaml.cs:316 四宿主同值）——选「关闭」时这些项与当前形态无关，选「胶囊」时岛家族项也不适用（原型静态稿本身未做显隐，属用户新提出的交互要求）。
  - **怎么做** BuildDesktopPage 按 StatusBarMode 条件渲染：关闭=悬浮面整区隐藏只留看板开关；胶囊=隐藏岛家族三项；岛=全显；SaveStatusBarMode 已有广播链，切档后重建本页（保持滚动位与焦点不丢）。
  - **怎么验证** UIA 三档各断言可见控件集合（关闭档 desktop-layout-box/numeric-floating-toggle/edge-collapse-toggle/desktop-opacity-slider 不在 UIA 树；胶囊档仅岛家族三项不在；岛档全在）且切回原档全显恢复；CI 场景 D 增补断言或单开场景。
  - **红线** 只动显隐与页重建，不改各设置项自身语义与保存链；本机编不了 App，Core 全绿 + CI 编译/断言绿为门；视觉项待装机亲验闭项。
  - **证据** 1ab67ae：BuildDesktopPage 三档条件渲染（关闭=只留 board-toggle + 隐藏原因 Hint；胶囊=隐藏岛家族三项、不透明度+看板仍留；岛=五项全显）、未建控件显式置 null；SaveStatusBarMode 延后一拍重建本页并还原滚动位/焦点；CI 场景 W4 种子关闭档断言四项在树外+看板在树、切岛档回树；SU2 三档显隐集合逐档断言+切档后重取失效 radio；SU3 先切岛档再走四件；SG 种子播岛档保三路字段跳转锚点；Core 1418/1418 + ParseFile 0 错；视觉观感待装机亲验。

## MVP 待验/未修项（50 项 = 50 勾 / 0 未勾；未完成项即装机清单；2026-11-13 审计发现原头 42/8 与实数 43/7 漂移，本头按实数+Toast 改未勾重算）

- [x] 双击 exe 二次启动不重复进程，既有实例收到唤起气泡提示（场景 E1 CI 代验 + 2026-10-17 真机双半实证，TrayIconService.cs:99-110）
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出（场景 E2/E3 已代验）
- [x] 跨 DPI 拖动无错乱【2026-11-21 用户令按序清任务闭项授权：原卡点=缺双 DPI 屏未跨屏实测，非独立验证】：需真实双 DPI 显示器，当前环境缺（位置/普通拖动已有场景 R/R3 证据 38750239155）
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
- [x] Toast 发送、点击、激活路由实机验证【2026-11-21 用户令按序清任务闭项授权：原卡点=点击激活 BLOCKED（imac 压横幅），发送/呈现已 PASS，非独立验证】：发送+呈现半 2026-11-06 openclaw 实测 PASS——二次启动→Balloon dispatched（beacon-20261106.log 13:12:47）→横幅实拍 `openclaw-20261106/banner-t2.png`「Beacon 已在运行。」+通知中心两条 Beacon 通知 `notification-center.png`；AUMID 自愈已有场景 F5t 证据 38494525200；本机补强批（GLM Failed 态 OS 横幅 PRESENT 12:04→MISS 12:05 相邻帧 + 通知中心两条在栏 + 详情窗 Open→浏览器落 bigmodel.cn/usage，裁剪版归档 `bm2-toast-tray/`，台账 2026-11-08-bm2-toast-tray.md）；**点击→激活路由本轮复现：Failed+无效 detailUrl 状态已本地 mock 稳定触发并投递 OS（无 403 依赖），imac 系统侧压制横幅显示（直发注册 AUMID toast 亦无横幅）→点击步 BLOCKED，原因与所需动作见 docs/acceptance-evidence/2026-11-08-toast-activation.md（真人点击在能显示横幅的机器上验，装机清单面 12）**；2026-11-13 界面审计改未勾：点击激活半 BLOCKED 未闭，不整条勾
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 规则引擎表驱动单测含冷却（NotificationEngineTests 17 例）
- [x] 注入 Failed 状态收到且仅收到一条 Toast（NotificationEngineTests 17 例 + 场景 F5 恰 1 条硬断言）
- [x] 托盘图标颜色/状态像素与刷新周期实机验证：2026-11-06 openclaw 实测——同位两态像素差分 Success 绿 RGB(62,185,80)↔Warning 琥珀 RGB(209,152,33)（其余托盘图标逐像素不变，`openclaw-20261106/tray-green.png` vs `tray-e2.png`）；刷新周期 30s 档实测（state-logtail.txt 两次刷新间隔+Last update 随刷新走）；图标在托盘可见区直接显示不进溢出；tile 更新已有场景 G7a 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 断网显示 Offline+时间；计数与实际 Widget 状态一致（场景 F2/O/Q 三面已代验）
- [x] 打开 <100ms（缓存优先无网络等待）；键盘可完整操作关闭（BeaconRuntime.cs:27,113 + QuickPanelWindow.cs:116-118，run 38743014862）
- [x] 计数/事件与状态一致；右键 Pin 落库（场景 O/S/X1 已代验，run 38590456994）
- [x] 从 L2 事件一键打开对应详情；[Retry] 全链路可用（场景 S4 + U1-U5 已代验）
- [x] 跨进程点击穿透/焦点行为实测【2026-11-21 用户令按序清任务闭项授权：原卡点=tile 收起态点击穿透未验（行 69 裁定不自加整窗穿透），焦点/结构面已 PASS，非独立验证】：焦点面 2026-11-06 openclaw 真实点击实测 PASS——点胶囊唤起看板前台不变（fg 保持 wsl.exe，`capsule-test.txt`+`board-opened.png`）、ESC 关面板（RFC §6.2.7 ②真机复证）；结构面 PASS（无 WS_EX_APPWINDOW、状态翻转前台不变，.5 批）；**仍 OPEN：tile 收起态点击穿透到下层窗口场景**（行 69 已裁定不自加整窗穿透）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab（场景 G7a + H WS_EX_TOOLWINDOW 已代验）
- [x] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一（悬浮形态为主 2026-10-18 裁定；场景 H/H6 已代验，38750239155）
- [x] 两种入口（设置开关/右键 Pin）行为一致并持久化（场景 X1-X3 已代验，run 38590456994）
- [x] 非准入类型在两处入口均不可钉（场景 Y1-Y4 已代验，run 38608625892）
- [x] 重启后位置/收起态还原；拔显示器再接回 tile 不丢（PinLayoutMathTests 回归锁 commit b08cbf7；场景 H/P 已代验；拔显示器物理事件待真机）
- [x] 跨 DPI 拖放与降分辨率越界回收【2026-11-21 用户令按序清任务闭项授权：原卡点=需多屏/虚拟显示未验，非独立验证】：需多显示器/虚拟显示驱动
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈（场景 F3 对话框数 0 + F4 恢复 90s 自愈已代验）
- [x] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出（场景 V1/V2/V4 已代验，38459944253/38661655290）
- [x] RFC §6.2.7 四项真人操作与真实网络环境验收【2026-11-21 用户令按序清任务闭项授权：原卡点=真人 6 面待装机，①②④已 PASS，非独立验证】：②不抢焦点 2026-11-06 openclaw 真实点击复证（点胶囊唤起看板前台不变，`openclaw-20261106/` 批）；①下钻 PASS（UIA Invoke 时间线行→L3）④不进任务栏/Alt-Tab PASS（.5 批）；③动作面清单已补（14 面逐条动作定义+铁律对照+缺口，`docs/acceptance-evidence/2026-11-08-l0-action-surfaces.md`）；真人操作仍待装机（6 面清单见该文档）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] tile 与胶囊改色/重置托盘图标同步：2026-11-06 openclaw 实测——`severityColors.warning=#ff00ff` 重启后 warning 态托盘圆点渲染品红 RGB(206,0,206)（`openclaw-20261106/mag-tray-8.png` 16 帧稳定），删键重置后默认色表恢复（`tray-after-reset.png`）；B-706 运行时绘制链真机复证；改色/重置已有 G4/G5、38953986133 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 某 Widget 设 colorOverride 仅该 tile 变色，优先级正确（场景 G1 已代验）
- [x] 非法颜色值回退默认色不崩溃；重启后保持（场景 G2 已代验）
- [x] Success 到 Error 状态变化有平滑变色过渡（reduced 档）（场景 L1/L2 已代验，38284623093）
- [x] 视觉观感与强度裁量真人验收【2026-11-21 用户令按序清任务闭项授权：原卡点=需真人工作片段观察，档位/帧差分已有 G6/G7b/G7c/V2，非独立验证】：档位/强度/帧差分已有 G6/G7b/G7c/V2 证据
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 动效不抢焦点、不发声；隐藏/收起时无动画循环（场景 H1 + AudioApiAbsenceTests 全源 206 文件断言 + 循环面代码审计；真机听声目检可装机复核）
- [x] 导出文件全文无 token；导入+重录密钥后行为与原机一致（场景 Z，204b884，run 38725138151）
- [x] 真人全流程录屏归档【2026-11-21 用户令按序清任务闭项授权：原卡点=需录屏能力与真人，非独立验证】：需录屏能力与真人（CI 截图已有 visual-evidence 工件）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] B-803 干净 Windows 11 VM 下载运行验收【2026-11-21 用户令按序清任务闭项授权：原卡点=需干净 Win11 VM 本环境无，非独立验证】：当前环境为 Linux 无 Windows VM
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

## MVP 验收标准（master-plan §14，4 项 = 4 勾 / 0 未勾，需真实装机验证）

- [x] explorer 登录会话调度实机验证：Run 键已有 M3 证据 38761946476；登录循环 2026-11-06 openclaw 实测——注销（4647 @12:39:31）→交互登录（4624 type=2 @12:56:37）→explorer 9392 拉起 Beacon pid 208（logon-probe.txt：`beacon=208 parent=explorer(9392) session=2`，命令行即 Run 键值）；证据 `docs/acceptance-evidence/openclaw-20261106/logon-probe.txt`、`2026-11-06-openclaw-realmachine.md` §1（首条探测 beacon=none 系 ONLOGON 探测早于 Run 键发射，如实记录）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 更长时稳定与胶囊视觉观感实机观察【2026-11-21 用户令按序清任务闭项授权：原卡点=需一个工作日驻留，15min 浸泡已有 E2/E3/AD，非独立验证】：需 Windows 实机持续运行（托盘驻留与 15min 浸泡已有 E2/E3、AD 证据 39057049259）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] 全局快捷键正常打开悬浮面板（场景 K1/N + WM_HOTKEY 直驱硬断言已代验）
- [x] 真实 GitHub PAT 与目标仓库全链验收【2026-11-21 用户令按序清任务闭项授权：原卡点=需用户凭据未验，非独立验证】：待用户提供凭据或明确授权（凭据类输入，非测试机缺口；三台 Windows 真机 openclaw/imac/.5 在位）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)

## 用户四单（2026-10-14，4 项全勾）

- [x] 事件看板默认全部 + 点击过滤且过滤态可见（活动 chip 实心高亮 + 概览头带源；e905a48+23660f8，1078 测试绿，run 38549671960）
- [x] 看板中文 + 设置页语言切换（UiStrings en/zh 双字典唯一机制，即时生效落盘；e905a48，1078 测试绿，run 38549671960）
- [x] 悬浮框边缘坍缩加开关（默认开）+ 收起态点击细条粘性展开（e905a48，1078 测试绿，run 38549671960；2026-11-13 审计注：坍缩拖拽与细条点击半无取证通道，待真机走查）
- [x] 托盘 Open Beacon 与 Notifications 语义分化（意图出处 B-503 = mvp-issues.md:266；e905a48，run 38549671960）

## 外部评审 2026-10-22 展示组件选择机制落地（2026-10-23 立，4 项 = 4 勾 / 0 未勾）

> 来源评审：docs/review/2026-10-22-display-component-selection.md；裁定：先架构闭环再扩模板库，跟踪表 12 项清零前不新增模板种类（细目见评审文「落地跟踪」表）。

- [x] P0 架构闭环第一批：空状态断链/旧对象覆盖/52%% 双百分号/样式恢复/槽位化替换（f9a9aac，DisplayTemplateTests 905 绿，CI run 40599680681）
- [x] P1 架构闭环第二批：缺失值区分 + 能力匹配 + 语义描述（f9a9aac）
- [x] P1 第三批：DetailWindow 完整源映射渲染 + 组件中心实时预览与参数表单（f9a9aac）
- [x] P2 收尾【2026-11-21 用户令「按序清任务」闭项授权】：模板感知尺寸防裁切真机确认 + 全局 progressStyle 与实例样式优先级统一（代码半已落 f9a9aac，裁切观感装机复核）。2026-11-08 收口=imac 真机探针（数字卡模板 48 档 vs 32 对照同框）确认防裁切生效，探针顺带抓出并修掉两缺陷（槽位面列定义缺一叠印 ff708ec、caption 无截断硬裁 c7b0cb2，CI 42400083245/42400558637 绿），全局 pool 下实例 bar 生效实证，证据 docs/acceptance-evidence/2026-11-08-p2-tile-sizing.md；待用户装机确认。2026-11-13 界面审计改未勾：观感半亲验前不闭项。
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- 验收口径：P0 各项须有 Windows CI 场景或等价端到端断言，不得只跑 DisplayTemplates 纯函数单测收工。

## 文档双语补全（2026-10-16 用户令，2 项全勾）

- [x] docs/ 根目录实质文档 16 篇全部成对（13 篇完整译本 + master-plan/mvp-issues/mission-excerpt 3 篇过长留「暂无译版」说明；本目录链接 0 断链，mkdocs build --strict 绿；b3f32e0）
- [x] docs/design/ 7 篇与 docs/rfc/ 1 篇双语配对（7 篇完整译本 + redesign-plan-2026-10-16 留说明；23 对行数/标题/表行/围栏计数全等；mkdocs strict 绿；51492ae）

## 组件化重构分期（依 docs/design/redesign-plan-2026-10-16.md，43 条 = 43 勾 / 0 未勾）

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

### Phase 6 回归与真机验收（6 条 = 6 勾 / 0 未勾）

- [x] 动作端到端命令执行半（场景 AF run 40903469089，cf43001；URL 开浏览器半归 A-2 BLOCKED）
- [x] 验证多窗口订阅与取消订阅（EventBus 5 例 + 三窗 provider 测试；场景 AB run 39057049259 全绿）
- [x] 重启恢复的显示器切换与 DPI 拖动【2026-11-21 用户令按序清任务闭项授权：原卡点=需第二屏+解锁 GUI，位置/收起还原已有两轮实机，非独立验证】：需第二块显示器与解锁的 Windows GUI（位置/收起还原已有两轮实机记录）
  已代决:按 docs/review/2026-10-30-decisions.md 执行(2026-10-30)
- [x] HTML 安全边界与错误恢复（HtmlTemplateParserTests 62 例 + Sanitize 4 例；样例导入 CI run 39043157904 全绿）
- [x] 验证缓存、断网、凭据错误与操作失败（四支路测试面代验，1328 全绿，de528f9/75f4a85）
- [x] 验证现有 Provider、AI 额度显示与通知规则不回归（Connections 662 全绿 + 真机 356 次刷新零失败，de528f9/75f4a85）

## 面板升级看板（2026-11-05 用户令）

- [x] PANEL-UPGRADE-1【2026-11-21 用户令「按序清任务」闭项授权】 [P1] 快捷面板升级为看板：默认尺寸放大（目标 ~720x560，可调可记忆），事件动态（时间线）成为主区——时间/级别/来源/摘要逐行可读，条数放宽（现 8 条上限重新评估），头部 OverallSummary 与来源过滤保留；悬浮岛/胶囊点击唤起目标同步指向看板；字体与行距按可读性重排；设置项若涉尺寸记忆走 shell-state 惯例。2026-11-13 界面审计改未勾：方案 B 2026-11-11 落地后无真机证据，待真机走查+用户亲验。
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

- [x] AQUA-001【2026-11-21 用户令「按序清任务」闭项授权】 额度显示方式第三档「整行水族箱」（2026-11-15 用户令：进度条横贯 tile 整行宽、位于文字行下方；填充区做水族箱质感——波纹水面持续缓动（活水）、水位高度=用量比例、填充色用既有档位配色；波纹幅度小频率慢不抢眼、motion intensity=off 档静止；设置页「额度显示方式」下拉加第三项三选一，切换即时生效走 SettingsApplied 链）：落地=220f6cd（Core AquariumWave 正弦采样+无缝循环数学+AppConfig progressStyle aquarium 档+设置页下拉第三项）/ da7437e（PinTile 整行 12DIP 水族箱带渲染：底轨+水体+前后双层波、BarFillColor 档位色单源、motion off 档静止）/ b54a5cd+ac7a8f3+9e90454（渲染三轮收口：Core 周期对齐采样修末段漂相、pool 已证形态（显式宽高+非负几何）、crest 提出水面线（几何 Y=crestY+A 非负、水位定位走 Margin=waterlineY−A））/ 83998ef（nightly 场景 AI 四阶段取证：p20/p60 mock 档+Write-ScenarioConfig appearance 注入+四态断言）。证据：Core 1312 全绿（AquariumWaveTests 7 例锁点数/周期对齐/无缝循环/振幅相位/慢波量化锚）；nightly run 43755252181 全绿（场景 AI 20/60/95/off 四态带像素 558+水体档位色+crest rows 露出断言全过；双帧差分 0 按 G7c 口径降级转装机目检）；本地逐行像素复核三态水位与波峰位置全吻合几何预测（20% 首色行 DIP 8.0=9.6−1.6 波峰顶、60% DIP 3.0≈4.8−1.6、95% 首行 0=Clip 裁顶预期）；12 件截图+口径入 docs/acceptance-evidence/aquarium-style/（run id+commit 链+断言表）；波纹活水观感与水位视觉正确性待用户装机亲验，亲验前不闭项。**2026-11-17 用户令横向纠偏（「我说的注水桶应该是横着的」「就是整个就是注水桶」——整个 tile=横放水桶、水从左往右灌满全高桶身、水位前沿=竖直线 frontX=进度×桶宽、前沿波纹沿前沿纵向漂移；竖直涨水的旧 aquarium 渲染作废）**：e022b1d（PinTile 横灌渲染=水体改左锚全高动 Width、波层改竖直前沿条（Core Crest 轴交换 VerticalWaveFigure、Margin=frontX−A−skirt 非负域）、Host 铺满全高+桶身 Clip、MotionEngine StartRipple/StopRipple 加纵向轴）/ 58b2532（前沿振幅 1.6→3.0 DIP=桶高 32 的 1/10 仍「小幅」，兼 CI 列采样波峰全色可证下限；量化锚测试 Constants_SmallAmplitudeSlowPeriod 重定标 ≤3.0）/ 0cd5413（场景 AI 断言重推=全高列采样：前沿后方 12 DIP 水体 ≥30% 列面积+前方 8 DIP 空区 ≤2%+前沿条波峰 ≥4px+桶身整板双帧差分；旧 12DIP 带断言/带裁剪助手全废）；设置页第三项标签改「注水桶（整桶横灌：水面从左往右推进=用量）」并补 appearance-progress-style-box 锚点、configuration.md 双语 progressStyle 行补 aquarium 横灌语义（原两行只写 bar/pool 的欠账一并清）、configuration-effects.md 双语补第三档说明；2026-11-17 nightly run 43963531775 全绿（head 923adb6，Visual evidence+Publish 双 success）：场景 AI 三态+off 列采样全过（后方水体 81-86/28+、前方空区 0/1−、前沿波峰 6/4+；front=57/153/237px 逐态吻合 floor(桶宽×fraction)+10 钳位公式；差分 0 按 G7c 降级，但波层在场已由前沿条硬断言钉死、降级只剩「动没动」一义）；像素三重互证（CI 日志 front ↔ 全屏水体行程 48px=fillWidth47+抗锯齿 ↔ 镜像公式）；证据档重写 17 件入 docs/acceptance-evidence/aquarium-style/（旧 12DIP 带断言与截图退役），装机亲验面：竖直前沿波纹手感、95% 漫水可读性、空桶 tint 观感
- [x] SET-INST-001【2026-11-21 用户令「按序清任务」闭项授权】 · P1 设置项改完无需重启/无需开关窗口即见效果（2026-11-15 用户令「设置之后没有立刻生效」）：全仓复查 SettingsWindow 15 处 SaveApp()/SaveActions()/UpsertWidget 写点，逐点判定广播面——3 处真缺口已修（Actions.cs:207/237 动作保存与行内删除只落盘不广播、Appearance.cs:229 密度档只落盘不重建宿主），另 3 处「有意不广播」写点补理由注释（Navigation.cs:92/129 窗口几何、PinnedHostWindow PersistLayout 与 FloatingTileWindow PersistPosition 拖动手感，宿主内部已当场施加）。广播矩阵（SettingsApplied→ApplySettingsEffects 重建托盘/胶囊/L0 两宿主、PinsChanged→ReloadTiles+Republish+岛重算、ReloadWidgets→调度重建+首拉）逐写点核对无漏网。证据：24dbd08（三处补广播）+ 6af1ae9（回归 SettingsImmediateEffectTests，Core 5 例锁按钮集当场增删/密度档三档取值+fail-open，Storage 3 例锁动作增删对同一 store 实例当场可见即下次开窗生效），Core 1282 / Connections 1017 / Actions 55 / Storage 62 全绿；用户可感知面（设完动作下次开详情窗即见按钮、切密度桌面 tile 即刻重排）待装机亲验
- [x] BUG-1【2026-11-21 用户令「按序清任务」闭项授权】 概览 OpenCode 品牌图标渲染成实心方块：BrandIconFactory.cs:67 强制 FillRule.Nonzero，而 BrandIcons.cs opencode path 是同向双矩形镂空「0」形（nonzero 下内框 winding=2 被填实）——修复=工厂统一 FillRule.EvenOdd（Simple Icons 无自交图形，even-odd 等价），Core 补解析单测断言镂空语义（SvgPathParserTests.Parse_OpencodePath_IsNestedDoubleRectangle，948 Core 绿）（2026-11-05 用户截图报障；3443ad3，CI run 41956471135 绿；观感复核=装机后概览选中 opencode 来源卡看「0」形镂空）；2026-11-13 界面审计改未勾：观感复核待装机亲验
- [x] BUG-2【2026-11-21 用户令「按序清任务」闭项授权】 概览来源卡文字截断：QuickPanelWindow.cs:363（chip 标签）与 :481-482、:548（MaxWidth=260 + CharacterEllipsis）在窄窗三列布局下把来源名截没（用户截图「文字都显示不全」）——修复=来源卡标签换行不省略+卡片 MinWidth=96+整卡 Tooltip 全名，通知标题与状态行摘要改换行优先（260 只作换行边界）（2026-11-05 用户截图报障；e3a8b3a，CI run 41956565696 绿；UI 截断不可单测，人工验收=装机后窄窗拖小面板看来源卡全名换行+悬停 Tooltip）；2026-11-13 界面审计改未勾：观感复核待装机亲验
- [x] 悬浮岛 CapsuleWindow【2026-11-21 用户令「按序清任务」闭项授权】（display-modes-analysis §7 Phase 2，2026-11-05 用户单）：config.capsuleIsland 展示形态开关默认关保持现状；开=顶部居中锚定（CapsuleIslandMath 纯函数 6 例 Core 锁口径）+ 收起态=状态环+未读/异常徽标+品牌标 + 点击唤起看板（§1 只读契约不变）+ 拖动位置独立持久化（shell-state.json islandLayout，与经典胶囊布局互不覆盖）+ 设置页开关即时生效（capsule-island-toggle，UiStrings 双语）；ace0cdb+9a03daa，1683 测试全绿，CI run 41957917477 绿；岛观感/拖动/徽标不可单测，人工验收=设置开「悬浮岛形态」看胶囊顶部居中+徽标随未读/异常变色、拖动重启还原、点击唤起看板；2026-11-13 界面审计改未勾：观感待装机亲验
- [x] 悬浮岛回归（UIA 挂死）修复【2026-11-21 用户令「按序清任务」闭项授权】：CapsuleWindow 无条件 new+Activate——showCapsule=false（CI 各场景缺省口径）也常驻一个置顶工具窗干扰 UIA，nightly Visual evidence 两次无进展被人肉取消（41958504066/41963265872；日志复核两跑各场景实际持续推进，取消点分别在 AB3 静默窗与 AF→J 过渡的静默间隙，非真挂死，但无护栏则永无出口）——修复=①胶囊窗懒实例化（App.xaml.cs EnsureCapsule 工厂：仅 ShowCapsule=true 建/显窗，设置页开关运行时切换即建/关，建窗即回放 lastStates 来源预览+聚合快照+未读徽标初值；CapsuleIsland 仍为纯形态开关不独立建窗，构造函数本就不激活/置顶，Activate 留在显式路径；WinUI 定赋值穿透局部函数链 CS0165 两轮 CI 实证：settingsWindow/hotkey 前置声明）②visual-evidence.ps1 总超时护栏（分离 watchdog 缺省 4800s=80min 强杀本脚本进程，BEACON_EVIDENCE_TIMEOUT_SEC 可调；脚本含 15min 浸泡全程实测 ~72min，不能按 15min 设）+ 开场 Beacon 窗口基线与 Stop-Beacon 残留取证落日志（a517429/55e5bc8/2a989dd）；验证=CI 41973057764 绿（1683 测试）+ 手动 nightly 41973341778 全绿（Visual evidence 步 success，132 场景断言全过含 AD 浸泡/AF/J，日志见「总超时护栏已武装」「窗口基线[script-start]」，05:22:47→06:34:26 共 71.7min 于护栏内自然完成，run 结论 success 非 canceled）（2026-11-05 悬浮岛 commit 后回归；人工验收=装机开 showCapsule 看胶囊照常右下角、设置关胶囊后置顶窗彻底消失）；2026-11-13 界面审计改未勾：缺陷修复半 CI 已验，观感半待装机亲验
- [x] DOC-GIF：为全部「开启后有效果」的开关/配置项生成 off/on 对比图与切换 GIF，落 docs「配置效果」双语页，流水线可重跑兼作回归（kode todo C-24 同款，2026-11-01 用户令全舰队推广）——2026-11-06 落地：9 组有画面效果的开关成对静帧 + 9 张 GIF（theme / pinDisplayMode / progressStyle / motion.mode / density / severityColors.warning / uiOpacity / capsuleIsland / colorOverride），页 `docs/configuration-effects.md` + `docs/zh/configuration-effects.md`（mkdocs 双导航），原帧 `docs/img/config-effects-src/`（25 文件 + manifest）；流水线 `tools/doc-gif/capture-config-effects.ps1`（Windows 抓帧，PrintWindow 窗口级）→ `tools/doc-gif/assemble.py`（任意机器合成），本轮两段各跑通一次（含 -Only 补跑与基线还原）。图外说明：`launchOnStartup`/`hotkey`/`pollIntervalSeconds` 效果是注册表值/热键/节奏（已在页内指向验收证据）；`keepQuickPanelOpen`/`edgeCollapseEnabled` 为行为类且 edgeCollapse 需真实拖拽，待解锁会话补图。
- [x] USER-RPT-1【2026-11-21 用户令「按序清任务」闭项授权】 悬浮岛视觉重做：2026-11-06 用户实测「简直无法看」——岛形态从未做过真机视觉验收即报完成。处置=deleg_b1bba043 在飞（.5 实机截图诊断→按 Dynamic Island 观感标准重做→前后对比证据→CI 绿→push）。验收口径：用户装最新包亲眼确认可看，才算闭。2026-11-08 收口=a413ec0 一次修完五缺陷（看板 DI 回归/贴顶/收起高度残留/影子暗带/启动闪帧），imac 200% DPI 十件真机取证+前后对比（收起窗 482×86→482×70 贴顶平齐），证据 docs/acceptance-evidence/2026-11-08-island-head-closeout.md，CI 42396968796 绿；待用户装机确认。2026-11-13 界面审计改未勾：亲验未发生不闭项。
  - 交付（2026-11-06 续）：patch 落源（CapsuleWindow.cs/.xaml+ThemeColors.cs+MotionEngine.cs：近实色黑药丸+发丝白描边、健康环退场单灯点+晕、徽标异常取色压过未读、悬停展开 MorphIn 形变；评审修复两处=环缓存恒更新防切回陈旧环、MorphIn 限岛形态保经典逐字节不动）；.5 真机（10.240.0.5）现场 build 0 错误 + test **TEST_EXIT=0**（同 patch 基线 1670/0）+ publish self-contained（13:35 `publish-island`，标记门禁防无补丁假构建）；三态真机取证最终构建实际截取——收起 135×39 顶中、error 注入徽标 蓝→红、SendInput 悬停展开 162×94 顶缘锚定向下长出+收回复核 135×39，附看板唤起 736×576；vision 自查三态全过（暗底白字对比恒定/单色故事/行内三级对比清晰）。证据 `docs/acceptance-evidence/2026-11-06-island-visual.md` + `island-visual-before-after.png` 等十件。待用户装机亲验后闭。
- [x] USER-RPT-2【2026-11-21 用户令「按序清任务」闭项授权】 整体显示不正常：2026-11-06 用户实测显示异常（具体面板/形态待实机截图定位）。处置=随 USER-RPT-1 的实机取证一并排查（最新构建+看板方案 B+岛形态三态截图逐一核对）；找出全部显示缺陷一次性修，不再挤牙膏。2026-11-08 收口=实机诊断定位五缺陷一次修完（a413ec0，含看板打不开这个最大异常面），四态+看板+经典对照全取证无新增异常；待用户装机确认。2026-11-13 界面审计改未勾：亲验未发生不闭项。
- [x] USER-RPT-4【2026-11-21 用户令「按序清任务」闭项授权】 悬浮岛效果错误：2026-11-06 21:5x 用户在已含 65dc2ac 重做的最新 Nightly（13:15 UTC 构建晚于重做提交）上实测「悬浮岛的效果是错的」——重做版视觉仍不达 Dynamic Island 观感标准，用户否决。处置=实机截图取证定位具体错面（形态/位置/动效/徽标/展开行为逐项）→ 修正 → 三态前后对比证据 → 用户亲验闭项。2026-11-08 收口=错面定位为不贴顶+影带板感+收起高度残留三项，a413ec0 全修（10x 像素级取证贴顶平齐+肩 16+底角 12+发丝描边），证据同 USER-RPT-1；待用户装机确认。2026-11-13 界面审计改未勾：亲验未发生不闭项。
- [x] USER-RPT-3 流程整改【2026-11-21 用户令按序清任务闭项授权：流程铁律已强化在案（各条目带来源+日期）】：用户反馈的问题未入 todo 即流失（本三条即是补记）——铁律已强化：用户每报一个问题，当场入项目 todo.md 带来源+日期，处置结果回发用户确认。
- [x] ISLAND-RING-1【2026-11-21 用户令「按序清任务」闭项授权】 岛圆环进度徽标 + 组件化两扩围 + 载体统一（2026-11-07 用户令链 03:1x–03:3x 四连发）：①岛收起态要有圆圈进度展示（「人家都有圆圈展示进度的」，BrimDeck 技法学照原生实现）②进度环/进度条收编为组件中心两个组件（勾选即变、无默认样式概念）③岛消费 hiddenComponents（勾掉的组件不进岛的状态聚合）④展示载体（悬浮岛/悬浮框/看板）统一进组件中心勾选，托盘只留设置。2026-11-08 收口核对：①②③④代码面全落（c78200e 交付+d98e674 源头过滤修正），真机证据 island-ring/ 十一件（勾掉故障组件回绿满环/只环/只条/都不勾三态）+ a413ec0 修掉其看板 DI 回归（ToggleBoard 通则四载体唤起面全通，04-board-window.png 实证）；待用户装机确认。
  - 交付（c78200e）：`RingProgressBadge`（底环+进度弧+中心数字，BrimDeck UI.Ring 技法原生落地：12 点起笔 ArcSegment 过半翻转 IsLargeArc+Counterclockwise+Round 端帽+≥.999 整圆；WinUI 无位置构造器/IsStroked 两处 CI 实证修正 e80fa89/1614560）+ `BarProgressBadge`；环/条=组件中心「悬浮岛」虚拟组件（island.progressring/island.progressbar，Core ComponentCenterCatalog 单源恒在册）；岛聚合过滤下沉 StatusAggregator 源头（SetHiddenComponentsProvider+Republish，**实机取证踩坑修正 d98e674**：消费端 lastStates 重投影有顺序竞态抹掉真实 Error，island-trace 04:12:39 raw Error:1→filtered err=0 铁证）；载体三项勾选（岛/悬浮框走旧字段零迁移、看板新增量可选字段 boardEnabled 缺键=true）+ 悬浮形态下拉收编 + 托盘菜单收编「设置+退出」（CI E2/E3 down-count 同步 0/1）+ 看板唤起单门 ToggleBoard。
  - 证据：`docs/acceptance-evidence/2026-11-07-island-ring.md` + `island-ring/` 十一件（imac 200% DPI PrintWindow 窗口级：红弧50%环+红条+中心数 → **勾掉故障组件回绿满环** → 只环/只条/都不勾三态 → 悬停展开行结构不变；并排大图 island-vs-brimdeck-side-by-side.png 重制，BrimDeck 列占位=其条带窗未渲染刘海面已如实记录）；修复后数据链 trace `ok=1 err=1 ratio=0.50 ring=#F85149` + 像素计数 red=829/green=0。imac 现场构建 0 错误+四套全绿（1001/27/673/41，行为锁红先行：IslandProgressComponent/StatusAggregatorHidden/CarrierCatalog Tests）。DPI 教训：非 PerMonitor-aware 进程 GetWindowRect 虚拟化坐标把 PrintWindow 截图裁半。
  - 待用户亲验：装机勾选组件中心「悬浮岛」组看环/条显隐即变、勾掉故障组件看岛回绿、设置→组件中心「展示载体」三项勾选窗口出现/消失，亲验后闭。2026-11-13 界面审计改未勾：证据在、亲验未发生不闭项。**2026-11-21 用户令「按序清任务」闭项授权。**
- [x] NIGHTLY-AB2 nightly 假红收口（run 43134760840 场景 AB2 判据窗 GLM 新行=1 期望 ≥2）：根因=停用期组循环按 skip 退避越睡越久（streak 2 的 60s 段），复订只翻 enabled 标志不打断退避段，恢复刷新 02:15:54 才落地溢出判据窗（artifact beacon-AB2-resume-fail.log 逐行对退避相位）；修复=f9580a7 加 WidgetHost.KickConnection 连接级批量 kick 打断退避等待（WidgetHost.cs:172），conn-toggle 复订支路（SettingsWindow.Connections.cs:387）与模块复订支路（BeaconRuntime.cs:290）两处接线；回归 KickConnection_WakesSkipBackedOffGroup_BeforeBackoffExpires（WidgetHostTests.cs:373，FakeDelayer 退避段永不放行负验门，摘 kick 实测复红）；Core 1275/Connections 1017/Actions 55/Storage 57 全绿，ci 43182921853 绿，nightly 43183551717 AB 三段全绿复证（2026-11-12）。
- [x] LANG-1 文档站语言自动切换缺失【2026-11-21 用户令按序清任务闭项授权：原卡点=G2-2 拍板待定，「只记录不实现」口径，非独立验证】（[P2] 检查项，2026-11-14 fleet-docs-audit-G2 轮，台账 docs/审计-文档一致性.md G2 节 G2-3）：部署站=单语 mkdocs（mkdocs.yml `theme.language: en`），全站 html lang 恒 en——中文页 site/zh/index.html 全页 CJK 609 字而 `lang="en"` 错标（本地 mkdocs-material 9.7.7 build 实测，与 docs.yml 同管线）；`grep locale|html_lang|i18n|alternate` 于 mkdocs.yml+docs/*.md = 0 命中，无 Accept-Language 自动识别，语言切换仅 nav「中文」手动链接。修法依赖 .vitepress 幽灵基建拍板（G2-2：删 .vitepress 收口 mkdocs，或部署栈迁 vitepress——vitepress per-locale lang 原生正确，mkdocs 路线需 mkdocs-static-i18n 插件）；本轮只记录不实现。

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

- [x] ISLAND-D1【2026-11-21 用户令「按序清任务」闭项授权】 [P0] 第一步·选择展示方式：岛收起态展示形态可选（当前仅 CapsuleWindow.StatusFaces.cs:34-66 的 环/条 二选勾）。组件中心岛组扩为「展示方式」选择：单组件常驻 / 多组件堆叠 / 徽标+轮播 等，选项落 AppConfig 并进组件中心 UI（file: src/Beacon.Core/Services/ComponentCenterCatalog.cs:97-107）（cc9b577+89ccaf0：IslandDisplayPlan 三模式 single/stack/carousel + AppConfig.IslandDisplayMode + 设置页「展示方式」下拉 BuildIslandSection；Windows CI 42546551569 绿，Core 1170/Connections 874/Actions 54/Storage 48）；2026-11-13 界面审计改未勾：仅单测与 ci 测试 run 无视觉取证，验收口径 island-data/ 证据不存在，待真机走查
- [x] ISLAND-D2【2026-11-21 用户令「按序清任务」闭项授权】 [P0] 第二步·选择展示组件：组件中心勾选的真实 widget 可「上岛」（当前岛上零真实组件，仅虚拟环/条）。岛组列出全部已连接 widget（ComponentCenterCatalog.Group 已有全量清单）加「上岛」勾选；岛收起态按序渲染所选组件，宽度自适应收起行上限，超出堆叠/轮播（cc9b577+89ccaf0：ComponentCenterCatalog.OnboardCandidates/SetIslandOnboarded + AppConfig.IslandComponents 序 + 岛组「上岛」勾选 BuildIslandOnboardRow；同上 CI 42546551569 绿）；2026-11-13 界面审计改未勾：同 D1，无视觉取证待真机走查
- [x] ISLAND-D3【2026-11-21 用户令「按序清任务」闭项授权】 [P0] 第三步·对接数据源：岛组件吃真实数据流（当前只接 Notifications.UnreadCount，App.xaml.cs:228）。26 个连接 Provider 的 widget 数据（claude/codex/copilot/kimi 用量、deepseek/moonshot 余额、herald 告警、http.status、jenkins.build、machine.net 等）按现有刷新节拍喂进岛组件渲染，数据为零/断连的组件按 data_quality 口径显示占位不造假（cc9b577+89ccaf0：App.xaml.cs RecomputeIsland 喂真实 widget 状态节拍 + IslandDisplayPlan.Compute 对空/断连/陈旧出占位 chip 不造假；同上 CI 42546551569 绿）；2026-11-13 界面审计改未勾：同 D1，无视觉取证待真机走查
- [x] ISLAND-D4【2026-11-21 用户令「按序清任务」闭项授权】 [P1] 后续行为：点岛上某组件→展开看板并定位到该组件详情/配置（当前只有整岛点击开看板，无组件级下钻）；critical 数据组件点击直达对应处置入口（89ccaf0：RouteIslandClick 组件级下钻 + IslandWidgetActivated + QuickPanelWindow.RevealSource/OpenIslandTarget；同上 CI 42546551569 绿）；2026-11-13 界面审计改未勾：同 D1，无视觉取证待真机走查
- 验收（用户可执行）：勾选 ≥2 个真实组件上岛 → 岛收起态截图显示真实数据（非恒 0 计数）→ 点组件跳对应详情；证据入 docs/acceptance-evidence/island-data/

## 悬浮岛灰板修复（2026-11-16 用户令执行巡检单 beacon-island-backdrop-fix 2026-10-20，1 项 CI 已取证 / 2026-11-21 用户令「按序清任务」闭项授权）

- [x] ISLAND-BACKDROP-1【2026-11-21 用户令「按序清任务」闭项授权】 岛态窗矩形内曲面外月牙区露灰蒙底（用户实测「岛周围一层灰色底板」）：方案 A（c4007ce 岛态摘 SystemBackdrop）被 CI 像素证伪（四角 765=3×255 满值=黑背板非壁纸，WinUI3 窗背板露底）；方案 B=SetWindowRgn 真镂空（a404049 窗裁曲面轮廓 + 2349493 region 量窗矩形 + 44d78ec 框架落定后重建+几何去重键防 SetWindowRgn 自触成环）；判据 AH5 三段=岛顶部肩月牙 vs 窗外同高参照 ≤24 / 经典四角 ≥40 / 回切岛 ≤24（fbcad6f+41da77d 修两重采样假红：外框顶出屏+客户区角点在轮廓多边形内、CI 桌面宿主终端窗占屏致底部背景非壁纸）；nightly 43898913432 全绿（AH3 组件下钻 Invoke 首绿 + AH5 三段 + 发布步 success），像素复核月牙=壁纸白、岛内部=GLM 环绿非黑；副产修复三件=Progman/WorkerW 免避让（9acfd0b，前台翻桌面岛隐没真机缺陷同根因）/ TOPMOST band 重钉（3906815，SetWindowRgn 降 band 症）/ 岛面 InvokePattern（d40e2a9，读屏可达性+取证通道兼修）；证据 docs/acceptance-evidence/island-backdrop-fix/；亮暗壁纸观感/运行时 10 次循环切换/切换瞬态闪烁/DWM 阴影观感待装机亲验

## 岛视觉精度修复（2026-11-18 用户真机三瑕疵巡检单 beacon-island-visual-precision，1 项 CI 已取证 / 2026-11-21 用户令「按序清任务」闭项授权）

- [x] ISLAND-EDGE-1【2026-11-21 用户令「按序清任务」闭项授权】 悬浮岛边缘视觉精度（用户真机三瑕疵：透明或灰色底板残留、圆角不自然、强锯齿）：走首选路线 ULW 逐像素 alpha 环带窗（新增 IslandEdgeBandWindow，纯 Win32 分层窗沿岛轮廓外扩 4px 自绘黑环带，岛体窗 region 与 XAML 零改动；c566b2a/7a87dce/fb9afe7/b8f5396/96f0ba6/e70db22/4c84c48）；CI 两因连环定谳=band 位置陈旧（纯移动不触发 region 重算，e5c50b9 挂 DidPositionChange 跟挪并缓存几何段表/DPI）与 TopmostGuard 反压（哨兵把岛钉到 band 顶，21b3a58 把 band 注册为哨兵第二个 client 同事件先钉岛再钉带）；其间一轮被「黑对黑」陷阱洗白（陈旧 band 底缘核心段全宽直线恰压岛体黑面故隐形，只漏外沿 AA 行 2px 斑；z 序错与位置陈旧给出完全相同的斑，像素差必须换算到候选源各自理论落点核对几何全形）；AH5d 三 advisory 升硬门（28cd9de 升门 + 2db0e56 修悬空 else，PS 解析期合法只在 5.1 运行期炸）=底缘灰带≤1px / 曲线色阶≥4 / band 几何对齐岛 clientRect−4px；像素直证 AA=main 左缘 y2 硬跳 255×8,17,1（色阶 3）vs spike 连续灰阶 191,135,127,88,24,0（色阶 7）；nightly run 44855478510 全绿（head 2db0e56，Visual evidence 与场景 AH5d 双 success，实测灰带 0px、色阶 7/9/10、band rect=374,-4 276x40 对齐 island 378,0 268x32 偏移 0，发布步按门控 skipped）；用户真机三瑕疵亲验待装机（视觉条目 agent 不闭项）；合并后 main 头 086c145 = 2db0e56 + todo.md 4 行（纯台账），而 2db0e56^{tree}=b84fcb9 与已验 run 的 head 5bb308a^{tree} 逐字节同（rebase 只换 hash 不换树）→ 首个 main nightly 代码/workflow 内容即已验内容，无新风险面

## 悬浮岛底部残留 + 空白悬浮条（2026-11-24 用户真机报——岛窗口面问题用户已多次报障：灰底→EDGE-1 锯齿→本条，修复必须走到用户亲验为止）

- [ ] ISLAND-BOTTOM-1【2026-11-24 用户真机截图报障，已派 beacon 席执行中】悬浮岛底部边缘没处理干净 + 桌面出现一个空白悬浮条（用户原话「悬浮岛底下没有处理干净，还有一个空白的悬浮条」）。
  - 用户截图 vision 定性（2026-11-24）：空白条=纯白不透明、大圆角胶囊、与主岛同排分离、无任何内容——**未透明化的空壳窗口**（ULW alpha 整帧失效），非描边带内容残留；主岛底部边缘收起态截图看是干净的，残留疑在展开态/其他 DPI。
  - 取证三方向：① IslandEdgeBandWindow.Update 位图清零/alpha 预乘路径（Windows/IslandEdgeBandWindow.cs）；② 空帧 Show 门控哨兵（CapsuleWindow.IslandChrome.cs:251 注释段是否真堵住空帧 Show）；③ 白条在经典胶囊态是否复现（判岛态专属还是共用窗口路径）。
  - 修复方向按取证定：底部残边=ULW 逐像素 alpha 整岛化或 Rgn 内收 1px+XAML alpha 边缘带补偿（根治路线见 skill 记录）；空白条=修门控与位图失效逻辑。
  - 取证①②③结论（2026-11-24）：① 位图清零/预乘路径本身正确（CountInk/Premultiply 逐像素 alpha 无误），空壳成因不在绘制侧；② 空帧门（6f52775）只堵住 Update 自身路径——两处回场 Show（CapsuleWindow.SetVisible、FullscreenAvoidance 避让回场）无门控，分层窗从未成功 ULW 时 ShowWindow 即未透明化空壳窗，正是真机「纯白不透明胶囊条」；③ 经典胶囊态可复现（SetVisible 回场路径与岛态共用，_ruleHidden 残留岛态 true 时恒进回场分支），非岛态专属。
  - 修（18a1e8d）：IslandEdgeBandWindow 增 _hasFrame（仅成功 ULW 非空位图后置位），Show 无位图原样返回、IsAlive 同步要求 _hasFrame（TopmostGuard 不再把空壳钉回可见）；两处回场 Show 加 _island 门；RestoreWindowRegion 清 _lastBandSegs 缓存（经典态 SyncEdgeBandPosition 不再用旧岛轮廓重钉）。
  - CI 验收面：AH5e 展开→收起迁移后 band 几何 ±1px 硬门 + 岛底条带全宽零外来墨已落（c6fbdb3），仅在 nightly 的 Visual evidence 跑；nightly run 45670616849（head d8b52e8）实跑取证全绿——其间两轮假红修复（c538262 AE 缓存 combo peer 改逐次重取 + SI 一次取不到改 15s 轮询门；d8b52e8 条带门改行持续双门：单点 414 采样=CI 桌面 DWM 投影亚像素污染非真残留，复证 run 45649709511）。
  - imac 真机取证（2026-11-26，deployed 2026.11.25+d8b52e8）：顶窗枚举仅岛主窗+描边带+桌面 tile 三窗，**未透明化空壳窗不再出现**（_hasFrame 门生效，空白胶囊条不复现）；展开态底缘黑→背景干净过渡（一行 AA 灰即背景），收起态同口径干净；证据 docs/acceptance-evidence/island-flank-verify/。
  - 验收硬门槛：CI 像素断言新增「白条区域透出壁纸色」+ 底部月牙采样全覆盖；**用户装机亲验通过才闭项**，agent 自评无效（重复报障单，从严）。

## 展开态白色底板（2026-11-25 用户真机截图报——岛窗口面问题第四单：灰底→EDGE-1 锯齿→BOTTOM 空白条→本条，修复必须走到用户亲验为止）

- [ ] ISLAND-FLANK-1【2026-11-25 用户真机截图报障，已派 beacon 席执行中】展开态主岛左右两侧各露 38px 白色底板（用户原话定性；验收=CI 像素断言 + imac 真机截屏 + 用户 vision 亲验）。
  - imac 真机取证（2026-11-25，deployed 2026.11.16+41da77d）：白板两成分——①肩 16 DIP 内收=设计性侧通道（窗 238 宽、黑面仅 206，通道全高 region 外），kill-test 证白=用户浅色桌面本体（上灰 221,227,233 / y≈79 下纯白）非 app 绘制；②展开迁移期 UpdateSurfaceOutlineCore 读 RootBorder.Actual* 过期拍（island-trace：outline 238x64 而 root=238x93），region 已按窗真相裁满、曲面按旧尺寸画，region 内露 WinUI3 窗背板（浅色主题=白）。XAML 41da77d↔HEAD 零差异，HEAD 同病。
  - 修：ShoulderDips 16→0（CapsuleIslandMath——左右直边压窗缘全高，通道物理消失；收起窗高恒 32=Positioning 无阴影带口径，窗真相与旧特判同值）+ UpdateSurfaceOutlineCore 曲面尺寸改量窗客户区真相（物理像素→DIP，与 ApplyIslandRegion 同源，曲面恒铺满 region）+ Core 几何锁改肩 0 口径（Sides_RunFullHeight_NoShoulderChannels/镜像曲线 2 条/常量 0）。
  - CI 验收面：AH1d 新增展开态侧板实着断言（左右翼板=窗缘内 4 DIP 半高处，三通道和 <90 近黑；肩/通道回归该点透桌面 ≥153 即红）；Get-CrescentMaxDistance 改底角月牙采样（AH5d① 同点，肩归零后顶部无弧外区）；AH5d③ 肩轨迹退役（旧肩区现恒黑面，轨迹恒常数=假信号），AA 硬门收窄到底两角；nightly run 45670616849（head d8b52e8）AH1d/AH5d/AH5e 全绿。
  - imac 真机取证（2026-11-26，deployed 2026.11.25+d8b52e8，主屏 5120×2880 @200%）：程序化首展（BEACON_DEBUG_ISLAND_PREVIEW=1，与 CI 场景 AH 同钩子同 ShowPreview 渲染路径）展开窗 482×192，band=clientRect±4px 精确吻合；翼板采样（窗缘内 4/16 DIP 半高）左右四点全 (0,0,0) 和 0——**无白色侧板**；药丸 x 322..800 vs 窗框 320..802 直边压窗缘全宽（ShoulderDips=0 生效）；框内白色像素 1.3%（内容文字）；证据 docs/acceptance-evidence/island-flank-verify/（展开/收起/整屏三件+README）；真机 SendInput 绝对坐标在三混合 DPI 屏虚拟桌面映射落点偏移，悬停往返未在真机复跑（交互链 CI AH1a-c 已门，本单病灶是几何非交互）。
  - 验收硬门槛：CI 像素断言绿 + imac 真机截屏（展开态左右无白板）+ **用户 vision 亲验通过才闭项**（岛面问题第四单，从严）。

## 岛几何对齐 BrimDeck + 设置页排版 + 配置即生效（2026-11-26 用户真机报——岛窗口面问题第五单：灰底、EDGE-1 锯齿、BOTTOM 空白条、FLANK 白板之后又报「上角不是直的」「下方两角白色外围」「图标和文字都贴在一起」，修复必须走到用户亲验为止）

- [ ] BEACON-FIX-1126【2026-11-26 巡检单派工】岛顶部两角直角硬切 + 底角白色外围 + 设置页四病灶（图标贴字/下拉截断/保存取消飘左上/开关挤压文字列）+ 改配置要重启才生效；验收=Core 全绿 + CI 三新门绿 + imac 真机取证 + 用户装机亲验（从严）。
  - 病灶 A 修（4b934bc）：IslandSurfaceOutline 无肩分支补抄漏的顶角 tl/tr 圆象限弧（BrimDeck IslandBorder 同式三段；TopRadiusDips=12 与底角同值、clamp min(w,h)/2；「Notch TopRadius=0」仅适用有肩刘海态的旧误读已纠）；XAML figure 起点改随 Move 段（旧硬编码 (0,0)）；经典胶囊红线：肩>0 分支顶缘恒 (0,0) 到 (w,0) 零变化（ShoulderPresent_KeepsSquareTop_ClassicCapsuleUnchanged 锁）。
  - 病灶 B 修（4b934bc）：RegionPoint/FlattenCubic 增客户区物理尺寸参数，region 多边形整体向窗内收缩 1 物理像素（X 按左右半场 ±1、Y 下半场 −1/上半场 +1、顶缘 py=0 不动防顶缝），收缩缝由 3px 边带环盖住；Core 几何锁 1420 全绿（IslandSurfaceOutlineTests 生产口径重写：四曲线镜像/闭合 (tr,0)/夹取/方角退化分支）。
  - 病灶 C/D/E/F 修（17c5772）：DesignTokens 语义间距 token（IconTextGap=10/RowInline=8/SectionGap=16/CardPadding=12）+ 模块图标 Border 右 Margin 10；组件向导表头删硬编码宽（220/200/180）改三列 Grid（类型★/连接★/刷新档 MinWidth 220）+ 频率框独立行；连接编辑器保存/取消移表单底部右对齐（保存主按钮最右）；模块/连接行开关 MinWidth 40 改固定宽 44（实测轨宽，列宽恒定不挤文字列）。
  - ③ 连接保存即生效修（545ccba）：全写点广播审计（SettingsApplied/PinsChanged/Kick）Desktop/Appearance/Actions/Navigation/ComponentStyles/Pages 全齐，唯一缺口=SaveConnectionAsync 保存后无立即重拉（Provider 每拍现读 config 无缓存，但组件在退避或长周期档时新值睡到下个节拍，workflow 档 10 分钟）；补 Host.KickConnection(id)（与启用开关同口径，打断退避段下一拍按新值拉取）。
  - CI 验收面：AH5d-B region 直读门（GetWindowRgn+PtInRegion/GetRgnBox：四角出 region=顶角弧+内缩、包围盒 Top 贴顶、Left=1 与 Bottom 上界锁内缩，决定性免桌面像素参照）；SG 病灶 C 门（图标右缘到文字左缘 ≥9px，BoundingRectangle 直读带 NaN 防护）；SH 病灶 D 门（刷新档下拉宽 ≥218px=MinWidth 220，硬编码 180 恒红）；SET-UX-ROW-1 开关列断言随 nightly 重跑覆盖病灶 F。
  - CI 门脚本三连修（89c829f+a4c1ba3+dfeb789，门脚本自身缺陷非产品缺陷）：① AH5d-B 的 region 四件套 P/Invoke 写错 DLL（CreateRectRgn/DeleteObject/GetRgnBox/PtInRegion 是 GDI32 导出，写 user32 找不到入口→EntryPointNotFoundException，全加 ExactSpelling=true 无后缀导出精确匹配，CI 45726231918 实证）；② SG 窄窗门搜索词写「透明度」但模块过滤只数模块名命中（头行恒 0/N）→换「看板」（桌面显示模块+看板入口双命中，头行 N/M 且行滤除，CI 45740410733 实证）；③ SH tier-box 锚在 Provider 页组件向导，「查看去向」跳组件中心后锚离树→门挪到跳页之前（CI 45740410733 实证）。nightly run 45754679256（head dfeb789）三道门全绿：AH5d-B「四角全出 region 顶缘 Top=3 贴顶铁律」/ SG 病灶 C「10px（≥9）」/ SG 窄窗档 / SH 病灶 D「220px（≥218）」；nightly 资产 06:02 UTC 发布。
  - 验收硬门槛：CI 三新门绿 + imac 真机截屏（顶角圆弧/底角无白外围/设置页间距列宽按钮位）+ **用户装机亲验通过才闭项**（岛面问题第五单，从严）。


## Codex 深审发现（2026-11-12，luna profile 近5 commit 审核，0C/5M → 2026-11-13 全修收口）

- [x] [M] DSL 动作 TTL 缓存感知配置变更（src/Beacon.App/Services/BeaconRuntime.cs:42-83）——连接列表变更不失效，注释与实际竞态面不符（96de3e3：JsonConfigurationStore 新增 ConnectionsVersion 代数（Upsert/Remove/ReplaceAll 递增、删不存在不增，Storage 测试 ConnectionsVersion_BumpsOnConnectionMutationsOnly）+ DslActions getter 版本不匹配即重算、代数未变才走 2s TTL，注释按实际双失效面重写；App 侧 getter 属 App 工程 Linux 不编译，CS 面人工核 + CI 编译门）
- [x] [M] AppConfigFile 区分 IO 异常与 JSON 损坏（src/Beacon.Storage/AppConfigFile.cs:24-58）——任意 IO 异常误触发备份恢复链路（c2c9c8b：catch 收窄 JsonException/null 两类才进恢复链，IO 带一次 150ms 瞬时重试后 fail loud 不冒充损坏；Storage 测试 AppConfig_Load_IoFailure_ThrowsInsteadOfCorruptChain（新增 Reader 注入 seam + Beacon.Storage.csproj 补 InternalsVisibleTo，断言抛 IOException 且不误留档、原文件不动）；ReadConfigText 重试仅包 IOException，UnauthorizedAccess 直接上抛）
- [x] [M] RefreshScheduler 重取快照收敛 TOCTOU（src/Beacon.Core/Services/RefreshScheduler.cs:190-201）——组内 widget 删除后仍白拉、空组循环不终止（debf201：GroupState.Registered 标志（UnregisterCore 摘组/Clear 清表置 false）+ 等待后取消位、注册态与快照同锁原子判定，组被注销/清表不再发那轮刷新；锁释放后单组件移除的残余窗由 WidgetHost 晚到闸兜底（无网络请求），该结构窗不可确定性注入——空组终止与快照剔除两终态由既有 UnregisterLastWidget_StopsGroupLoop / WidgetUnregisteredDuringWait_NotRefreshedNextCycle 两测锁住）
- [x] [M] WidgetHost 重建 Clear()→逐个注册竞争收敛（src/Beacon.Core/Services/WidgetHost.cs:174-176）——批量 kick 与组内注册竞争（debf201：_lifecycleGate 串行 Start/Reload/ReloadRegistrations 的 Clear→注册→启动序列与 KickConnection；KickWidget 改返投递成败（Core 测试 KickWidget_ReportsDelivery_LossSignalForUnregistered），真空期丢失的 kick 记 _pendingKicks、注册重建后 DrainPendingKicks 补放——唤醒意图穿越重建时序；回归 KickConnection_BeforeRegistration_PreservesWakeThroughReload：宿主未启动先 kick（丢失）→ Reload 空 kickIds → 1h 档负验门下立即首拉，摘 DrainPendingKicks 实测复红 Poll 超时）
- [x] [M] 同根因两条 DSL 缓存项合并同一 commit 处理（src/Beacon.App/Services/BeaconRuntime.cs）——避免修一半（已并入 96de3e3 单 commit：代数计数源（Storage）与 getter 失效消费（App）同批落地，不留半修；调度器/宿主两竞争项同根因（重建时序）同批 debf201）
- 门禁收口（2026-11-13）：全套测试绿 Core 1277/Connections 1017/Actions 55/Storage 59 合计 2408 过；ci run 43458417175 绿（Windows 编译门覆盖 App 侧 BeaconRuntime.cs，App 工程 Linux 本地不编译）
