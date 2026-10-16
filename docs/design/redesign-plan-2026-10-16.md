[English](redesign-plan-2026-10-16.en.md) | [中文](redesign-plan-2026-10-16.md)

# beacon 重新设计计划（外部设计稿）

> 来源：https://chatgpt.com/s/t_6ad1038aa6f0819193d8efe0e81a9b7f
> 整理日期：2026-10-16
> **性质：外部建议，不是本仓事实。** 每一条在动手前必须对代码核验；
> 文中提及的「已有实现 / 此前设计」一律按 **未落文档** 处理，先核验再定性。

---


 build.card.severity = Error
```

Selector 可以是纯函数，输入相同则输出相同。

同一份状态可以存在多个 Selector，供不同显示组件复用。

### 4.3 派生状态

派生状态由已有状态计算，不应重复存储为互相矛盾的事实。

例如：

- 额度使用百分比。
- 需要关注的事项数量。
- 是否存在失败任务。
- 多个任务中最严重的状态。
- 任务耗时是否超过预设阈值。

派生状态只有在其依赖发生有效变化时才需要重新计算。

### 4.4 更新粒度

建议使用以下更新策略：

- 状态内容变化：更新对应状态并通知订阅者。
- 仅刷新时间变化：只更新依赖时间的组件。
- 连接断开：更新连接健康与数据新鲜度状态。
- 模板变化：重新加载对应显示组件。
- 布局变化：更新布局，不重新获取业务数据。
- 主题变化：更新设计令牌和视觉属性，不触发网络请求。

## 5. 显示系统重新设计

当前问题不是单纯缺少几种卡片，而是缺少清晰的显示组件分类、统一设计规范，以及组合不同组件的机制。

### 5.1 组件分为三类

**A. 数据组件**

负责提供或派生数据，例如 Provider、Selector 和计算节点。

**B. 显示组件**

负责把输入数据呈现出来，例如数字、徽标、列表、图表和 HTML。

**C. 交互组件**

负责把用户操作转化为 Command，例如按钮、菜单、确认、链接和快捷操作。

三类可以组合，但不应相互绑定成不可拆分的业务组件。

### 5.2 第一批标准显示组件

#### A. 状态与指标

| 组件 | 用途 |
|---|---|
| Metric | 大数字、标签、趋势说明 |
| Metric Group | 多个相关指标的紧凑组合 |
| Status Badge | 成功、失败、运行中等状态 |
| Status Indicator | 圆点、图标、文字组合 |
| Progress Bar | 额度、任务进度、下载进度 |
| Progress Ring | 小尺寸圆环进度 |
| Gauge | 具有明确上下限的仪表指标 |
| Countdown | 距离重置、截止或计划时间 |
| Key-Value | 标签和值的紧凑展示 |
| Comparison | 两个或多个指标的比较 |

#### B. 任务与信息列表

| 组件 | 用途 |
|---|---|
| Status Card | 单个任务或系统的状态摘要 |
| Task List | 多个任务的列表 |
| Event Timeline | 按时间展示状态变化 |
| Activity Feed | 最近事件和操作记录 |
| Data Table | 多字段结构化数据 |
| Ranked List | 按优先级、数值或时间排序 |
| Queue View | 排队、运行和完成任务的简要状态 |
| Error Summary | 失败原因与错误摘要 |
| Empty State | 无数据、无事件或尚未配置 |
| Connection State | 在线、离线、认证失败和过期状态 |

#### C. 数据可视化

| 组件 | 用途 |
|---|---|
| Sparkline | 小尺寸趋势线 |
| Line Chart | 时间序列趋势 |
| Bar Chart | 多项指标对比 |
| Stacked Bar | 多类别组成 |
| Donut Chart | 比例和额度分布 |
| Heatmap | 按时间或类别展示密度 |
| Distribution | 数据分布与区间 |
| Multi-Series Chart | 多指标趋势对比 |

图表必须由有意义的数据驱动。不能为了视觉丰富而让每个组件都带一张没有业务价值的图。

#### D. 布局与组合

| 组件 | 用途 |
|---|---|
| Container | 通用内容容器 |
| Stack | 水平或垂直排列 |
| Grid | 网格布局 |
| Split View | 主次内容分栏 |
| Tabs | 多视图切换 |
| Section | 分区与标题 |
| Divider | 视觉分隔 |
| Conditional | 根据状态显示或隐藏内容 |
| Repeat | 对集合数据重复渲染模板 |
| HTML Screen | 用户自定义 HTML 小屏幕 |

#### E. 交互组件

| 组件 | 用途 |
|---|---|
| Action Button | 执行明确的命令 |
| Icon Button | 紧凑图标动作 |
| Action Menu | 多个可用操作 |
| Link | 打开原始详情 |
| Toggle | 修改本地显示偏好 |
| Acknowledge | 确认或标记已处理 |
| Retry | 请求重新执行或刷新 |
| Confirm Dialog | 高风险操作前确认 |
| Tooltip | 补充信息 |
| Expand / Collapse | 展开更多内容 |

以上是组件库的目标目录，不要求一次全部实现。组件的价值在于能被独立复用和组合，而不是组件数量越多越好。

### 5.3 组件分层

```text
Display Component Library
├── Primitives
│ ├── Text / Icon / Image
│ ├── Badge / Divider / Tooltip
│ └── Surface / Spacing
│
├── Data Components
│ ├── Metric / Progress / Gauge
│ ├── Status / KeyValue
│ ├── List / Table / Timeline
│ └── Charts
│
├── Layout Components
│ ├── Stack / Grid / Split
│ ├── Section / Tabs
│ └── Conditional / Repeat
│
├── Interaction Components
│ ├── Button / Menu / Link
│ ├── Toggle / Acknowledge
│ └── Confirm / Expand
│
└── Composite Components
 ├── Build Status
 ├── AI Usage
 ├── GitHub Summary
 └── Custom HTML Screen
