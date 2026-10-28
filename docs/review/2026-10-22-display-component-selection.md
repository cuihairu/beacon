# Beacon 外部审核（2026-10-22）· 展示组件选择机制

> 来源：外部 AI 评审对话导出（用户提供；导出稿含重复碎片段与首部残壳，原始分享链接未随导出保留，原始字节存档见同目录 `2026-10-22-display-component-selection.extract-raw.txt`）
> 取回：2026-10-22（评审者自述基于当日可访问的 GitHub `main` 分支源码）
> 整理：2026-10-23（按 containment 去重，保留一份完整正文；评审原文中的 citeturn/`<Cite>` 引用标记与残壳剔除；提取中丢失的段落以【提取缺失】如实标注，不代拟）
>
> **性质：外部建议，不是本仓事实陈述。** 每条结论都必须先与代码核对再采纳；
> 下方「核验结论」表记录核对结果，未核实的条目不得直接实施。
> 评审者自述未在本地运行 Windows/WinUI 程序，本文属**源码静态审计**，裁切与交互观感部分待真机验证。

## 核验结论

整理人已逐条与仓内代码核对；前四行为任务交付时已核验项，后两行为整理时补验。

| # | 评审声称 | 仓内核验 | 结论 |
|---|--------|--------|------|
| 1 | `DisplayTemplate` record 含 `RequiresProgress` 能力门槛；`Compatible()` 仅此一条规则 | `src/Beacon.Core/Services/DisplayTemplates.cs:20`（record 第 4 参 `bool RequiresProgress`）+ `:44`（`All.Where(template => !template.RequiresProgress \|\| state is { Progress: { } })`） | ✅ 属实 |
| 2 | `OpenComponentStyleCenter()` / `ApplyComponentDirect()` 存在，且应用回调捕获最初的 `widget` 对象 | `src/Beacon.App/Windows/SettingsWindow.cs:2741` / `:2760`；`:2753` 闭包 `(root, surface) => ApplyComponentDirect(widget, root, surface)` 捕获构造期 `widget`，非按 Id 取最新 | ✅ 属实（旧对象覆盖风险的代码路径成立） |
| 3 | 空状态进入通用目录后「载入组件树编辑器」断链，显示「本窗未接入设置页」 | `src/Beacon.App/Windows/SampleGalleryWindow.cs:620`（定向回调缺失分支）与 `:629`（非定向入口）两处同文案 | ✅ 属实 |
| 4 | `BoundFraction()` 绑定值缺失时回退到 0 | `src/Beacon.App/Components/ComponentSurfaceRenderer.cs:285-287`：绑定空串（无状态/字段缺）回退静态 `Props.progress`，缺省值即 0 | ✅ 属实（「缺失显示为 0%」成立） |
| 5 | 数字卡 `52%%` 重复百分号 | `DisplayTemplates.cs:31` metric 模板 `unit="%"` + 绑定 `Selector="percent"`；`ComponentBindingEngine` percent 选择器输出 `Math.Round(raw*100) + "%"`（自带 `%`）；`ComponentMetric` 再独立渲染 unit → `52%%` | ✅ 属实（代码路径成立） |
| 6 | 缺陷 C 重开不恢复已选模板；缺陷预览读只读快照；DetailWindow 只传自身状态 | `SampleGalleryWindow.cs:146` `_templateId = Compatible(targetState).FirstOrDefault()?.Id`（恒选首个兼容模板）+ `:117/:145` `readonly _targetState` 快照；`DetailWindow.cs:199` `Render(其余面树, _state, …)` 只喂自身状态 | ✅ 属实 |

其余条目（§三槽位化整体替换的 PinTile 行为、§四建议架构、§六各任务）为评审建议或尚未逐行核验的声称，实施前须先核对相应调用链。

---

## 去重后全文

# Beacon 深度代码审核：展示组件选择机制

**审核重点：同一种数据能否根据其类型和语义，在多个兼容的可视化组件中自由选择，而不是被数据源或业务模块固定一种展示方式。**

