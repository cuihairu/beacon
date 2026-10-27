# Beacon 需求实现差距分析（2026-10-24）

审计基线：本地 `main`，HEAD `8d16605`。仅静态读码及文档溯源；未运行 Windows/WinUI，不把源码存在当作用户已能看到，也不把潜在裁切当作实测缺陷。本次只新增本报告。

结论：已有全局两种样式和实例级五种模板，不能说实现完全写死；但选择、保存、恢复与预览没有形成可靠闭环。共列 **13 条差距：11 条源码可确认的行为/文档契约差距，2 条待体验验证的入口与可见性问题**。最关键的是无状态组件进入错误模式，无法应用样式（G01）。

## 1. 原始需求溯源与证据层级

本次用户原话：「展示的方式可以选择, 到现在展示组件的中心都看不到」。这是本次会话直接输入；仓内此前未找到该完整句子的原始记录。**“中心”的确切对象及其验收定义，需求原文缺失**。不能擅自把它定为组件中心页面、圆环中心数字、画布中心点或焦点。

`docs/requirements` 目录不存在。检索覆盖 `todo.md`、README 双语、`docs/design` 及 `docs/zh/design`、`docs/review`、本地 issue 清单、`.github` 和 Git 提交说明；忽略依赖与构建产物。根目录部分英文文档只是占位页（如 `docs/design/redesign-plan-2026-10-16.md:4`），故追到中文正文。未查询远端 GitHub issue，不能据本地无记录断言远端也无需求。

以下逐条摘录与本题直接相关的原文；“用户令转录”“外部建议”“实现台账”分开看，后两者不冒充用户原话。

| ID / 性质 | 需求或记录原文 | 出处 |
|---|---|---|
| R01 用户令转录 | 「beacon 能选择展示的组件最好」 | `docs/design/collapsed-progress.md:254`；中文同文 `docs/zh/design/collapsed-progress.md:254` |
| R02 用户令转录（代码注释） | 「设置里让用户自己选表现方式，比如一开始的进度条」 | `src/Beacon.App/Windows/SettingsWindow.cs:1218`（标注 2026-10-10；未找到独立会话原稿） |
| R03 设计细化 | 「L0 tile 的 `bar\|pool` 是**样式二选一**不是有无」 | `docs/zh/design/collapsed-progress.md:282` |
| R04 设计细化 | 「**重启不丢** = config.json 落盘即满足，无额外机制。」 | `docs/zh/design/collapsed-progress.md:313`（此处说偏好开关，不直接等同模板身份存储） |
| R05 外部设计稿 | 「同一份数据可以被不同类型的组件展示。」 | `docs/zh/design/redesign-plan-2026-10-16.md:822` |
| R06 外部设计稿 | 「一个显示实例可以组合多个数据源。」 | `docs/zh/design/redesign-plan-2026-10-16.md:823` |
| R07 外部设计稿 | 「应为组件库建立独立的视觉示例页，集中展示组件在不同状态和尺寸下的实际效果。」 | `docs/zh/design/redesign-plan-2026-10-16.md:258` |
| R08 外部设计稿 | 「默认尺寸和最小尺寸。」「可访问性名称与键盘操作。」「在小尺寸下如何降级展示。」 | `docs/zh/design/redesign-plan-2026-10-16.md:233`、`:237`、`:239` |
| R09 外部评审建议 | 「选择已有数据  选择展示样式  实时预览  应用」 | `docs/review/review-2026-10-18-component-center.md:62` |
| R10 外部评审建议 | 「展示中心应默认使用当前 Widget 的真实状态预览，允许用户调整标题、颜色、尺寸等外观参数。」 | `docs/review/review-2026-10-18-component-center.md:98` |
| R11 外部评审验收建议 | 「一份百分比数据至少可选择横条、圆环、数字卡和水位样式」 | `docs/review/review-2026-10-18-component-center.md:113` |
| R12 外部评审验收建议 | 「GLM 使用横条、DeepSeek 使用圆环，互不影响」；「相同数据在不同展示位置可以不同布局」 | `docs/review/review-2026-10-18-component-center.md:115`、`:116` |
| R13 外部评审对用户意图的转述 | 「同一个百分比数据，可以在界面中自主选择数字、横条、圆环、水位等适用的展示方式，选择不会改变数据源，也不会要求用户编写组件 JSON。」 | `docs/review/2026-10-22-display-component-selection.md:256` |
| R14 实施台账 | 「组件示例页（SampleGalleryWindow）改造为组件中心『选 → 调 → 用』单一闭环」 | `todo.md:26`（原文使用中文双引号；此处引号转排） |
| R15 实施台账 | 「组件中心定向模式改模板选择（左列按数据能力筛选的样式列表 + 应用面 tile/row/detail 可选，应用按面合并其余面保留）」 | `todo.md:28` |
| R16 未完成工作约束 | 「在跟踪表 12 项清零前不新增展示模板种类。」 | `todo.md:133`；十二项均未开工见 `docs/review/2026-10-22-display-component-selection.md:268`、`:272`–`:283` |
| R17 README 定位 | 「持续从不同数据源取数，标准化为一份共享状态，交给显示组件呈现」 | `README.zh.md:21`；仅产品定位，没有样式选择或“中心”的验收定义 |
| R18 本地 issue 痕迹 B-702 | 「消费 B-602 落库的 pinned 标记 + Settings 逐个开关；PinTile（StatusLight+标签+数字/进度，32 DIP 高）；仅 pinSupported 类型可钉；pins.json 读写。」 | `docs/zh/mvp-issues.md:337`；是显示/钉选基础，不等于展示方式切换 |
| R19 特定场景的中心要求 | 「是 logo 本体居中、进度环作 logo 的外圈。」 | `docs/zh/design/collapsed-progress.md:15`；仅适用于坍缩徽章，不能外推为所有圆环中心必须有数字 |