```

Composite Component 是由基础组件组合出来的预设方案，不应该重新实现数据获取或 Action 调用。

### 5.4 原生组件与 HTML 组件共存

原生组件负责最常见、最轻量、与 Windows 交互紧密的显示场景。

HTML 小屏幕负责自定义布局、复杂视觉和用户定义的呈现方式。

两者共享：

- 状态读取接口。
- 数据绑定语义。
- Action ID 与参数约定。
- 主题和设计令牌。
- 组件尺寸与生命周期管理。
- 错误状态和数据更新时间。

这样无需把整个 Beacon 改成网页应用，也不必强迫 HTML 使用者只能使用系统提供的固定卡片。

## 6. 统一视觉设计系统

组件多并不意味着视觉会更好。必须同时建立统一的 Design System。

### 6.1 设计令牌

至少包含：

- 色彩：背景、表面、边框、文字、强调色、状态色。
- 字体：字号、字重、行高、数字字体样式。
- 间距：统一的基础间距阶梯。
- 圆角：小、中、大三档为主。
- 阴影：普通窗口、悬浮窗口和弹出菜单。
- 动效：持续时间、缓动、减少动画模式。
- 尺寸：紧凑、标准、宽松三种密度。
- 状态：正常、运行中、警告、错误、离线、数据过期。

状态不能只通过颜色区分，还应通过图标、文字或其他辅助信息表达。

### 6.2 三种视觉密度

**Compact**

适用于桌面常驻小组件。只显示核心指标和必要状态。

**Comfortable**

适用于 Quick Panel。显示标题、摘要、状态和主要操作。

**Detailed**

适用于详情视图。可以显示更多字段、事件时间线和图表。

同一个组件应尽可能通过尺寸和密度参数适配三种场景，而不是开发三个完全独立的版本。

### 6.3 组件视觉规范

每个组件必须明确：

- 默认尺寸和最小尺寸。
- 内容层级与信息优先级。
- 加载、正常、空数据、错误、离线和过期状态。
- 鼠标悬停、按下、焦点和禁用状态。
- 可访问性名称与键盘操作。
- 是否允许固定到桌面。
- 在小尺寸下如何降级展示。

### 6.4 解决“丑”的具体要求

不能再以「能显示数据」作为组件完成标准。

需要统一检查：

1. 字体层级是否明确。
2. 数字是否足够突出。
3. 边距和对齐是否一致。
4. 信息是否过密或过于空洞。
5. 状态色是否克制且一致。
6. 图标风格是否统一。
7. 长标题和错误文本是否正确截断。
8. 不同尺寸下是否仍然清晰。
9. 主题切换和高对比度模式是否可用。
10. 是否存在无意义的边框、阴影、装饰或重复标签。

应为组件库建立独立的视觉示例页，集中展示组件在不同状态和尺寸下的实际效果。

## 7. HTML 小屏幕架构

HTML 小屏幕是最终形态的重要组成部分，但它不应改变整个 Beacon 的核心架构。

### 7.1 HTML Screen 的组成

```text
HtmlScreenInstance
├── Template
│ ├── HTML
│ └── CSS
├── DataBindings
├── ActionBindings
├── Theme
├── Layout
├── RefreshPolicy
└── RuntimeState
```

HTML 模板描述显示结构，DataBindings 绑定数据，ActionBindings 绑定操作。

每个小屏幕实例拥有自己的配置和布局，但不独立维护外部业务状态。

### 7.2 数据更新方式

推荐：

```text
State Store
 ↓
Selector / Binding
 ↓
Structured Update
 ↓
HTML Bridge
 ↓
Update Bound Data
 ↓
Render Changed Content
```

不要把“每次数据变化就重新生成整份 HTML”作为唯一机制。

数据变化时优先推送结构化数据，模板变化时才重新加载模板。HTML 页面可以根据收到的数据局部更新。

### 7.3 安全边界

- HTML 页面不得直接获取或读取凭据。
- Action 只能通过 Beacon 提供的受控桥接请求。
- 页面不得通过桥接执行任意本地命令。
- 外部导航和网络访问必须受控。
- 模板应限制本地文件访问和未经授权的脚本能力。
- 命令参数必须在 Core 中校验，不能信任页面传入的数据。
- 错误模板不得导致整个 Beacon 崩溃。
- 不允许一个 HTML 小屏幕直接读取其他组件的内部状态。

第一阶段可使用受控 HTML/CSS 模板；需要 JavaScript 时再评估受限运行环境。React 不是必要依赖。

## 8. Action 与反馈架构

### 8.1 Action 定义

Action 需要具有稳定标识、参数契约和明确的执行结果。

例如：

```text
github.workflow.rerun
jenkins.build.open
widget.refresh
notification.acknowledge
system.open-url
```

显示组件只引用 Action ID，不直接引用某个具体执行器实例。

### 8.2 Action 执行流程

```text
UI Interaction
 ↓
Action Request
 ↓
Parameter Validation
 ↓
Policy / Confirmation
 ↓
Action Dispatcher
 ↓
Executor
 ↓
Action Result
 ↓
Operation State + Event
 ↓
UI Feedback
 ↓
Refresh Relevant State
```

继续复用现有 Action Runner 和 Executor。不要额外建立一套与当前系统并行的执行框架。

### 8.3 执行结果

至少支持：

- Succeeded
- Failed
- Cancelled
- TimedOut
- Unsupported

对于异步外部任务，还应区分请求已接受和外部任务最终完成。

### 8.4 用户偏好与业务状态隔离

用户确认、已读、静默、固定组件、显示模式等属于 Beacon 的本地偏好。

它们可以影响通知或显示，但不能直接改写外部系统的真实状态。

## 9. 配置与持久化

继续使用本地 JSON 配置和现有 Storage 抽象。

建议配置职责如下：

| 配置 | 职责 |
|---|---|
| `config.json` | 全局设置、主题、热键、显示密度、通知策略 |
| `connections.json` | Provider 连接、端点和凭据引用 |
| `widgets.json` | 显示实例、数据绑定、组件类型、刷新设置 |
| `pins.json` | 桌面布局、位置、窗口形态 |
| `actions.json` | Action 定义与参数配置 |
| `notifications.json` | 通知历史及已读状态 |
| `cache/` | 必要的状态缓存和最后成功更新时间 |
| `secrets.bin` 或现有安全存储 | 受保护的凭据数据 |

不要为了新架构而无必要地重命名或迁移全部文件。

### 9.1 Widget 实例配置示例

下面是目标模型的示意，不是要求立即固定最终 JSON Schema。

```json
{
 

---

}
# Beacon 响应式数据流与可组合桌面组件架构设计