先说一个重要发现：**Beacon 最新源码已经开始实现你要求的功能，不再只是之前的静态组件示例页。但目前实现的"按类型选择"，和你期望的完整能力驱动组件系统，仍然不是一回事。**

我核对了 `SettingsWindow.cs`、`SampleGalleryWindow.cs`、`ComponentSurfaceRenderer.cs`、`WidgetConfig.cs`、`WidgetInstanceResolver.cs`、绑定引擎以及相关设计记录。

最新代码中已经可以确认：

- 组件实例增加了 **「展示样式」** 入口。
- 组件中心存在面向具体数据的选择模式，不再只有 12 种基础控件目录。
- 已加入横条、圆环、数字卡、水位和状态摘要等展示模板。
- 选择样式可以通过 `WidgetConfig.Components` 持久化，并针对 `tile`、`row`、`detail` 分别指定。
- 配置修改后已经有即时重渲染路径，不必等待下一次网络请求。

这些都是真实的源码进展，不能再沿用之前"完全没有组件选择功能"的判断。

不过，**已有"样式选择入口"不代表已经实现完整的"数据语义 → 兼容组件 → 实例选择 → 运行时渲染"体系。** 下面继续核对它的筛选依据、数据绑定、缺失状态、兼容性以及配置闭环。

## 一、最核心的问题：现在筛选的是"有没有进度值"，不是"数据是什么类型"

当前 `DisplayTemplates.Compatible()` 的判断实际上只有一条规则：

```csharp
All.Where(template =>
    !template.RequiresProgress ||
    state is { Progress: { } })
```

也就是说，**只要当前状态有 `Progress`，就开放横条、圆环、数字卡、水位；否则只开放状态行。**

这与你的要求有一个重要区别：

| 维度 | 你的预期 | 当前实现 |
|--------|--------|--------|
| 类型来源 | 数据源声明稳定的数据类型和语义 | 当前 `WidgetState.Progress` 是否有值 |
| 百分比 | 自动列出适合百分比的所有组件 | 有 `Progress` 时列出 4 种进度样式 |
| 无数据时 | 仍可配置组件，预览提示暂无数据 | 可能退化成通用组件目录 |
| 其他数据 | 数量、金额、时长、状态等有各自适用组件 | 主要只有进度与状态两类 |
| 兼容性 | 类型、单位、范围、展示区域共同决定 | 基本是单个 `RequiresProgress` 布尔值 |

这个差别很关键。**"没有收到百分比"不等于"这个数据不支持百分比"。** 刚创建组件、网络断开、接口暂时失败时，都不应该让组件选择能力消失。

## 二、源码中已经找到的确定性缺陷

以下是静态代码路径核对所得；尚未在 Windows 上执行界面测试。

**缺陷 A：没有状态时，"展示样式"可能出现不能应用的界面。**

`SettingsWindow.OpenComponentStyleCenter()` 把 `Host.GetState(widget.Id)` 传入组件中心；`SampleGalleryWindow` 又直接用 `_targetState == null` 判断是否进入定向选择模式。

当状态为空时，它切换到通用的 12 类基础组件目录，点击「载入组件树编辑器」后，却因为这条入口没有传入 `_applyToSettings` 回调，而显示 **「本窗未接入设置页」**。这不是单纯的体验不佳，而是一条按源码可以推导出的断链路径。

**缺陷 B：数字卡存在重复百分号。**

`DisplayTemplates` 给数字卡设置了 `unit="%"`，而 `ComponentBindingEngine` 的 `percent` 选择器本身已经输出带 `%` 的字符串。`ComponentMetric` 又会独立渲染单位。

例如输入 `Progress = 0.52`，代码组合结果会是 `52%%`，而不是预期的 `52%`。

**缺陷 C：重新打开选择器不会恢复当前选中的展示样式。**

组件保存的是生成后的 `ComponentDescriptor` 树，不是用户选中的模板 ID。组件中心初始化时直接选择兼容模板中的第一个，因此，即使上次设置成了圆环，下次进入也可能默认选中横条。

这三个问题都应该在继续扩展组件数量之前修复。