Git 痕迹：`184cdae` 引入 10-22 外部评审；`todo.md:27` 记录 P0 `4847f99`，`:28` 记录 P1 `94aa2aa+32dbe97+fb10bd1`。这些是演进线索，不代表本次验收通过。10-18 的旧“无绑定/无模板”结论已过时，不能照抄为当前缺陷。

## 2. 展示组件真实实现与“中心”现状

### 展示方式与切换链

| 层次 | 当前实现与可选项 | 源码证据 |
|---|---|---|
| 全局传统额度外观 | ComboBox 两种：bar 进度条（默认）、pool 圆池注水；用户可选并保存 | `src/Beacon.App/Windows/SettingsWindow.cs:1221`、`:1246`、`:1343` |
| 实例展示模板 | 五种：bar 横条、ring 圆环、number 数字卡、pool 水位、statusline 状态行 | `src/Beacon.Core/Services/DisplayTemplates.cs:22`–`:38` |
| 兼容筛选 | 当前 Progress 有值时五种均列出；没有进度时仅状态行。null 状态另触发 UI 模式错误（G01） | `src/Beacon.Core/Services/DisplayTemplates.cs:43`；`src/Beacon.App/Windows/SampleGalleryWindow.cs:198` |
| 选择操作 | 设置“组件”行的“展示样式”→独立窗口→左侧列表点击换模板→右侧选 tile/row/detail→“应用展示样式” | `src/Beacon.App/Windows/SettingsWindow.cs:2830`；`src/Beacon.App/Windows/SampleGalleryWindow.cs:322`、`:499`、`:614` |
| 保存与刷新 | 校验树，替换所选 surface 根树，UpsertWidget 落盘；PinsChanged 发刷新通知。数据源 ID 沿用 | `src/Beacon.App/Windows/SettingsWindow.cs:2702`、`:2760` |
| 通用组件中心 | 关于区“组件中心”进入基础类型目录及 Props 表单；应用需先有树编辑目标、再回设置保存。不是直接选择某数据的成品展示模板 | `src/Beacon.App/Windows/SettingsWindow.cs:702`、`:2727`；`src/Beacon.App/Windows/SampleGalleryWindow.cs:46`、`:370` |
| 正式渲染 | 模板生成绑定树→ComponentSurfaceRenderer 工厂；tile 用 PinTile（定义在 PinnedHostWindow.cs），详情用 DetailWindow | `src/Beacon.App/Components/ComponentSurfaceRenderer.cs:125`、`:270`、`:301`、`:329`；`src/Beacon.App/Windows/PinnedHostWindow.cs:216`；`src/Beacon.App/Windows/DetailWindow.cs:194` |