**文档用途：** 作为 Beacon 后续架构调整、功能扩展和 Code Agent 实施的统一技术基线。

**目标平台：** Windows 10 / Windows 11 
**技术栈：** C#、WinUI 3、.NET 10 
**架构模式：** 单进程模块化应用 + 响应式数据流 + 可组合显示组件 
**核心原则：** 数据与显示分离、状态驱动渲染、命令与状态分离、原生优先、渐进式扩展、本地优先。

---

## 1. 产品重新定位

Beacon 不只是一个展示 GitHub、CI 或 AI 用量的桌面状态面板。

Beacon 是一个运行在 Windows 桌面上的**数据观察、信息呈现与交互反馈系统**。

它持续从不同数据源获取信息，经过标准化和状态处理后，交给不同的显示组件呈现。用户可以通过这些组件观察数据、判断情况并发起操作。操作结果和外部状态变化重新进入系统，推动下一轮状态更新。

整个系统是一条持续运行的闭环：

**数据源 → 数据处理 → 状态 → 显示 → 用户交互 → 命令执行 → 反馈 → 状态更新。**

显示组件只是数据流的消费者和交互入口，而不是业务逻辑的所有者。

### 1.1 核心设计原则

1. **Data First：** 数据源不决定显示形式。
2. **Reactive：** 状态发生有效变化时，自动更新相关消费者。
3. **Composable：** 数据、显示组件、动作和布局可以独立组合。
4. **Single Source of Truth：** 同一份业务状态由 Core 统一管理，不在每个窗口重复维护。
5. **State ≠ Event ≠ Command：** 当前状态、发生的事件和用户发出的命令是不同概念。
6. **Renderer Independent：** 原生组件和 HTML 组件使用相同的数据与动作契约。
7. **Design System First：** 组件共享统一的视觉规范，而不是各自随意设计。
8. **Local First：** 无需服务器、账号系统或云端服务。
9. **Small by Default：** 默认展示重要信息，不将桌面变成大型仪表盘。
10. **No Reinvention：** 直接复用 GitHub、Jenkins 等外部系统的能力，不重复实现它们的业务。

## 2. 总体架构

```text
┌───────────────────────────────────────────────────────────────┐
│ External Systems │
│ │
│ GitHub / Jenkins / AI Usage / HTTP API / Local Data Sources │
└─────────────────────────────┬─────────────────────────────────┘
 │ Pull / Push / Poll
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 1. Data Acquisition │
│ │
│ Connection Providers / Widget Providers / Refresh Scheduler │
│ Authentication / Rate Limit / Retry / Timeout / Cache │
└─────────────────────────────┬─────────────────────────────────┘
 │ Raw Data
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 2. Data Normalization │
│ │
│ Domain Mapping / Validation / Source Metadata / Freshness │
│ External Status → Beacon Domain Model │
└─────────────────────────────┬─────────────────────────────────┘
 │ Normalized State
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 3. Reactive Core │
│ │
│ State Store / State Transitions / Selectors / Event Stream │
│ Derived State / Aggregation / Change Detection │
└──────────────────┬──────────────────────┬────────────────────┘
 │ │
 ▼ ▼
┌─────────────────────────────┐ ┌──────────────────────────────┐
│ 4. Display Composition │ │ 5. Signal & Notification │
│ │ │ │
│ Data Bindings / Selectors │ │ Rules / Thresholds / Dedupe │
│ Component Registry │ │ Windows Toast / Recent Events │
│ Native Renderer │ │ │
│ HTML Renderer │ └──────────────────────────────┘
│ Layout / Theme / Animation │
└──────────────┬──────────────┘
 ▼
┌───────────────────────────────────────────────────────────────┐
│ Windows UI │
│ │
│ Pinned Tiles / Floating Widgets / Quick Panel / Detail / Tray │
└─────────────────────────────┬─────────────────────────────────┘
 │ User Interaction
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 6. Command & Action │
│ │
│ UI Intent → Command → Validation → Authorization → Executor │
│ │ │
│ ▼ │
│ Operation Result │
└─────────────────────────────┬─────────────────────────────────┘
 │ Result Event / State Update
 ▼
 Reactive Core

 Storage: Configuration / Cache / Preferences / History
 Security: Secret Store / Action Policy / HTML Bridge
 Diagnostics: Logs / Errors / Timing / Runtime Health
```

这不是要求一次性实现所有模块，而是明确模块边界及数据流方向。现有代码应优先映射到这些职责，能复用的直接复用。

## 3. 数据流设计

### 3.1 六个阶段

#### 阶段 A：获取数据

Provider 连接外部系统，获取原始数据。

包括：

- 定时轮询。
- 用户手动刷新。
- 外部系统提供的事件推送，前提是数据源本身支持。
- Action 执行后触发的定向刷新。

刷新调度、认证、超时、重试、限流和缓存属于数据获取层。

Provider 不负责绘制 UI，不直接调用显示组件，也不决定通知是否弹出。

#### 阶段 B：标准化

将外部数据映射为 Beacon 可以理解的领域模型。

例如：

```text
Jenkins: SUCCESS
GitHub: completed + success
Other CI: passed
 ↓
Beacon: LifecycleState.Success
```

标准化数据必须保留必要的来源标识、更新时间、数据新鲜度和外部详情链接。