## 三、更深层的问题：当前"切换样式"实际上可能替换整个展示区域

这是我认为比组件数量更值得优先处理的设计问题。

目前 `ApplyComponentDirect()` 会把选中的模板生成成一棵新树，直接赋给 `Components[surface]`。而 `PinTile` 一旦发现 `tile` 有声明式树，就跳过原有的传统布局，改由新树渲染整个区域。

这意味着用户原本只是想把**百分比从横条换成圆环**，实际却可能同时替换掉原布局中的品牌图标、标签、数值区、状态灯等元素。

你要的交互（示意）：

```
服务额度        服务额度
剩余 68%   →    剩余 68%
[横条]          [圆环]
```

【提取缺失：评审原文此处有两段 SVG 对比示意（横条 ↔ 圆环），导出碎片中仅存残壳，无法复原。】

示意：保留同一个数据源及外围信息，只替换百分比的可视化组件。

正确的抽象应该是：

**Widget 数据实例 → 展示区域（Shell）→ 数据槽位（Slot）→ 兼容的展示组件（Renderer）**

而不是每次选一种样式，就用新模板覆盖整个 Widget 的根组件树。完整根树替换可以作为高级编辑功能，但不应该是普通用户"更换展示样式"的默认语义。

## 四、另外几个值得立即修复的代码问题

| 严重性 | 问题与源码依据 | 影响 |
|--------|--------|--------|
| P0 | `OpenComponentStyleCenter()` 的应用回调捕获最初的 `widget`；每次 `ApplyComponentDirect()` 又从该旧对象复制 `Components` | 在同一个选择窗口中先设置 `tile`，再设置 `row`，后一次保存可能把第一次配置覆盖掉 |
| P1 | `BoundFraction()` 在绑定值缺失时默认回退到 `0` | 数据未知可能被误显示为 0% |
| P1 | `DetailWindow` 使用只传自身状态的 `Render()`，而 `tile`、`row` 已有多数据源映射路径 | `detail` 中声明的跨组件绑定无法按预期解析、更新 |
| P1 | `SampleGalleryWindow` 把目标状态保存在只读 `_targetState`，预览从该快照取值 | 打开组件中心后，外部数据更新不会自动刷新预览 |
| P1 | 选中 `number` 模板后，22 号数字再加摘要；悬浮 `tile` 默认高度按原有布局计算 | 部分样式可能出现高度不足、裁切；需真机确认 |
| P2 | 定向模式只能选模板和 `tile/row/detail`，没有样式参数表单 | 想调整圆环大小、线宽、数值精度等，仍需要编辑组件树 |

尤其是第一条旧对象覆盖问题：这是实际保存路径的问题，不能只通过增加模板测试来解决。

## 五、建议的正确架构

我建议保留 Beacon 已经做好的组件树、绑定引擎、渲染注册表和多展示面机制，但重新定义"选择展示组件"的职责。

推荐的流程：

```
Provider / 数据源        —— 声明数据能力、单位、范围、语义
Typed Data / 统一数据模型 —— percentage、number、money、duration、status…
Compatibility Resolver   —— 数据类型 + 语义 + 展示区域 + 组件能力
普通用户                 —— 选择样式、参数、实时预览
高级用户                 —— 组件树、多源绑定、复杂组合
持久化配置 → Surface Renderer —— tile / row / detail，各自选择和渲染
```

### 1\. 在 Core 中加入真正的数据能力描述

当前 `WidgetTypeDescriptor` 只有类型标识、名称、钉选能力和配置字段，没有输出数据的语义类型；`WidgetState` 只有可选 `Progress` 和字符串 `Payload` 等字段。

建议新增类似如下的模型：

```csharp
public sealed record DataFieldDescriptor(
    string Key,
    DataValueKind Kind,
    string? Unit,
    double? Min,
    double? Max,
    string? Semantic
);

public enum DataValueKind
{
    Percentage,
    Number,
    Money,
    Duration,
    Status,
    Text,
    TimeSeries
}
```

这里的关键不是枚举有多少种，而是**类型能力由 Provider 稳定声明，不依赖某一次请求有没有返回数据**。