所以：**切换机制确实存在，并非写死一种；但模板结构/尺寸是预设值，普通定向模式没有外观参数表单。** “基础组件类型数”“成品模板数”“全局旧样式数”不可混算。

### “中心”分别核查，不替用户决定含义

1. **组件中心功能入口**：代码存在两个入口，关于页叫“组件中心”，实例行叫“展示样式”（SettingsWindow.cs:702、:2830）。关于页打开的却是基础工具箱，实际同数据样式选择要走另一个入口。可达路径存在，命名和用途容易错位；用户当前安装版本是否包含这些提交、是否实际可见，未验证。
2. **窗口中央内容区**：SampleGalleryWindow.cs:173 建五列 Grid，中列 300 DIP，预览加在第 2 列（:223、:270）；有“预览”标题、Border 和内部 ScrollViewer（:233）。不是没有中央区域，也没有设置该区域 Collapsed。但窗口宽 860 DIP、最高 780 DIP（:30、:157），根 Grid 无整体滚动；预览仅纵向滚动，横向禁用（:244）。小屏、高 DPI、窗口遮挡和缩小时的完整可达性不能由代码保证，需实机检查。
3. **圆环几何中心/中心内容**：ComponentSurfaceRenderer.cs:301 的 Ring 仅向 Grid 加两个 Ellipse，没有中心 TextBlock/图标；DisplayTemplates.cs:27 把摘要作为圆环旁的兄弟节点。故中心空白是结构事实，不是中心数字被遮住。仓内没有通用圆环必须显示中心数字的明确要求；坍缩 logo 居中要求 R19 属另一场景。
4. **中心点、中轴、画布回中**：本条选择器是普通 Grid/ListView 窗口，不是带平移缩放的画布（SampleGalleryWindow.cs:171）。未找到本功能的中心标记、适应视图/回中需求，不能凭用户一句话补成画布需求。
5. **键盘焦点**：列表、按钮、ComboBox 是标准控件；有点击事件不等于完整键盘验收。中心预览里的 Ellipse 也不是交互目标。未执行 Tab/Enter/Space、焦点环或读屏验证，不能宣称全部可达。

## 3. 需求—现状—差距（13 条）

下列文件名以第二节源码路径为准；每条给出需求和实现定位。G01–G11 是静态确认的行为或既有文档契约差距；G12–G13 是待验证项，不能计为已经实测的 UI bug。外部评审建议作为仓内目标线索，不等于用户已逐项确认。