原始响应可以按需保留在有限的诊断数据中，但不能将所有 Provider 的任意字段都无限制地塞进统一状态对象。

#### 阶段 C：更新状态

Core 保存最新状态，并判断：

- 状态是否真正发生变化。
- 哪些派生状态需要重新计算。
- 哪些显示组件依赖该数据。
- 是否需要生成业务事件。
- 是否满足通知规则。

状态相同的轮询结果不应导致所有组件重新绘制。

刷新时间变化与业务内容变化也要区分，避免仅仅因为更新时间改变就产生大量 UI 更新。

#### 阶段 D：展示与判断

显示组件订阅所需的数据，通过统一的 Renderer 展示。

组件可以显示当前状态，也可以组合多个数据源。例如：

- 用量组件显示剩余额度和重置时间。
- 构建组件组合任务状态、进度和耗时。
- HTML 小屏幕组合多个状态源，显示自定义布局。

组件可以接收交互，但不能绕过 Core 直接访问 Provider 或凭据。

#### 阶段 E：命令执行

用户交互产生命令请求。

```text
用户点击 Retry
 ↓
RetryRequested
 ↓
权限与参数校验
 ↓
Action Dispatcher
 ↓
Provider / Action Executor
 ↓
OperationResult
```

命令执行结果与业务状态更新分别处理。

例如，重跑请求被接受，只表示请求已提交，不代表构建已经开始，更不代表构建成功。

#### 阶段 F：反馈

反馈分为两条路径：

1. **操作反馈：** 命令成功提交、失败、取消、超时或不支持。
2. **外部状态反馈：** 外部系统的真实状态发生变化。

两者都可以产生事件、更新相应状态，并触发显示更新。

Beacon 自身的用户偏好，如已读、已确认或静默，也应作为独立的本地状态处理，而不是篡改外部业务状态。

### 3.2 State、Event、Command 与 Operation

| 类型 | 语义 | 示例 |
|---|---|---|
| State | 现在是什么情况 | 构建失败、额度剩余 30% |
| Event | 刚刚发生了什么 | 构建由 Running 变成 Failed |
| Command | 用户希望执行什么 | 重新运行构建 |
| Operation | 命令的执行过程 | 提交请求、等待结果、超时 |
| Operation Result | 命令执行到了什么结果 | 请求已接受、失败、取消 |
| Preference | 用户希望系统如何表现 | 告警已读、组件置顶、静默 |

第一版不需要建立复杂的事件溯源系统。上述概念可以通过现有领域模型、事件总线和 Action Result 逐步落实。

## 4. 响应式状态系统

### 4.1 State Store

建立统一的运行时状态入口，建议命名为 `IStateStore`。

职责：

- 按稳定的状态标识存储最新状态。
- 提供读取快照的能力。
- 发布状态变化。
- 支持订阅与取消订阅。
- 对相同的业务内容进行变更检测。
- 避免多个刷新请求同时覆盖更新后的状态。
- 保证 UI 线程不会直接被后台网络请求阻塞。

初期使用进程内内存实现，不需要引入外部响应式框架或数据库。

### 4.2 Selector

Selector 从现有状态中提取显示组件所需的信息。

例如：

```text
State:
 build.status = Failed
 build.duration = 180s
 build.branch = main

Selector:
 build.card.title = 

---

># Beacon 响应式数据流与可组合桌面组件架构设计

**文档用途：** 作为 Beacon 后续架构调整、功能扩展和 Code Agent 实施的统一技术基线。

**目标平台：** Windows 10 / Windows 11 
**技术栈：** C#、WinUI 3、.NET 10 
**架构模式：** 单进程模块化应用 + 响应式数据流 + 可组合显示组件 
**核心原则：** 数据与显示分离、状态驱动渲染、命令与状态分离、原生优先、渐进式扩展、本地优先。

---

## 1. 产品重新定位

Beacon 不只是一个展示 GitHub、CI 或 AI 用量的桌面状态面板。

Beacon 是一个运行在 Windows 桌面上的**数据观察、信息呈现与交互反馈系统**。

它持续从不同数据源获取信息，经过标准化和状态处理后，交给不同的显示组件呈现。用户可以通过这些组件观察数据、判断情况并发起操作。操作结果和外部状态变化重新进入系统，推动下一轮状态更新。

整个系统是一条持续运行的闭环：

**数据源 → 数据处理 → 状态 → 显示 → 用户交互 → 命令执行 → 反馈 → 状态更新。**

显示组件只是数据流的消费者和交互入口，而不是业务逻辑的所有者。

### 1.1 核心设计原则

1. **Data First：** 数据源不决定显示形式。
2. **Reactive：** 状态发生有效变化时，自动更新相关消费者。
3. **Composable：** 数据、显示组件、动作和布局可以独立组合。
4. **Single Source of Truth：** 同一份业务状态由 Core 统一管理，不在每个窗口重复维护。
5. **State ≠ Event ≠ Command：** 当前状态、发生的事件和用户发出的命令是不同概念。
6. **Renderer Independent：** 原生组件和 HTML 组件使用相同的数据与动作契约。
7. **Design System First：** 组件共享统一的视觉规范，而不是各自随意设计。
8. **Local First：** 无需服务器、账号系统或云端服务。
9. **Small by Default：** 默认展示重要信息，不将桌面变成大型仪表盘。
10. **No Reinvention：** 直接复用 GitHub、Jenkins 等外部系统的能力，不重复实现它们的业务。

## 2. 总体架构

```text
┌───────────────────────────────────────────────────────────────┐
│ External Systems │
│ │
│ GitHub / Jenkins / AI Usage / HTTP API / Local Data Sources │
└─────────────────────────────┬─────────────────────────────────┘
 │ Pull / Push / Poll
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 1. Data Acquisition │
│ │
│ Connection Providers / Widget Providers / Refresh Scheduler │
│ Authentication / Rate Limit / Retry / Timeout / Cache │
└─────────────────────────────┬─────────────────────────────────┘
 │ Raw Data
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 2. Data Normalization │
│ │
│ Domain Mapping / Validation / Source Metadata / Freshness │
│ External Status → Beacon Domain Model │
└─────────────────────────────┬─────────────────────────────────┘
 │ Normalized State
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 3. Reactive Core │
│ │
│ State Store / State Transitions / Selectors / Event Stream │
│ Derived State / Aggregation / Change Detection │
└──────────────────┬──────────────────────┬────────────────────┘
 │ │
 ▼ ▼
