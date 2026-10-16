[中文](todo-triage-2026-10-16.md) | [English](todo-triage-2026-10-16.en.md)

# todo 42 条分类（2026-10-16 用户执行单）

> 口径：把 `todo.md` 42 条未完成逐条归入 A（可直接实现）/ B（需用户环境或用户拍板）/ C（已被代验可关闭）。
> 红线照执行单：C 类勾选必须给证据（场景名或测试文件 file:line），不许为清账而勾。
> 本版结论 **C 为空集**，理由见下节；用户点名的 6 条（37/39/57/62/65/67）括注里确有「已代验」，但每条的括注自身同时保留了「需装机半」——按 todo.md 自带的验收口径「改了未验不算完成」，勾账即自我矛盾。故 6 条落 B，并在 B 表内单列「已代验半证据」与「装机清单对应行」。
> 行号为本文生成时 `todo.md` 的行号；新增或删除 todo 行会导致漂移，判定时以条目原文为准。

## 总览

| 类 | 条数 | 分布 |
|---|---|---|
| A 可直接实现 | 5 | Phase 5「完善组件目录与用户体验」全部（todo.md:184-188） |
| B 需用户环境或用户拍板 | 37 | MVP 21（:37-83）+ 后续可选 1（:91）+ 验收 3（:96-99）+ Phase 4 六条（:175-180，需拍板）+ Phase 6 六条（:192-197，真机验收） |
| C 已被代验可关闭 | 0 | 无一条满足「证据覆盖整行」 |

## 为什么 C 是空集

判定标准：一条能进 C，要求已有代验证据覆盖该行的**全部**验收口径。逐条核下来：

1. 21 条 MVP 未完成项的括注全部以「需装机半」收尾，且多数同时依赖真实凭据（:44/:46/:48/:54/:99）或物理硬件（:70/:71）或 OS 面行为（:37/:57/:96/:97）。CI 证据（`daily-build` 视觉取证工件，断言失败即红构建）只覆盖机械半。
2. 用户点名的 6 条是「证据最充分」的一组，仍不满足整行覆盖：
   - todo.md:37 进程半已代验（场景 E1 进程数恒 1），气泡呈现属 OS Toast 面（E1b 已建探测、无头会话不可见降级 warning，run 38619441591）。
   - todo.md:39 位置还原 + 拖动手势链已代验（场景 R/R3，run 38750239155），跨 DPI 拓扑需物理多屏。
   - todo.md:57 AUMID 自愈半已代验（场景 F5t，预删注册键后 `Initialize()` 重建，run 38494525200），点击/激活路由属 OS Toast 面。
   - todo.md:62 缓存无白屏 + Esc 键盘关闭已代验（场景 K3 硬断言，run 38743014862），100ms 精确计时需秒表。
   - todo.md:65 空白穿透宿主裁决已代验（H5b `WM_NCHITTEST`：tile 行=HTCLIENT、外框空白环带=HTTRANSPARENT，run 38750239155），真实鼠标落到下层窗的观感需装机。
   - todo.md:67 位置还原 + 二选一 + 拖放链已代验（场景 H/H6，run 38750239155），手感需装机。
3. 引擎层证据确实齐全（`tests/Beacon.Core.Tests/EventBusTests.cs:9` Publish_InvokesAllSubscribers、`:25` HandlerException_DoesNotAffectOtherSubscribers、`:38` Unsubscribe_StopsDelivery、`:52` 发布快照语义、`:86` SubscribeOnUi_MarshalsToDispatcher；`tests/Beacon.Core.Tests/NotificationEngineTests.cs` 引擎去重与冷却），但这几项被 todo.md 已勾选行（:58/:59）消费，不落在 42 条未完成里。

结论：清账式勾选 6 条会同时违背执行单红线与 todo.md 自身验收口径，不做。若你认为其中某条的装机半已可放弃（例如把「气泡观感」「100ms 计时」降级为不做），那是验收口径变更，需你明确说，我按新口径勾并注明。

## A 类（5 条）：Phase 5 完善组件目录与用户体验

前置事实：Phase 2 已把设计令牌与词汇件备齐（`ComponentFeedback` 四词汇件 Empty/Loading/Error/Offline 已落地但无调用面，`docs/design/redesign-2026-10-16-组件库落地.md`；`SampleGalleryWindow` 已有 14 工厂实样证明面），所以 A 类主要是**把已备的件接到真实面**，不是从零造件。

