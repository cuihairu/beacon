[English](phase4-html-ruling-2026-10-17.en.md) | [中文](phase4-html-ruling-2026-10-17.md)

# Phase 4 HTML 小屏幕 · 边界裁定与架构决策（2026-10-17）

## 1. 裁定记录

用户 2026-10-17 令：「Phase 4 HTML 小屏幕（6 条，开工前须先出架构决策，仓内有反向记录）……这个设计下吧」。

本文件即开工前的架构决策交付物，回答 `redesign-2026-10-16-契约.md` §4 留问（HTML 小屏幕与
`master-plan.md:769`「不引入 WebView」的边界，映射与迁移 §6.3）。留问自本文件起关闭。

## 2. 与反向记录的和解

仓内反向记录（`docs/master-plan.md:769`）：「悬浮窗口应尽可能使用原生 Windows 能力实现，
不引入 WebView 或其他 UI 框架来重复实现桌面外壳」。

设计稿自身（`redesign-plan-2026-10-16.md` §7 末行）：「第一阶段可使用受控 HTML/CSS 模板；
需要 JavaScript 时再评估受限运行环境。React 不是必要依赖。」

两者不冲突：设计稿的第一阶段不要求 JavaScript，而 Beacon 现有渲染段已是声明式组件树
（Phase 3 `ComponentDescriptor` / `ComponentSurfaceRenderer`）。因此第一阶段裁定为——

> **受控 HTML/CSS 子集 + 原生渲染。无 JavaScript，无 WebView2，无 React。**
> HTML 模板在 Core 内解析为 `ComponentDescriptor` 子树，交给既有声明式渲染器用
> WinUI 原生控件绘制。HTML 只是「另一种写组件树的语言」，不是另一种运行时。

需要 JavaScript 才能表达的面（任意交互逻辑、外部脚本）超出本裁定边界，届时须新开裁定。

## 3. 载体与展开点

- 载体：任一面键（tile / row / detail，面键开集口径不变）的根节点声明为
  `{ "type": "htmltemplate", "props": { "html": "<模板文本>" } }`。零新模型字段、零新 JSON
  顶层键、零新面键——HTML 是节点类型，不是新的存储形态。
- 展开点：`WidgetInstanceResolver.Resolve` 纯函数内，`HtmlTemplateParser` 把模板文本解析为
  组件子树（wrapper column），再过既有 `ComponentTreeValidator`（8 层 / 32 子 / 256 节点
  防爆半径同样约束合成树）。展开后 tile / row / detail 四个消费面零改动即获得 HTML 面。
- 解析器为静态纯函数（守已代决⑩：无 IO、无运行时服务、无缓存）；解析失败返回 null，
  调用面回传统渲染——与既有「非法树整树弃用」同口径。

## 4. 模板格式（封闭词汇表）

语法骨架：可选 `<style>` 块（必须先于其他元素）+ 元素树。双引号属性；`<!-- 注释 -->`
可跳过；`&` 实体与裸 `&` 一律解析错（状态文本词汇用不上，保持封闭）。

元素白名单（映射到既有渲染注册表类型键）：

| 元素 | 映射类型键 | 专有属性 | 绑定槽 |
|---|---|---|---|
| card | card | — | — |
| column / row | column / row | gap | — |
| text | text | size(body/caption) | text |
| section | sectionlabel | — | text |
| metric | metric | unit | value |
| progress | progress | width | progress |
| dot | statusdot | size | severity |
| badge | badge | — | text |
| kv | keyvalue | key | value |
| divider | divider（自闭合） | — | — |
| button | actionbutton | data-action（必填） | text（按钮文案） |

全元素通用属性：`class`、`when`（条件显示，透传 ComponentVisibility）。未知元素、未知属性、
专有属性误挂 → 解析错。词汇表即白名单：`<script>`、`<iframe>`、`<img>`、`<a href>`、`on*`
事件属性、外链协议在语法上**不可表达**——不是「过滤危险输入」，是词汇表里根本没有。

## 5. 受控数据桥接

插值 `{{...}}` 脱糖为既有 `ComponentBinding`（绑定引擎零改动）：

- `{{state.summary}}` → state 源，字段名须在引擎封闭字段集内（summary / severity /
  progress / detailUrl / lifecycle / connectionId / widgetId / widgetType）；