┌─────────────────────────────┐ ┌──────────────────────────────┐
│ 4. Display Composition │ │ 5. Signal & Notification │
│ │ │ │
│ Data Bindings / Selectors │ │ Rules / Thresholds / Dedupe │
│ Component Registry │ │ Windows Toast / Recent Events │
│ Native Renderer │ │ │
│ HTML Renderer │ └──────────────────────────────┘
│ Layout / Theme / Animation │
└──────────────┬──────────────┘
 ▼
┌───────────────────────────────────────────────────────────────┐
│ Windows UI │
│ │
│ Pinned Tiles / Floating Widgets / Quick Panel / Detail / Tray │
└─────────────────────────────┬─────────────────────────────────┘
 │ User Interaction
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 6. Command & Action │
│ │
│ UI Intent → Command → Validation → Authorization → Executor │
│ │ │
│ ▼ │
│ Operation Result │
└─────────────────────────────┬─────────────────────────────────┘
 │ Result Event / State Update
 ▼
 Reactive Core

 Storage: Configuration / Cache / Preferences / History
 Security: Secret Store / Action Policy / HTML Bridge
 Diagnostics: Logs / Errors / Timing / Runtime Health
```

这不是要求一次性实现所有模块，而是明确模块边界及数据流方向。现有代码应优先映射到这些职责，能复用的直接复用。

## 3. 数据流设计

### 3.1 六个阶段

#### 阶段 A：获取数据

Provider 连接外部系统，获取原始数据。

包括：

- 定时轮询。
- 用户手动刷新。
- 外部系统提供的事件推送，前提是数据源本身支持。
- Action 执行后触发的定向刷新。

刷新调度、认证、超时、重试、限流和缓存属于数据获取层。

Provider 不负责绘制 UI，不直接调用显示组件，也不决定通知是否弹出。

#### 阶段 B：标准化

将外部数据映射为 Beacon 可以理解的领域模型。

例如：

```text
Jenkins: SUCCESS
GitHub: completed + success
Other CI: passed
 ↓
Beacon: LifecycleState.Success
```

标准化数据必须保留必要的来源标识、更新时间、数据新鲜度和外部详情链接。

原始响应可以按需保留在有限的诊断数据中，但不能将所有 Provider 的任意字段都无限制地塞进统一状态对象。

#### 阶段 C：更新状态

Core 保存最新状态，并判断：

- 状态是否真正发生变化。
- 哪些派生状态需要重新计算。
- 哪些显示组件依赖该数据。
- 是否需要生成业务事件。
- 是否满足通知规则。

状态相同的轮询结果不应导致所有组件重新绘制。

刷新时间变化与业务内容变化也要区分，避免仅仅因为更新时间改变就产生大量 UI 更新。

#### 阶段 D：展示与判断

显示组件订阅所需的数据，通过统一的 Renderer 展示。

组件可以显示当前状态，也可以组合多个数据源。例如：

- 用量组件显示剩余额度和重置时间。
- 构建组件组合任务状态、进度和耗时。
- HTML 小屏幕组合多个状态源，显示自定义布局。

组件可以接收交互，但不能绕过 Core 直接访问 Provider 或凭据。

#### 阶段 E：命令执行

用户交互产生命令请求。

```text
用户点击 Retry
 ↓
RetryRequested
 ↓
权限与参数校验
 ↓
Action Dispatcher
 ↓
Provider / Action Executor
 ↓
OperationResult
```

命令执行结果与业务状态更新分别处理。

例如，重跑请求被接受，只表示请求已提交，不代表构建已经开始，更不代表构建成功。

#### 阶段 F：反馈

反馈分为两条路径：

1. **操作反馈：** 命令成功提交、失败、取消、超时或不支持。
2. **外部状态反馈：** 外部系统的真实状态发生变化。

两者都可以产生事件、更新相应状态，并触发显示更新。

Beacon 自身的用户偏好，如已读、已确认或静默，也应作为独立的本地状态处理，而不是篡改外部业务状态。

### 3.2 State、Event、Command 与 Operation

| 类型 | 语义 | 示例 |
|---|---|---|
| State | 现在是什么情况 | 构建失败、额度剩余 30% |
| Event | 刚刚发生了什么 | 构建由 Running 变成 Failed |
| Command | 用户希望执行什么 | 重新运行构建 |
| Operation | 命令的执行过程 | 提交请求、等待结果、超时 |
| Operation Result | 命令执行到了什么结果 | 请求已接受、失败、取消 |
| Preference | 用户希望系统如何表现 | 告警已读、组件置顶、静默 |

第一版不需要建立复杂的事件溯源系统。上述概念可以通过现有领域模型、事件总线和 Action Result 逐步落实。

## 4. 响应式状态系统

### 4.1 State Store

建立统一的运行时状态入口，建议命名为 `IStateStore`。

职责：

- 按稳定的状态标识存储最新状态。
- 提供读取快照的能力。
- 发布状态变化。
- 支持订阅与取消订阅。
- 对相同的业务内容进行变更检测。
- 避免多个刷新请求同时覆盖更新后的状态。
- 保证 UI 线程不会直接被后台网络请求阻塞。

初期使用进程内内存实现，不需要引入外部响应式框架或数据库。

### 4.2 Selector

Selector 从现有状态中提取显示组件所需的信息。

例如：

```text
State:
 build.status = Failed
 build.duration = 180s
 build.branch = main

Selector:
 build.card.title = 

---

# Beacon 响应式数据流与可组合桌面组件架构设计