例如，某个额度数据声明为 `Percentage`，同时注明语义是 `quota.remaining`。展示组件既知道它能够渲染圆环，也知道 80% 代表剩余而非消耗，不会错误地复用"占用越高越危险"的颜色逻辑。

### 2\. 将组件分成两个层次

**基础渲染组件**：`text`、`metric`、`progress`、`ring`、`pool`、`statusdot` 等。它们是构建 UI 的原子组件，保留现有 `ComponentSurfaceRenderer` 很合理。

**展示模板**：百分比数字卡、环形进度、水平条、水位等。每个模板声明自己需要的数据能力、适用展示面和可配置参数。

现在的 `DisplayTemplates` 已经有了模板注册表雏形，没必要推倒重写。应将：

```csharp
bool RequiresProgress
```

升级为类似：

```csharp
AcceptedKinds
AcceptedSemantics
SupportedSurfaces
Parameters
```

还要让兼容性检查返回原因，而不只是布尔值。比如"适合详情页，但悬浮 tile 空间不足"，用户就能理解为什么某种展示不可选。

### 3\. 把"换样式"和"替换组件树"拆开

对于普通用户，我建议配置中单独记录：

```json
【提取缺失：评审原文此处有一段建议的新配置结构 JSON（含样式/模板身份字段），导出碎片仅存尾部括号，无法复原。】
```

这是**建议的新配置结构，不是当前 Beacon 已支持的格式**。

程序通过这些字段生成组件树，或把对应渲染器安装到固定数据槽位。现有 `WidgetConfig.Components` 则保留为高级自定义树入口。

这样有三个直接收益：能够恢复当前选中的样式；普通配置不需要编辑 JSON；模板升级时不必依赖旧生成树的形状去猜用户原来的选择。

## 六、建议交给 Code Agent 的实施优先级

| 优先级 | 修改位置 | 任务及验收要求 |
|--------|--------|--------|
| P0 | `SettingsWindow.cs`、`SampleGalleryWindow.cs` | 修复空状态时的定向选择断链。新建、离线、首次未取数都可以完成配置 |
| P0 | `SettingsWindow.cs` | 每次应用前通过 Widget ID 读取最新配置，避免连续设置不同 surface 时互相覆盖 |
| P0 | `DisplayTemplates.cs`、`ComponentBindingEngine.cs` | 修复 `52%%`；为 0%、52%、100%、缺失值加入测试 |
| P0 | `PinnedHostWindow.cs`、`WidgetConfig.cs` | 普通样式切换改为替换数据槽位，保留原有品牌、标签、状态等外围信息 |
| P1 | `WidgetTypeDescriptor.cs`、`WidgetState.cs` | 增加 Provider 输出字段的稳定语义描述，区分数据能力与当前值 |
| P1 | `DisplayTemplates.cs` | 将 `RequiresProgress` 改成能力匹配规则；为不同 surface 增加适配要求 |
| P1 | `ComponentSurfaceRenderer.cs` | 区分无数据、过期、有效 0%、有效数值，不能把缺失显示成零 |
| P1 | `DetailWindow.cs` | 使用完整源映射渲染，并响应附加数据源更新 |
| P1 | `SampleGalleryWindow.cs` | 恢复已选模板、订阅实时状态、展示真实参数配置界面 |
| P2 | `PinnedHostWindow.cs`、`TileSizing` | 根据模板测量尺寸，处理圆环、数字卡、横条在不同 tile 档位上的裁切问题 |
| P2 | Settings 外观配置 | 明确全局旧 `progressStyle` 与实例样式配置的优先级，避免两个选择入口互相矛盾 |

### 必须做的端到端验收

我建议在 Windows CI 中至少验证以下流程，而不是只跑 `DisplayTemplates` 的纯函数单测：

【提取缺失：评审原文此处为一个可视化验收清单组件（box），导出碎片仅存「复制验收清单」按钮残壳，清单条目无法复原。落地时以本文件「落地跟踪」表 + todo.md 计划节的验收口径替代。】

以上清单是建议验收标准，不代表我已运行通过。