- `{{payload.run_id}}` → state 源 `payload:run_id` 形（键非空、无二级冒号）；
- `{{selector.value-hint}}` → selector 源，名字须在选择器注册表（value-hint / quota-windows）；
- 文本内容混合插值 → 拆为 literal / 绑定子节点序列；属性值整体为单个插值 → 该属性槽绑定，
  属性值混合字面与插值 → 解析错（宁报错不猜）；`when` / `gap` / `class` / `size` / `unit` /
  `key` / `width` / `data-action` 不接受插值（属性是配置面，数据面只有绑定槽）。

取不到值回空串（绑定引擎既有 fail-closed 口径），不炸进程、不留占位垃圾。

## 6. Action ID 桥接

`<button data-action="rerun-failed">重跑失败</button>` → `actionbutton` 节点，动作 ID 只接受
**静态字面**（无插值、无空白）——设计稿 §7.3「不能信任页面传入的数据」：不让远端状态数据
挑选动作，动作面只能由本地配置声明。

执行面在渲染侧（App，类型词汇归渲染侧的已代决⑤口径）：按 actions.json + 连接 DSL 动作全集
查 ID，`ActionVisibility` 过滤（widgetType / connection_id 限定照常生效）；不存在或不可见 →
渲染诊断占位（不渲染可点按钮）；存在且可见 → 既有 `ActionRunner.ExecuteAsync` 路径执行，
`RequireConfirmation` 照常。模板声明了一个不存在的动作 ID 不炸进程——面照常渲染，按钮位出
诊断文字。

## 7. 主题令牌与基本样式适配

`<style>` 块只收 `.class { prop: value; }` 形（一类一规则，无组合器、无伪类）：

- prop 白名单：`tone`（info / ok / warn / bad）、`pad`（sm / md / lg）、`gap`（sm / md / lg）；
- value 只接受令牌词；`#hex`、`rgb()`、具名 CSS 颜色等字面色一律解析错——**模板在语法上
  不可能硬编码颜色**，浅色 / 深色 / 高对比自动适配（渲染经 ThemeColors / UiPalette 档位色）；
- sm / md / lg 的 DIP 换算在渲染侧（sm=4 / md=8 / lg=12，对齐 DesignTokens Space4/8/12），
  Core 只存令牌词——类型换算留渲染期（Props 全字符串的既定口径）；
- 同名 class 重复定义、未知 prop、未知令牌词 → 解析错。

## 8. 增量更新语义

- 模板解析一次 / 配置修订：实例不可变、随配置解析（改布局只重解析的既定不变量），
  模板文本只在配置变化时重新解析；
- 数据到达 = 既有绑定重求值 + 面重渲（Phase 3 整树重渲口径，树 ≤256 节点）；
  **不把「每次数据变化重新生成整份 HTML」作为机制**（设计稿 §7.2）——HTML 文本根本不参与
  数据更新路径，更新走的是结构化绑定（引擎对同一棵合成树按新状态快照求值）；
- Core 可测事实：同一实例、两份状态快照 → 绑定值不同、合成树对象不变。

## 9. 实例生命周期与错误回退

- 实例生命周期 = 既有 WidgetInstance 生命周期：配置加载 / 变更时解析，只读共享，无独立
  运行时状态、无定时器、无后台任务（HTML 面不引入新的生命周期形态）；
- 错误回退：解析失败 / 越词汇表 → 该面弃用回传统渲染 + 诊断（加载侧进 LoadErrors，
  解析侧 fail-closed null）——错误模板不能崩溃常驻进程，也不能静默裁剪成半棵；
- 存储层 `SanitizeWidgetComponents` 加载即展开校验：坏模板在加载时报错并被弃用，
  不等首次渲染才发现。

## 10. 明确不在本裁定内

- JavaScript / 受限脚本运行环境 / WebView2 / React——需要时须新开用户裁定；
- 任意 CSS 样式（布局模型、字体、动效、图片）——词汇表扩白名单属后续演进，逐项评审；
- 模板间共享样式、外部模板文件、模板热加载——模板只随 widgets.json 走；
- 跨 widget 数据读取（附加源声明照 Phase 3 既有口径，白名单校验不变）。