**文档用途：** 作为 Beacon 后续架构调整、功能扩展和 Code Agent 实施的统一技术基线。

**目标平台：** Windows 10 / Windows 11 
**技术栈：** C#、WinUI 3、.NET 10 
**架构模式：** 单进程模块化应用 + 响应式数据流 + 可组合显示组件 
**核心原则：** 数据与显示分离、状态驱动渲染、命令与状态分离、原生优先、渐进式扩展、本地优先。

---

## 1. 产品重新定位

Beacon 不只是一个展示 GitHub、CI 或 AI 用量的桌面状态面板。

Beacon 是一个运行在 Windows 桌面上的**数据观察、信息呈现与交互反馈系统**。

它持续从不同数据源获取信息，经过标准化和状态处理后，交给不同的显示组件呈现。用户可以通过这些组件观察数据、判断情况并发起操作。操作结果和外部状态变化重新进入系统，推动下一轮状态更新。

整个系统是一条持续运行的闭环：

**数据源 → 数据处理 → 状态 → 显示 → 用户交互 → 命令执行 → 反馈 → 状态更新。**

显示组件只是数据流的消费者和交互入口，而不是业务逻辑的所有者。

### 1.1 核心设计原则

1. **Data First：** 数据源不决定显示形式。
2. **Reactive：** 状态发生有效变化时，自动更新相关消费者。
3. **Composable：** 数据、显示组件、动作和布局可以独立组合。
4. **Single Source of Truth：** 同一份业务状态由 Core 统一管理，不在每个窗口重复维护。
5. **State ≠ Event ≠ Command：** 当前状态、发生的事件和用户发出的命令是不同概念。
6. **Renderer Independent：** 原生组件和 HTML 组件使用相同的数据与动作契约。
7. **Design System First：** 组件共享统一的视觉规范，而不是各自随意设计。
8. **Local First：** 无需服务器、账号系统或云端服务。
9. **Small by Default：** 默认展示重要信息，不将桌面变成大型仪表盘。
10. **No Reinvention：** 直接复用 GitHub、Jenkins 等外部系统的能力，不重复实现它们的业务。

## 2. 总体架构

```text
┌───────────────────────────────────────────────────────────────┐
│ External Systems │
│ │
│ GitHub / Jenkins / AI Usage / HTTP API / Local Data Sources │
└─────────────────────────────┬─────────────────────────────────┘
 │ Pull / Push / Poll
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 1. Data Acquisition │
│ │
│ Connection Providers / Widget Providers / Refresh Scheduler │
│ Authentication / Rate Limit / Retry / Timeout / Cache │
└─────────────────────────────┬─────────────────────────────────┘
 │ Raw Data
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 2. Data Normalization │
│ │
│ Domain Mapping / Validation / Source Metadata / Freshness │
│ External Status → Beacon Domain Model │
└─────────────────────────────┬─────────────────────────────────┘
 │ Normalized State
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 3. Reactive Core │
│ │
│ State Store / State Transitions / Selectors / Event Stream │
│ Derived State / Aggregation / Change Detection │
└──────────────────┬──────────────────────┬────────────────────┘
 │ │
 ▼ ▼
┌─────────────────────────────┐ ┌──────────────────────────────┐
│ 4. Display Composition │ │ 5. Signal & Notification │
│ │ │ │
│ Data Bindings / Selectors │ │ Rules / Thresholds / Dedupe │
│ Component Registry │ │ Windows Toast / Recent Events │
│ Native Renderer │ │ │
│ HTML Renderer │ └──────────────────────────────┘
│ Layout / Theme / Animation │
└──────────────┬──────────────┘
 ▼
┌───────────────────────────────────────────────────────────────┐
│ Windows UI │
│ │
│ Pinned Tiles / Floating Widgets / Quick Panel / Detail / Tray │
└─────────────────────────────┬─────────────────────────────────┘
 │ User Interaction
 ▼
┌───────────────────────────────────────────────────────────────┐
│ 6. Command & Action │
│ │
│ UI Intent → Command → Validation → Authorization → Executor │
│ │ │
│ ▼ │
│ Operation Result │
└─────────────────────────────┬─────────────────────────────────┘
 │ Result Event / State Update
 ▼
 Reactive Core

 Storage: Configuration / Cache / Preferences / History
 Security: Secret Store / Action Policy / HTML Bridge
 Diagnostics: Logs / Errors / Timing / Runtime Health
```

这不是要求一次性实现所有模块，而是明确模块边界及数据流方向。现有代码应优先映射到这些职责，能复用的直接复用。

## 3. 数据流设计

### 3.1 六个阶段

#### 阶段 A：获取数据

Provider 连接外部系统，获取原始数据。

包括：

- 定时轮询。
- 用户手动刷新。
- 外部系统提供的事件推送，前提是数据源本身支持。
- Action 执行后触发的定向刷新。

刷新调度、认证、超时、重试、限流和缓存属于数据获取层。

Provider 不负责绘制 UI，不直接调用显示组件，也不决定通知是否弹出。

#### 阶段 B：标准化

将外部数据映射为 Beacon 可以理解的领域模型。

例如：

```text
Jenkins: SUCCESS
GitHub: completed + success
Other CI: passed
 ↓
Beacon: LifecycleState.Success
```

标准化数据必须保留必要的来源标识、更新时间、数据新鲜度和外部详情链接。

原始响应可以按需保留在有限的诊断数据中，但不能将所有 Provider 的任意字段都无限制地塞进统一状态对象。

#### 阶段 C：更新状态

Core 保存最新状态，并判断：

- 状态是否真正发生变化。
- 哪些派生状态需要重新计算。
- 哪些显示组件依赖该数据。
- 是否需要生成业务事件。
- 是否满足通知规则。

状态相同的轮询结果不应导致所有组件重新绘制。

刷新时间变化与业务内容变化也要区分，避免仅仅因为更新时间改变就产生大量 UI 更新。

#### 阶段 D：展示与判断