## 七、最终判断

当前 Beacon 不是没有回应你的需求。**它已经从静态组件展示向可选择、可保存、可运行的展示模板迈进了一步。** `ComponentDescriptor`、绑定引擎、多 surface 和渲染注册表也是值得保留的基础。

但我不建议继续沿着"再加十几种模板"的方向开发，因为眼下有四个根本问题：

1. 类型能力仍由运行时值推断；
2. 样式选择可能替换整个展示区域，而非需要切换的数据槽位；
3. 模板身份、编辑状态没有完整保存与恢复；
4. 离线、连续配置、跨源渲染等实际使用路径存在代码缺陷。

**我的建议是先完成"基于语义的数据槽位 + 能力匹配 + 实例级样式配置"的架构闭环，再扩大组件库。**

这才真正对应你一直强调的要求：**同一个百分比数据，可以在界面中自主选择数字、横条、圆环、水位等适用的展示方式，选择不会改变数据源，也不会要求用户编写组件 JSON。**

本次审核基于 **2026 年 10 月 22 日可访问的 GitHub `main` 分支源码**，已逐条交叉核对关键的 Core、App、Storage 和三类展示面调用链。由于未能在本地完整克隆并运行 Windows/WinUI 程序，以上属于**源码静态审计**，其中裁切和交互体验部分明确标注为待真机验证，不能视为已经完成的运行测试。

（评审原文末尾附有后续服务邀约三条——样式选择不影响布局、扩展示例代码、修复断链及样式恢复的具体步骤——属对话性内容，非评审结论，不录入。）

---

## 落地跟踪

> 性质同 todo.md 台账：评审建议不是仓内事实，落地一条勾一条，括号内补 commit hash。
> 顺序遵循评审 §七结论与 todo.md 计划节：**先架构闭环（跟踪项 1–5、8），再扩模板库。**
> 12 项 = 11 勾 / 1 半勾（2026-10-23 立；2026-10-27 落地 f9a9aac——项 1–10、12 代码面+CI 场景收口，项 11 代码半落地、裁切观感待装机复核；CI 全绿实证 run 40599680681（2026-10-28，0a10853）——f9a9aac 后经 9727f8a/c4b0358/02497d9/0a10853 四轮场景侧收敛（02497d9 含产品侧 ItemClick 合成点击不触发的 SelectionChanged 兜底修复），AE 全段首绿）。