| ID | 需求/目标出处 | 当前代码事实 | 差距、影响与验收方向 |
|---|---|---|---|
| G01 最高优先 | R09；10-22 评审 `:272` 空状态可配置 | SettingsWindow.cs:2747 取状态，:2751 将通用回调设 null；SampleGalleryWindow.cs:198 用状态是否 null 判断模式，:627 无通用回调就报错 | 新建/首次未取数时点击展示样式却进入工具箱，无法应用。应按目标实例而非状态有无确定模式；无数据也能选兼容配置并保存 |
| G02 | R12/R15；评审 `:273` | SettingsWindow.cs:2753 闭包捕获旧 widget；:2768 从该对象复制各面 | 同一选择器先保存 tile 再保存 row，第二次以旧 Components 合并，可能丢失第一次修改。应用时读最新配置并验证连续双面保存 |
| G03 | R11 数字卡可用；评审 `:274` | DisplayTemplates.cs:31 unit=%；ComponentBindingEngine.cs:35 已返回带 % 的值；ComponentMetric.cs:33 另画单位 | 52% 后又显示一个 %（两个 TextBlock），数字卡重复单位；验收 0/52/100/缺失四态 |
| G04 | R12；评审 `:275` 样式恢复 | SampleGalleryWindow.cs:146 恒选兼容首项，:122 面默认 tile；构造器 :136 未接收当前配置 | 已保存圆环再打开却选横条；选择器不能准确表示当前样式/面。需保存身份或可靠反解并恢复 |
| G05 | R13；评审 `:120`、`:276` 槽位替换 | SettingsWindow.cs:2772 整面替换；PinnedHostWindow.cs:212 有树就跳过传统六列 | 普通换样式同时移除原品牌/标签等外围结构，超出只换数据呈现的预期。应保留外围，只换明确槽位；整树替换标为高级操作 |
| G06 | R05；评审 `:277`–`:278` 能力匹配 | DisplayTemplates.cs:20 仅 RequiresProgress；:43 用当前 Progress.HasValue，模板无语义/单位/适用面声明 | “这次没值”被等同“不支持进度”；适用性不区分来源语义和展示空间。需稳定能力描述与不可用原因 |
| G07 | R08 状态规范；评审 `:279` | ComponentSurfaceRenderer.cs:285 绑定缺失回退 Props.progress，默认 0 | 缺失进度和有效 0 画成同一形态；需无数据/过期/有效零区别，不能伪装确定零值 |
| G08 | R06；评审 `:280` | DetailWindow.cs:199 仅传自身 _state，未走完整 sources 映射重载；映射重载在 ComponentSurfaceRenderer.cs:161 | 详情组合多源绑定缺少真实附加源输入；需映射及附加源更新链验收 |
| G09 | R09/R10；评审 `:281` | SampleGalleryWindow.cs:117 readonly 快照，:145 赋值，:604 预览复用；:359 定向表单只有说明与 surface | “实时预览”不跟随后续数据更新，且不能调标题/颜色/尺寸。参数变化重新渲染不等于状态实时；应接更新并提供参数表单 |
| G10 | R03/R12；评审 `:283` | SettingsWindow.cs:1221 全局外观仍可选；PinnedHostWindow.cs:216 声明式分支提前返回 | 实例树实际优先于旧全局样式，但界面未在全局选项处说明作用范围，用户可能保存后看不到变化。需清楚标注优先级及恢复默认入口 |
| G11 | R07；R16；`todo.md:28` | SampleGalleryWindow.cs:604 固定样例/快照、:233 单一预览盒；未提供多状态多尺寸验证矩阵 | 示例页存在不等于完成“不同状态和尺寸”验收；台账仍写端到端观感待装机。需对五模板三展示面保存、重开及状态变化给出证据 |
| G12 待用户确认/体验验证 | 本次“中心都看不到”；R09 | SettingsWindow.cs:702 关于区中心，:2830 行内展示样式；两路径用途不同 | 入口发现与命名错位风险明确，是否正是用户所指尚不明。建议统一实例编辑入口和标题，在实机验证用户能直接找到 |
| G13 待 Windows 实测 | R08；评审 `:282` | PinnedHostWindow.cs:204 固定 tile 高；DisplayTemplates.cs:30 数字卡纵排大字+摘要、:26 横条宽 220；SampleGalleryWindow.cs:30 固定窗宽 | 预览与真实宿主尺寸不同，存在裁切/中心内容不可达风险，尚无本次截图证明。需小尺寸、长文本及多 DPI 验证，不先断言中心已被裁掉 |