显示组件订阅所需的数据，通过统一的 Renderer 展示。

组件可以显示当前状态，也可以组合多个数据源。例如：

- 用量组件显示剩余额度和重置时间。
- 构建组件组合任务状态、进度和耗时。
- HTML 小屏幕组合多个状态源，显示自定义布局。

组件可以接收交互，但不能绕过 Core 直接访问 Provider 或凭据。

#### 阶段 E：命令执行

用户交互产生命令请求。

```text
用户点击 Retry
 ↓
RetryRequested
 ↓
权限与参数校验
 ↓
Action Dispatcher
 ↓
Provider / Action Executor
 ↓
OperationResult
```

命令执行结果与业务状态更新分别处理。

例如，重跑请求被接受，只表示请求已提交，不代表构建已经开始，更不代表构建成功。

#### 阶段 F：反馈

反馈分为两条路径：

1. **操作反馈：** 命令成功提交、失败、取消、超时或不支持。
2. **外部状态反馈：** 外部系统的真实状态发生变化。

两者都可以产生事件、更新相应状态，并触发显示更新。

Beacon 自身的用户偏好，如已读、已确认或静默，也应作为独立的本地状态处理，而不是篡改外部业务状态。

### 3.2 State、Event、Command 与 Operation

| 类型 | 语义 | 示例 |
|---|---|---|
| State | 现在是什么情况 | 构建失败、额度剩余 30% |
| Event | 刚刚发生了什么 | 构建由 Running 变成 Failed |
| Command | 用户希望执行什么 | 重新运行构建 |
| Operation | 命令的执行过程 | 提交请求、等待结果、超时 |
| Operation Result | 命令执行到了什么结果 | 请求已接受、失败、取消 |
| Preference | 用户希望系统如何表现 | 告警已读、组件置顶、静默 |

第一版不需要建立复杂的事件溯源系统。上述概念可以通过现有领域模型、事件总线和 Action Result 逐步落实。

## 4. 响应式状态系统

### 4.1 State Store

建立统一的运行时状态入口，建议命名为 `IStateStore`。

职责：

- 按稳定的状态标识存储最新状态。
- 提供读取快照的能力。
- 发布状态变化。
- 支持订阅与取消订阅。
- 对相同的业务内容进行变更检测。
- 避免多个刷新请求同时覆盖更新后的状态。
- 保证 UI 线程不会直接被后台网络请求阻塞。

初期使用进程内内存实现，不需要引入外部响应式框架或数据库。

### 4.2 Selector

Selector 从现有状态中提取显示组件所需的信息。

例如：

```text
State:
 build.status = Failed
 build.duration = 180s
 build.branch = main

Selector:
 build.card.title = 

---

: true
 }
}
```

一个显示实例可以绑定多个数据源；同一个数据源也可以被多个实例复用。

布局、显示、数据绑定和动作配置应尽可能分开管理，以便分别修改和校验。

## 10. 项目模块划分

继续保留现有项目，不进行无必要的拆分。

```text
Beacon.App
├── Windows Shell
├── Tray / Hotkey
├── Window Host
├── Quick Panel / Detail / Settings
├── Native Renderer
├── HTML Renderer Host
└── UI Composition Root

Beacon.Core
├── Domain Models
├── Provider Contracts
├── State Store
├── State Transitions
├── Selectors
├── Event Bus
├── Refresh Scheduler
├── Notification Engine
├── Action Dispatcher / Runner
├── Component Contracts
├── Binding Engine
└── Layout / Theme Contracts

Beacon.Connections
├── Existing GitHub Provider
├── Existing Jenkins Provider
├── Existing HTTP Provider
├── Existing AI Usage Providers
└── Existing Local / Other Providers

Beacon.Actions
├── Existing Action Executors
└── Action Result Mapping

Beacon.Storage
├── Existing Configuration Store
├── Existing Cache Store
├── Existing Secret Store
├── Widget Instance Configuration
└── Configuration Migration
```

依赖规则：

- Core 不依赖 WinUI、WebView2 或具体 Provider。
- Connections、Actions、Storage 只依赖 Core。
- App 是依赖注入和运行时组装入口。
- 显示组件不直接调用 Connections。
- HTML 页面只能通过受控桥接访问数据与 Action。
- 原生 Renderer 和 HTML Renderer 遵循同一套组件契约。
- 不新增后台服务、独立数据库或云端服务。

## 11. 实施阶段

### Phase 0：现状审计与契约冻结

先核对现有实现，而不是立即重写。

- 列出现有 Provider、Widget、Action 和 Renderer。
- 核对 `WidgetState`、事件总线、刷新调度和 Action Runner。
- 识别现有状态模型中哪些字段是业务状态，哪些只是显示字段。
- 确定新组件模型与旧配置的兼容方案。
- 保留现有已完成的功能和真机验收要求。

交付：现状清单、目标模型映射表、差异与迁移计划。

### Phase 1：响应式状态与数据流

- 明确 State、Event、Command 和 Operation Result。
- 完善状态变更检测。
- 增加或整理统一的 State Store 和 Selector。
- 明确订阅、取消订阅、刷新失败和过期状态的处理方式。
- 确保 UI 只消费状态，不直接请求外部数据。

交付：状态流契约、单元测试和数据流示例。

### Phase 2：设计系统与基础组件库

优先实现：

- Text、Icon、Surface、Badge、Divider。
- Metric、Progress、Status Indicator。
- Key-Value、Status Card、List。
- Action Button、Action Menu、Link。
- Stack、Grid、Section、Conditional。
- Empty、Loading、Error、Offline 等通用状态。

同时建立统一的主题、尺寸、排版和视觉示例页。

交付：可复用的原生组件库及完整状态示例。

### Phase 3：组件组合与数据绑定

- 定义 Component Descriptor。
- 定义 Widget Instance 与 Binding 配置。
- 实现 Selector 到组件属性的绑定。
- 实现多个数据源组合。
- 支持不同组件消费同一份状态。
- 保证修改布局不会触发无意义的数据请求。