| # | 优先级 | 跟踪项 | 评审出处 | 涉及位置（仓内核验落点） | 任务要点 | 状态 |
|---|--------|--------|--------|--------|--------|------|
| 1 | P0 | 空状态断链 | §二缺陷A / §六P0 | `SettingsWindow.cs:2741` + `SampleGalleryWindow.cs:620,629` | 空状态（新建/离线/首次未取数）也能完成定向配置并应用，不再坠入断链的通用目录 | ☑ 已落地（f9a9aac；空状态定向配置改 targetWidgetId 驱动，enabled:false 断链复现修平；CI 场景 AE1） |
| 2 | P0 | 旧对象覆盖 | §四表P0 / §六P0 | `SettingsWindow.cs:2753`（闭包捕获构造期 `widget`） | 应用前按 Widget ID 读最新配置；同一窗内连续设置不同 surface 不互相覆盖 | ☑ 已落地（f9a9aac；应用/恢复按 Widget Id 重取最新配置，闭包只持 Id；CI 场景 AE3 双面齐断言） |
| 3 | P0 | 52%% 双百分号 | §二缺陷B / §六P0 | `DisplayTemplates.cs:31`（`unit="%"`）× `ComponentBindingEngine` percent 选择器（自带 `%`） | 去重百分号；0%/52%/100%/缺失值四态单测 | ☑ 已落地（f9a9aac；数字模板去 unit=%，percent 选择器单源；0/52/100/缺失四态=DisplayTemplateTests+CI 场景 AE2） |
| 4 | P0 | 样式恢复 | §二缺陷C / §六P1 | `SampleGalleryWindow.cs:146`（恒选 `FirstOrDefault()`） | 重开选择器恢复当前已选模板（保存模板身份或从已落盘树反解），不再默认横条 | ☑ 已落地（f9a9aac；恢复走 DisplayTemplates.Identify 反解 tile→row→detail + 参数回填；CI 场景 AE3 IsSelected 断言） |
| 5 | P0 | 槽位化替换 | §三 / §六P0 | `PinnedHostWindow.cs`（PinTile 见树即整面接管）+ `WidgetConfig.cs` | 普通样式切换替换数据槽位，保留品牌/标签/状态等外围信息；整树替换降级为高级编辑路径 | ☑ 已落地（f9a9aac；PinTile 槽位面=灯/图标/标签外围保留+数据槽换装，灯仍实时；单测+场景 AE1 落盘核验） |
| 6 | P1 | 语义描述 | §五§1 / §六P1 | `WidgetTypeDescriptor.cs`、`WidgetState.cs` | Provider 稳定声明输出字段的类型/单位/范围/语义，区分数据能力与当前值 | ☑ 已落地（f9a9aac；WidgetTypeDescriptor.DataKinds 类型级能力 + 8 家产额 Provider 声明 progress） |
| 7 | P1 | 能力匹配 | §五§2 / §六P1 | `DisplayTemplates.cs:20`（`RequiresProgress` 单布尔） | 升级为能力匹配规则（AcceptedKinds/Semantics/SupportedSurfaces/Parameters），兼容性检查返回原因；surface 适配要求 | ☑ 已落地（f9a9aac；DisplayTemplateKinds 词汇+TemplateCompatibility 带原因+CanApply/surface 白名单；单测覆盖） |
| 8 | P1 | 缺失值区分 | §四表P1 / §六P1 | `ComponentSurfaceRenderer.cs:285`（`BoundFraction` 回退 0） | 区分无数据、过期、有效 0%、有效数值；缺失不得显示为 0% | ☑ 已落地（f9a9aac；进度槽三态 Bound/Static/Missing，缺失出「等待数据」占位；CI 场景 AE2 缺失态断言） |
| 9 | P1 | DetailWindow 源映射 | §四表P1 / §六P1 | `DetailWindow.cs:199`（`Render` 只喂自身 `_state`） | 详情面用完整源映射渲染，并响应附加数据源更新 | ☑ 已落地（f9a9aac；ResolveSources 源映射渲染+ConsumedWidgetIds 变化重渲） |
| 10 | P1 | 实时预览 | §四表P1 / §六P1 | `SampleGalleryWindow.cs:117,145`（只读 `_targetState` 快照） | 订阅实时状态刷新预览 + 恢复已选模板 + 展示真实参数配置界面 | ☑ 已落地（f9a9aac；订阅 WidgetStateChanged 实时重渲+参数滑杆表单+恢复预填） |
| 11 | P2 | 裁切 | §四表P1 / §六P2 | `PinnedHostWindow.cs`、TileSizing 尺寸档 | 按模板测量尺寸；圆环/数字卡/横条在各 tile 档位不裁切（需真机确认，参考 2026-10-14 外框/客户区决策先例） | ◐ 半落地（代码面 f9a9aac：TileHeightDips=max(尺寸档, MinTileHeightDipsOf)，面板行高走全 tile 最大值既有链；裁切观感装机复核未完） |
| 12 | P2 | 优先级统一 | §六P2 | Settings 外观配置（全局 `Appearance.ProgressStyle`） | 明确全局旧 `progressStyle` 与实例级样式配置的优先级，两个选择入口不互相矛盾 | ☑ 已落地（f9a9aac；progressStyle 档加口径声明：只管传统渲染全局额度面，实例样式面不受影响，回传统=组件中心「恢复传统渲染」） |

配套验收要求（评审 §六「端到端验收」条目在导出中丢失，按其可见口径补记）：**不得只跑 `DisplayTemplates` 纯函数单测收工**——P0 各项须有 Windows CI 场景或等价端到端断言（空状态可配置、连续双面应用不互覆、0/52/100/缺失四态渲染、槽位替换保外围），观感与裁切项转装机复核。