| 行号 | 条目 | 实现要点 | 本地门禁 |
|---|---|---|---|
| 187 | 统一的空数据、离线与错误状态 | 把 `ComponentFeedback` 四件接到 QuickPanel 空区与 Detail 无数据态，替换现行「空区隐藏 + XAML 横幅」两套口径；接面后删掉重复文案分支 | Core 纯函数无改动即无新单测；App 侧人工核 + CI |
| 185 | 更丰富的布局与条件显示 | `ComponentLayout` 现有 Row/Columns/SectionLabel/When 之外的 Column 词汇件补调用面；条件显示按状态字段驱动而非写死 | 布局不变量已有守（`ComponentLayerInvariantTests`），新增形状补一例 |
| 184 | 时间线、表格、图表与趋势组件 | 新增 3 个组件工厂（时间线走事件历史、表格走 payload 多行、图表走趋势序列）；数据源走既有绑定引擎（state / selector / const 三态），不引入新 Provider | 各配 1 组 Core 侧解析与空态单测 |
| 186 | 组件实例编辑、尺寸调整与桌面布局配置 | 设置页新增实例面（组件树编辑、尺寸档、桌面布局区）；写入走既有 `Config.UpsertWidget` + 树校验 fail-closed 口径 | 校验分支补 Storage 侧单测 |
| 188 | 主题、密度与可访问性完善 | 密度刻度（紧凑/常规）接 `DesignTokens` 间距刻度；可访问性补齐 UIA 名称与焦点序（现行坑：Border 只设 AutomationId 不进 UIA 树，须 `SetName` 造 peer） | UIA 名称补齐后场景 X 类断言需同步核对 |

优先级顺序：187 → 185 → 184 → 186 → 188。理由：187 是纯接面、无新语义，风险最低；185 依赖 187 的状态语义；184 三个新件面最大但纯增量；186 牵动设置页与配置面（改动面最广，放后）；188 的可访问性补齐要在前四项的新件面落定后一次收口，否则返工。

**卡点（本机 Linux）**：App 侧（`src/Beacon.App/`，WinUI 3）在 Linux 编不了——`XamlCompiler` 先于 `Csc` 死，本地无 C# 门禁，App 改动只能人工核 + CI（`windows-latest`）验证。Core / Storage 侧单测可本地跑全量。这不是卡住，是既定交付面：历次 App 改动均走此路径落地。

## B 类（37 条）：用户需要提供什么

| 需要用户提供 | 覆盖行号 | 具体缺什么 | 已代验半（证据） |
|---|---|---|---|
| 真实 GitHub PAT | 44, 46, 48, 54, 99 | 一个可 revoke 的真实 PAT + 有 PR 与 Actions 活动的真实仓库 | mock 链路场景 D；API 层假 Handler 单测（GitHubPullRequestsProviderTests 18 例、GitHubActionsProviderTests 9 例、GitHubApiClientTests 10 例、GitHubWorkflowExecutorTests 6 例）；映射层表驱动 |
| 真实厂商 API 与认证字段 | 91 | 厂商私有打包工具的 API 文档、认证字段名、额度口径 | 开源标准 API 的 Jenkins Provider 已落位（`jenkins.build`） |
| 装机观感 / 计时 / 发声目检 | 37, 52, 57, 60, 62, 65, 67, 73, 75, 79, 80, 97 | 有头桌面会话：气泡/Toast 呈现、默认浏览器跳转、秒表测 100ms、听声、托盘图标目检、刷新不抢焦点的手感 | E1 进程半、F5t AUMID 自愈半、K3 Esc 键盘关闭、H1 前台不变、H5b `WM_NCHITTEST` 穿透裁决、G7a tile 层反映、G4/G5 改色、G6/G7b/G7c/V2 脉冲四半、V1/V2/V4 注入与通知链（run 见各括注） |
| 物理或虚拟显示栈（多屏、不同 DPI、拔插） | 39, 70, 71 | 至少两块缩放不同的屏；CI 是单屏会话，拓扑事件不可驱动 | R/R3 位置与拖动链、H/H6 悬浮拖放与还原（run 38750239155） |
| 真人全流程操作流 | 74, 82, 83 | 干净 Windows 11 虚拟机 + 录屏；RFC §6.2.7 四条含真实网络与真人步骤 | 截图已归档 `visual-evidence` 工件；缓存水合全周期单测已过 |
| 用户拍板（HTML 边界裁定） | 175, 176, 177, 178, 179, 180 | Phase 4 开工前须先出架构决策：仓内有反向记录（`docs/master-plan.md:769`），设计稿关于「已有实现」的断言约六成在仓内确有其事、两处基本前提大面积查无 | 无（决策前不动代码） |
| 真机回归验收 | 192, 193, 194, 195, 196, 197 | 依赖 A 类落地 + Phase 4 拍板后，走装机清单 71 条与场景取证回归 | 装机 71 条清单已就位（`docs/acceptance-checklist.md`），CI 视觉取证场景 A-Z 已是回归门 |

## 与 todo.md 的同步口径

- 本文不勾选任何 todo 行（C 为空集）。
- B 类行不动原文（各括注已自带「已代验半 + 需装机半」，与本文 B 表一致）。
- A 类按优先级逐条落地，每条落地后在 `todo.md` 对应行括号内补 commit hash 并勾选，与 Phase 0-3 同口径。
- 装机半清掉时，`docs/acceptance-checklist.md` 对应行的日期注记同步补上。