没有把 R19 的坍缩 logo 居中要求误记为通用圆环缺陷，也没有把过去“模板没有绑定”的旧问题再次计入。相比旧台账十二项，本表合并“语义描述+能力匹配”为 G06，将入口发现与状态/尺寸验收单列；不是机械搬运旧计数。

## 4. 交互形态建议（建议，不追认成历史需求）

成熟可视化编辑器的可借鉴模式：Grafana 在面板编辑器提供可视化类型选择、预览、显示选项，并明确保存/放弃操作；面板本身有 Edit 入口。参见 [Grafana Panel editor](https://grafana.com/docs/grafana/latest/visualizations/panels-visualizations/panel-editor-overview/) 与 [Panel overview](https://grafana.com/docs/grafana/latest/visualizations/panels-visualizations/panel-overview/)，检索日期 2026-10-24。以下是对 Beacon 的适配建议，不是这些产品的逐项原样行为：

- 每个数据实例提供可发现的“展示方式”入口；统一进入“实例名 / 展示面 / 当前样式”的编辑器。“组件中心”用于浏览模板时，也应明确选择目标实例，基础控件工具箱放高级区。
- 左侧用带缩略预览的单选模板列表，中间在真实宿主尺寸内预览，右侧提供样式参数；选项显示选中态和不兼容原因。样式选择保持数据源不变，不要求用户编辑 JSON。
- 用“应用 / 取消（放弃未保存修改）”，已应用有明确反馈，重开恢复当前项；不同实例和不同 surface 独立保存。无状态时显示“等待数据”并允许合适配置，不能跳到另一个模式。
- 预览随数据更新，注明最新时间；提供缺失、零值、正常、错误、过期的检查方式，并允许查看目标实际尺寸。超过可用空间要滚动或响应式布局。
- 若“中心”确认为画布中心，再考虑“适应内容/回到选中对象”等画布交互；若指圆环读数，再设计中心文字及尺寸规则。当前阶段不以此为由新增画布或中心数字功能。

## 5. 需求原文缺失部分：供用户确认的可验收清单

下列是把本次话语拆成候选验收点，不表示用户已经确认扩展范围。阻塞的是语义确认，不影响本报告完成。

| 候选 | 可观察的验收方式 | 尚待确认的边界 |
|---|---|---|
| A1 方式可选择 | 在同一数据实例上找到“展示方式”，选择至少两种适用方式，应用后实际 UI 改变 | 是实例级还是全局；需要哪些具体样式？现有五模板只是候选基线 |
| A2 选择有效且可恢复 | 不改连接/数据源，保存后重开和重启仍体现所选样式 | 是否包括 tile、row、detail 全部面，以及每面独立？ |
| A3 中心可见可达 | 在指定屏幕、DPI、窗口状态下看到并能到达用户所指对象 | “中心”究竟是组件中心入口、预览主体、圆环中心内容、坍缩 logo、几何中心/中轴还是键盘焦点？应先指定对象，不能全部默认实现 |
| A4 无数据也不误导 | 新建/离线打开选择器，清楚显示数据状态；不陷入不能应用的无关编辑模式 | 无数据时允许预选所有已知能力还是限制部分方式？ |
| A5 可复核证据 | 用用户正在使用的安装版本走完“找到→选择→预览→应用→重开”，截图标出中心对象 | 需 Windows 构建版本、复现场景和用户确认后的中心定义 |

后续验收至少覆盖：同一窗连续保存两面、重开恢复选择、两实例互不影响、进度 0/52/100/缺失、数据 52→78 更新、窗口缩小/高 DPI、键盘进入选择并应用。当前未做这些运行验证，不用单元测试通过替代可见可达证据。

## 6. 交付与范围

仅新增此报告，不修改实现代码。启动时工作区已有未跟踪 `docs/.vitepress/config.mts`、`docs/package.json`、`docs/node_modules`，不属于本次差距分析，提交只应包含本报告。提交前检查引用存在、差距计数及 `git diff --check`。推送结果以执行日志和最终回报为准，不在报告预写成功。