交付：配置驱动的组合实例及测试。

### Phase 4：HTML 小屏幕

- 实现 HTML/CSS 模板加载。
- 实现受控数据桥接。
- 实现 Action ID 桥接。
- 实现模板错误回退、实例生命周期和安全限制。
- 实现主题令牌与基本样式适配。
- 验证数据变化时可以增量更新。

交付：一个 HTML 小屏幕示例，组合真实的 GitHub 或构建数据。

### Phase 5：完善组件目录与用户体验

逐步补充：

- 时间线、表格、图表和趋势组件。
- 更丰富的布局和条件显示。
- 组件实例编辑、尺寸调整与桌面布局配置。
- 统一的空数据、离线和错误状态。
- 主题、密度和可访问性完善。

每增加一种组件，必须证明它能复用现有数据流与动作体系，不允许为每种展示形式重复建设 Provider。

### Phase 6：回归与真机验收

- 验证刷新、状态更新和命令执行闭环。
- 验证多窗口订阅与取消订阅。
- 验证显示器切换、DPI、窗口拖动和重启恢复。
- 验证 HTML 安全边界和错误恢复。
- 验证缓存、断网、凭据错误和操作失败。
- 验证现有 Provider、AI 额度显示和通知规则不回归。

## 12. 验收标准

新架构完成的判断标准不是组件数量，而是以下能力成立：

1. 新增 Provider 不需要修改显示组件实现。
2. 新增显示组件不需要修改 Provider。
3. 同一份数据可以被不同类型的组件展示。
4. 一个显示实例可以组合多个数据源。
5. 用户操作通过统一 Action 机制执行。
6. 操作结果不会被误认为外部业务状态。
7. 数据变化只更新必要的消费者。
8. HTML 与原生组件共享相同的数据与动作契约。
9. 组件拥有统一的视觉、尺寸、状态和可访问性规范。
10. 断网、过期、刷新失败和模板错误不会导致界面崩溃或错误地显示成功。
11. 所有敏感凭据仍由安全存储管理。
12. 现有功能、配置和真机验收能力得到保留。

## 13. 明确不做

本次架构调整不意味着 Beacon 变成一个通用低代码平台。

长期边界仍然是：

- 不实现通用 Workflow Engine。
- 不实现任意脚本执行平台。
- 不实现远程机器和服务器管理平台。
- 不实现云端账号、团队协作或多设备控制。
- 不实现第三方插件市场。
- 不要求所有显示组件使用 HTML。
- 不要求所有 HTML 小屏幕使用 React。
- 不实现完整的事件溯源系统。
- 不为每种外部数据源复制一套 UI。
- 不为了展示效果而引入不必要的复杂图表和动画。

## 14. 最终架构原则

Beacon 的核心是稳定的数据流与可组合的显示系统。

Provider 负责获取数据；Core 负责标准化、状态管理、变化检测和派生状态；显示组件负责呈现；Action 负责执行用户意图；执行结果和外部状态变化重新反馈给 Core。

原生组件和 HTML 小屏幕只是两种不同的呈现方式。它们不应该拥有独立的业务状态，也不应该绕过统一的 Action 和安全机制。

最终，Beacon 应该能够通过组合已有的数据、显示组件和动作，构建不同的桌面信息终端，而不是每增加一个需求就重新开发一张专用卡片。

**实现优先级：先让数据流正确，再让组件可组合，最后让展示足够丰富且美观。**</WritingBlock>

## 我的实施建议

这份方案最重要的不是一次性增加几十种组件，而是先把已有代码中三种职责拆清楚：

- `WidgetState` 负责业务状态，不再同时承担所有展示需求。
- `WidgetTypeDescriptor` 逐步演进为显示组件可声明的能力描述，避免把业务 Widget 类型和视觉组件类型混在一起。
- `WidgetHost` 继续承担数据获取和生命周期职责，但不要把它扩展成显示组件管理器。

另外，**第一批应优先完成基础视觉组件、Metric、Progress、Status Card、List、Timeline 和布局容器**。这些组件能覆盖大多数开发者状态场景，也能为后续 HTML 小屏幕提供稳定的设计语言。

在开始实施前，建议先让 Code Agent 审计现有代码，并输出「现有实现 → 新架构」映射及必要的迁移步骤。不要让它直接重写整套 Core。

---

If you want, I can:

- 继续详细说明Action与反馈架构
- 说明如何实现响应式状态系统
- 展开描述组件视觉规范和设计系统

---

# Beacon 重新设计：响应式数据流与可组合桌面组件架构

这次我会从你刚刚明确的核心思想重新整理，而不是继续在原有的「状态胶囊 + 几种悬浮卡片」上打补丁。

我重新核对了 Beacon 仓库的产品方案、技术 RFC、项目结构以及现有 `WidgetState`、`WidgetTypeDescriptor`、`WidgetHost` 等实现。当前已经有 Provider、状态缓存、事件总线、刷新调度、Action 执行器和桌面 Widget 基础，因此**不需要推翻现有工程，而是要重新明确数据流、显示系统和交互反馈之间的契约，再扩展显示能力和视觉体系**。

这次设计有四个关键变化：

1. **从固定 Widget 类型转向可组合的显示组件。** 数据源、展示形式、交互动作不再绑定成一个整体。
2. **从刷新后更新卡片转向响应式数据流。** 状态、事件、用户命令、执行结果有明确的流转关系。
3. **从少量固定外观转向完整的显示组件库。** 包含数字、状态、进度、列表、时间线、图表、组合布局，以及最终的 HTML 小屏幕。
4. **从统一模板转向统一设计系统。** 所有组件共享视觉令牌、排版、间距、状态表达和交互规范，但允许用户定制。

产品定位仍然是轻量的 Windows 开发者状态与操作中心，不扩张成通用监控平台、工作流引擎或远程运维平台。技术基础继续采用 C#、WinUI 3、.NET 10 和本地优先架构。

:::writing{variant=