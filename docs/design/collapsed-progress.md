[English](collapsed-progress.en.md) | [中文](collapsed-progress.md)

# 坍缩态进度显示设计（logo + 进度环）

> 用户设计单 2026-10-15。参考对象 [jazzenchen/agent-hud-open](https://github.com/jazzenchen/agent-hud-open)（macOS 常驻 HUD，Swift/SwiftUI）。
> 本文只定形态与语义，不含实现代码。
> 2026-10-15 补充单「组件可选展示」并入本文 §10（同日用户令，不另起文档）。

## 0. 口径（用户定死，本文不改）

坍缩态（collapsed）下组件只剩两样东西：

1. **中间的 logo**
2. **环绕 logo 的一圈进度条**

不保留完整面板、多余控件、历史文本。**不是** log 文本滚动，**不是**日志面板缩略——是 logo 本体居中、进度环作 logo 的外圈。

环的显示条件：**有进度 → 显示环；无进度 → 不显示环**（坍缩态就只剩 logo 本体）。环是条件渲染，不是常驻装饰——没有可量化进度时不画空环、不转圈、不留占位弧。

---

## 1. 参考项目结论（读源码，不凭想象）

参考项目是 macOS 菜单栏/Dynamic-Island 形态的 AI 用量 HUD。坍缩态由 `Sources/AgentHUDDesktop/Notch/Simple/SimpleMeterView.swift` 实现，几何由 `Sources/AgentHUDDesktop/Notch/Simple/SimpleMeter.swift` 的 `SimpleMeterConfig` 计算，默认值在 `Sources/AgentHUDCore/Models/Settings/ScreenPlacement.swift`。

### 1.1 环用什么绘制

```swift
// SimpleMeterView.swift:42-49
ZStack {
    Circle().strokeBorder(.white.opacity(0.14), lineWidth: config.ringWidth)   // 轨道
    Circle().inset(by: config.ringWidth / 2)
        .trim(from: 0, to: min(1, max(0, (item.usedPct ?? 0) / 100)))          // 进度弧
        .stroke(tint, style: StrokeStyle(lineWidth: config.ringWidth, lineCap: .round))
        .rotationEffect(.degrees(-90))                                        // 起点 12 点
}
```

结论：

- **纯矢量圆弧**，不是位图、不是蒙版、不是圆锥渐变。`trim(from:to:)` 截取圆的周长比例 = 进度值。
- **轨道环**（同色 20% 透明，`white.opacity(0.14)`）常驻在进度弧下层，给出「总量」的视觉参照。
- **`inset(by: ringWidth/2)`** 是关键细节：描边中心线内缩半个描边宽，保证整条描边落在环的外径之内——否则弧会长出外径。
- **`rotationEffect(-90°)`** 把起点从 3 点挪到 12 点，顺时针增长。
- **`lineCap: .round`** 圆头收尾。
- **环色 = 状态级别色**（`StatusPalette`：ok `#3ddc84` / warning `#ffd23f` / critical `#ff453a` / idle `#9a9aa0`，`StatusPalette.swift:4-13`）。环同时承载两个通道：**长度 = 进度，颜色 = 级别**。

### 1.2 环与中心元素如何共存

同一个 `ZStack` 里，环与中心元素**共用一个正方形 frame**，中心元素按固定比例内缩：

```swift
// SimpleMeter.swift:106-108
var logo: CGFloat { (size * (showsRings ? 0.55 : 0.68)).rounded() }  // 中心元素边长
var mark: CGFloat { showsRings ? size : logo }                       // 含环的正方形边长
```

- 环外径 = `size`（16…40 pt，默认 22），中心元素 = `size × 0.55`（有环时）→ **中心元素恒为外径的 55%**。
- `ZStack` 的 frame 是 `mark × mark`，环与中心元素都居中于它——**环是外边界，中心元素按比例内缩**，两者永不重叠。
- 字号 `fontSize = max(9, size × 0.59)`、标注间距 `labelSpacing = size × 0.23`——所有长度都从 `size` 一个量派生（`SimpleMeter.swift:105-113` 注释：「Every length follows from `size`, so the one slider scales the whole panel」）。
- 百分比数字是**环外的独立列**，不压环（`labelSpacing` 隔开），且可整体关掉（`showsNumbers`）。

### 1.3 参考项目的进度语义

`SimpleMeterItem.usedPct` = **该 agent 最紧额度窗口的已用占比 0…100**，无读数时为 `nil`（`SimpleMeter.swift:5-9`）。`items(rows:)` 按 `markKey` 合并共享品牌图的窗口，取**消耗最多**的那条（`tightest[key]`，`SimpleMeter.swift:47-61`）。

即：参考的环是**资源消耗环**，不是任务完成度环。

### 1.4 参考项目对「无进度」的处理（与本仓口径相反，见 §2）

`trim(to: (usedPct ?? 0) / 100)` 在无读数时截取长度为 0——**轨道环照画**，即「一枚空环」。参考没有「隐藏环」这一态：只要 `showsRings` 开，每枚标记都带轨道环。

### 1.5 参考项目的两态切换与动效

- 展开/收起由纯状态机 `HoverMachine` 驱动：`hoverDelayMs = 400`（悬停 400 ms 开）、`collapseDelayMs = 200`（离开 200 ms 收）（`AgentHUDCore/Models/Settings/Settings.swift:87-88`）。
- 几何过渡 `IslandAnimation`：**0.4 s，`timingCurve(0.2, 0.8, 0.2, 1)`**（强 ease-out），内容自停靠边偏移 5 pt 入场（`IslandAnimation.swift:6-28`）。
- **环本身不动**：没有进度动画、没有旋转。动效只属于几何过渡与「工作态标记变机器人」。
- 无障碍：每个格 `.accessibilityLabel("\(item.vendor) \(item.label)")`（`SimpleMeterView.swift:68`），容器 `.accessibilityElement(children: .combine)`（`:36`）——**环的等价物是文本标签，环自身不另带 a11y 值**。

---

## 2. 本仓适配差异

| # | 维度 | 参考项目 | 本仓（Beacon） | 处理 |
|---|---|---|---|---|
| D1 | 环的常驻性 | 恒画轨道环，无读数 = 空弧 | **条件渲染**：`Progress == null` 不画环、不画轨道、不留占位（用户口径） | 按本仓口径，**偏离参考** |
| D2 | 进度语义 | 最紧额度窗口已用占比 | 同构：`WidgetState.Progress`（0…1，已用占比） | 同源，见 §5 |
| D3 | 中心元素 | 全彩品牌图（位图/多色） | `BrandIcons` 单 path 实心 `Path`，**单色可染**（`BrandIconFactory.TryCreate`，tile 上 16 DIP，`ThemeColors.Label()` + 同色描边增重） | logo 与环可各自取色 → 通道分配见 §4.4 |
| D4 | 文字 | 环外可选百分比列 | 坍缩态**无文字**（用户口径） | 等价物移到无障碍标签，见 §8 |
| D5 | 坍缩形态 | 悬浮胶囊/条，含环行 | 拖到屏边贴死 → 现为 **12 DIP 细条**（`PinnedHostWindow.CollapseStripDips = 12`） | **最大结构改动**：坍缩形态由细条改为徽章盘，见 §3 |
| D6 | 动效口径 | 固定 0.4 s 曲线 | `MotionEngine`：`off/reduced/full` 三档 + `Intensity` 0.5–2.0，进度过渡 300 ms × intensity，off 档 0（`MotionEngine.cs:35-53`） | 环的出现/消失/推进一律走本仓档位，见 §3.2 |
| D7 | 多标记 | 一排独立环 | 同构，但合并规则用本仓自己的多窗口口径 | 见 §6 |
| D8 | 进度样式配置 | 无 | 已有 `appearance.progressStyle`：`bar`（默认）/`pool`；2026-10-11 用户令「圆池绝不做默认路径」 | 环是**坍缩态的投影**，不是第三种 tile 样式，见 §5.3 |

---

## 3. 两态切换

### 3.1 触发（沿用本仓既有手势，不新增）

| 动作 | 现状（`src/Beacon.App/Windows/PinnedHostWindow.cs`） |
|---|---|
| 收起 | 拖动贴死屏边（`edge <= max(2, 2 × dpi)`，`:976`）→ `_layout.Collapsed = true` |
| 临时展开 | 光标悬停收起体（`_hoverExpanded`，`:1018`），移出即回缩 |
| 粘住展开 | 收起态点击（`_hoverPinned`，`:992`），直到拖动重判或开关关闭 |
| 总开关 | `app.edgeCollapseEnabled`（默认开，`AppConfig.cs:35`）；关闭时清 `pins.json` 残留收起态（`:1059`） |
| 省电 | 收起态暂停全部动画循环（`:452`） |

**本轮只改「收起体的形态」，不动触发语义。** 悬停展开、点击粘住、拖动重判三条手势保持原样。

### 3.2 尺寸过渡与动画

- **锚定不变**：收起体与展开面板共用锚边（现口径「条缘与面板缘重合」，`:799-802`）——徽章圆心落在展开面板 footprint 内，保证「悬停收起体任何位置都在展开面板内」这条不变量继续成立。
- **几何过渡**：面板 ⇄ 徽章，宽高沿锚边收敛/放大。时长 = **300 ms × Intensity**，`off` 档 = 0（瞬时直设）——与 tile 进度条过渡同口径（`MotionEngine.BarTransitionDuration`）。
- **环的出现/消失**：同一时长内做 `trim` 从当前值到目标值（出现：0 → 进度值；消失：进度值 → 0，随后从可视树移除）。`off` 档瞬时。**不用淡入淡出冒充**——弧长动画与面板尺寸动画同时长同曲线，读起来是一次形变而不是两个动作。
- **进度推进**（有环期间 `Progress` 变化）：只动弧长，不重画、不跳变。
- **环永不自转**：坍缩态的环是**静止**的读数。转圈动画不承载任何语义（用户口径），且参考项目同样没有环动画（§1.5）。
- **参考对照**：参考 0.4 s `timingCurve(0.2, 0.8, 0.2, 1)` + 悬停 400/200 ms。本仓时长跟 `Intensity` 档位走，曲线沿用本仓既有 ease-out 口径；两档数值不同但都是「强 ease-out、亚秒级」，观感同族。

### 3.3 实现影响（本轮不实现，登记）

`CollapseStripDips = 12`、hit-test 宽（`:1139` 用 `CollapseStripDips`）、`ApplyLayout` 的收起几何（`:799`）三处都要跟着改；多组件时收起体长度由「面板高」变为「n × 徽章 + 间距」。

---

## 4. 坍缩态布局与几何

### 4.1 尺寸表（默认值 + 可调档）

| 量 | 符号 | 默认 | 档 | 依据 |
|---|---|---|---|---|
| 环外径 | `D` | 32 DIP | 24–40 | 与 tile 高 32 DIP 同族（`PinnedHostWindow.TileHeightDips = 32`）、RFC §6.2.2 胶囊高 32 DIP——坍缩徽章与 L0 tile、L1 胶囊读作同一族 |
| 环描边 | `w` | 3 DIP | 2–4 | 参考档 1–4（`ringWidthRange`）；D=32 下 3 DIP 在亚克力/半透明底上可辨 |
| logo 绘制盒 | `L` | 18 DIP | ≥16 | `L = round(0.55 × D)`（参考比例，§1.2）；下限 16 DIP = 本仓 tile 图标既有尺寸（`BrandIconFactory` 调用点 `size: 16`） |
| 净间隙 | `g` | 4 DIP | ≥ w | `g = (D − L)/2 − w = (32−18)/2 − 3` |
| 徽章占位 | — | 32 × 32 DIP | — | 命中判定与布局 |
| 徽章间距 | — | 6 DIP | — | 同 tile 栅格 `ColumnSpacing = 6`（`PinnedHostWindow.cs:243`） |
| 环起点 / 方向 | — | 12 点 / 顺时针 | — | 参考 `rotationEffect(-90°)`（§1.1） |

### 4.2 logo 不压环（硬规则）

- **`g ≥ w`**：净间隙不小于描边宽——环的内缘与 logo 绘制盒之间至少空出一个描边宽。
- **按墨迹量，不按绘制盒量**：Simple Icons 的 24×24 视框自带内边距，实心 path 实际墨迹约 `0.8 L`（`BrandIconFactory.cs:19-20` 实证：细线几何缩到 14 DIP 后前景覆盖率仅 7–22%）。`L = 18` 时墨迹 ≈ 14.4 DIP，实际墨迹间隙 ≈ 5.8 DIP，比名义 `g` 更宽——**排布按绘制盒算，视觉按墨迹看**，两头都安全。
- 描边中心线须内缩 `w/2`（参考 `inset(by: ringWidth/2)`），保证描边整体不越出 `D`。

### 4.3 logo 辨识度

- `L ≥ 16 DIP` 是下限（本仓既有 tile 图标尺寸，也是「细线几何看得清」的实证下限）。
- logo 沿用 `BrandIconFactory` 的**同色描边增重**（`strokeWidth > 0`），坍缩态小尺寸下不加粗会发灰。
- 环不覆盖 logo：由 §4.2 的间隙规则保证，环是 logo 的**外圈**而非叠层。

### 4.4 取色通道分配（环与 logo 不抢同一个像素）

本仓 logo 是单色 path（D3），因此可以**把两个通道分给两个元素**：

| 状态 | 环 | logo |
|---|---|---|
| 有进度 | 环色 = **级别色**（§4.1 调色规则：Widget > 级别 > 默认，`WidgetConfig.ColorOverride` 生效）；弧长 = 进度 | **标签色**（`ThemeColors.Label()`），不抢级别色 |
| 无进度 | 不画 | logo **染级别色**——级别信息不丢，且不新增任何元素（仍是「只有 logo 本体」） |

这样：**级别色在坍缩态恒有且仅有一个载体**，进度与级别永远不会争同一块像素。参考项目的 logo 是全彩品牌图、环只能靠自身取色区分（§1.1）；本仓因 logo 可染而更自由，这是 D3 带来的好处。

---

## 5. 进度语义定义

### 5.1 定义

> **环表示「该组件最紧窗口的额度消耗占比（已用 %）」**，取值 = `WidgetState.Progress`（0…1），与 L0 tile 的进度条**同源同值**。

### 5.2 理由

1. **模型里唯一可量化且由 provider 提供的 0…1 值就是它**（`WidgetState.cs:13-14`：`/// 0..1 归一化进度（构建进度等），无进度为 null`）。已落地的取值方全是额度类：`bigmodel` / `ark` / `opencode` / `kimi` / `sensenova` / `mimo` / `httpQuota`，口径统一为「最差窗已用占比」（`BigModelUsageProvider.cs:96`、`HttpQuotaProvider.cs:74`、`MiMoUsageProvider.cs:520` 等）。
2. **「任务完成度」在本仓没有数据源**。GitHub Actions 明确 `Progress = null`（`GitHubActionsProvider.cs:52`：「GitHub API 无进度百分比 → UI 按 Lifecycle 显示不确定进度」），Jenkins 同（`JenkinsBuildProvider.cs:78`：「无进度口径：正在构建按 Lifecycle 显示不确定进度」）。**编一个完成度就是假信息**。
3. **与参考项目同口径**（§1.3：最紧窗口 used share），跨项目可对照、可复用它的判读经验。
4. **与 L0 tile 同源**：用户在 L0 悬浮件、L1 胶囊、坍缩徽章三处看到的是同一个数，不会出现两个口径。`WidgetState.Progress` 一改，三处同帧一致。

### 5.3 与既有 `progressStyle` 的关系

- `appearance.progressStyle`（`bar` 默认 / `pool`）管的是 **L0 tile 的进度呈现**；坍缩徽章的环是**同一个 `Progress` 在坍缩形态下的唯一可行投影**——坍缩态没有横条可放。
- 因此环**不是第三种 tile 样式**，不改 `bar`/`pool` 的既有默认（2026-10-11 用户令「圆池绝不做默认路径」）。坍缩态的环与 tile 的 bar 同时存在、同值，互不取代。

### 5.4 「有进度」的判定（写死）

| 情形 | 判定 | 表现 |
|---|---|---|
| 任务已知总量、有 0…100% | 有进度 | 画环，弧长 = 值 |
| 额度类读数（已用占比） | 有进度 | 画环 |
| **未知总量的长任务**（只有开始没有分母：CI 构建中、Jenkins 构建中） | **无进度** | **不画环**；需要反馈时用既有信号——级别色 + `Lifecycle` 驱动的脉冲（本仓既有不确定进度口径），**不占用环的语义** |
| 无任务 / 无读数 | 无进度 | 不画环，徽章只剩 logo（染级别色） |

### 5.5 已知歧义与缓解（如实记录）

环是**消耗**语义——**满环 = 额度用尽 = 危险**，而多数用户会把满环读成「完成」。参考项目有同样的歧义，靠颜色缓解（§1.1 环色 = 级别色）。本仓沿用同一缓解：

- 满环必然是 warning/critical 色，颜色与长度同向（越满越红），不出现「满环却是绿色」的组合；
- 无障碍标签里带百分比文本（§8），读数不靠几何猜；
- 若后续用户要求「显示剩余」，那是**取反的同一数值**，作为选项加，不改本定义的数值源。

---

## 6. 多组件 / 排队时的口径

**一枚徽章对一个组件（或一个合并组），各自显示各自的进度；不做跨组件汇总。**

- **排列**：沿停靠边依次排列，间距 6 DIP（同 tile 栅格），顺序 = `pins.json` 既有钉选顺序。多个组件时收起体长度 = `n × 32 + (n−1) × 6` DIP。
- **合并**：同一品牌/连接的多窗口合并为一枚（沿用参考的 `markKey` + `tightest` 口径，§1.3），与本仓既有 `WidgetValueHint.MultiWindow(state.Payload)` 多窗口口径对齐——一枚徽章报**消耗最多**的那个窗口。
- **部分有进度**：有 `Progress` 的画环，没有的只有 logo。一眼即可看出「谁有进度」——这正是用户口径的意图。
- **为什么不做汇总**：不同组件的进度单位不可比（一个额度 %、一个构建 %），求平均或取最值是**没有依据的算术**，会把读数变成装饰。这与用户「不要做成看着在动但说不清是什么」的要求一致。
- **排队**：队列本身不是进度——排队中的组件按「无进度」处理（不画环）。若要表达排队位次，那是另一条信息通道，不占环。

---

## 7. 空闲态

| 情形 | 环 | logo |
|---|---|---|
| 无任何任务 / 无读数 | **不画**（不画空环、不画轨道、不留占位弧、不转圈） | 只剩 logo 本体，染级别色（§4.4） |
| 组件已离线 | 不画 | logo 灰（Offline 口径：`Offline 灰空心`，RFC §4.1）+ 沿用既有离线横幅口径 |

与参考项目的差异（D1）：参考恒画轨道环，空闲时是一枚空环；本仓按用户口径**不画**——空环在坍缩态会被读成「有任务但 0%」，是误导。

收起态本身是用户手势的结果（拖到屏边），因此徽章在空闲时仍然存在（用户把它停在那儿了），只是**不含环**。

---

## 8. 无障碍

坍缩态**没有文字**，环不能是唯一信息通道。等价物走 UIA：

1. **徽章必须有一个 UIA peer**。仅设 `AutomationId` 不进 UIA 树（本仓 CI 实证：`Border` 只设 AutomationId 不进树，须补 `SetName` 造 peer）——必须设 `AutomationProperties.Name`（或等价造 peer 手段）。
2. **Name = 文本等价物**：
   - 有进度：`"{组件名} 已用 {n}%（{级别}）"`（如 `"GLM 已用 62%（警告）"`）；
   - 无进度：`"{组件名}"`（不带百分比、不带「0%」）。
3. **`HelpText`** 放细节：窗口名 + 重置时间 / 深链（与 tile 的 tooltip 口径同源）。
4. **有进度时才暴露 RangeValue 模式**（Value = n、Min = 0、Max = 100）；**无进度时必须不暴露**——否则屏幕阅读器会读到上一轮的残留值，与用户「无环时不得留下 `aria-valuenow` 之类残留属性」的要求一致。
5. **键盘可达**：徽章需可聚焦，Enter/Space = 展开。**现状限制**：收起体所在宿主窗口是 `WS_EX_NOACTIVATE`（RFC §6.2.3，点击不抢焦点），键盘焦点有限——因此 **L2 面板必须提供等价的键盘入口**（面板内的组件列表），不能让键盘用户只能靠鼠标悬停。
6. **色盲**：级别不能只靠颜色——Name 里带级别文字与百分比（第 2 条）；环的粗细不承载任何信息（只承载信息的是弧长与颜色，弧长另有文本等价物）。

---

## 9. 待拍板点

| # | 问题 | 建议 |
|---|---|---|
| P1 | `D` 默认 32 DIP 是否合适（vs. 参考的 22） | 建议 32：与 tile 高/胶囊高同族；若嫌大，档位下探到 24 |
| P2 | 多组件收起体是否允许换行/堆叠（组件多时沿边会很长） | 建议先单行/单列，超出后按屏边可用长度截断（沿用 tile 溢出回收口径），待装机看观感 |
| P3 | 无进度时 logo 染级别色（§4.4）是否与「logo 保持品牌辨识」冲突 | 建议保留：logo 是单色 path，染级别色不损形；若用户要求 logo 恒为标签色，则无进度时补 4 DIP 级别点（角标） |
| P4 | 环的取色是否允许用户覆盖 | 建议沿用 `WidgetConfig.ColorOverride`（Widget > 级别 > 默认），不新增开关 |

---

## 10. 组件可选展示（2026-10-15 补充单，并入本文）

> 用户令：「beacon 能选择展示的组件最好」。本节给三层划分、组件清单、默认配置、坍缩态联动与持久化。

### 10.1 参考项目怎么组织可选组件（先给结论）

通读 `agent-hud-open` 源码（`Sources/AgentHUDCore` + `AgentHUDDesktop`），它的可选组件**不是一种机制而是三层**，每层恰好一个机制：

| 层 | 内容 | 唯一机制 |
|---|---|---|
| **内置常驻** | 岛窗本体、悬停面板底栏、告警系统、统计窗、设置窗、引导页、bots 机体 | **无开关**——只有数据或模式改变其内容，没有任何配置能移除它们 |
| **偏好开关** | 悬停面板三区（`showIslandQuota/Tokens/Sessions`）、重置倒计时（`showResetCountdown`）、菜单栏图标（`showMenuBarIcon`）、bot 形象（`botLogos`）；坍缩 meter 的环/数字/bot（`showsRings/showsNumbers/simpleBots`，按屏的 `ScreenPlacement`） | 平面布尔集中在 `Settings`（`Settings.swift:42`，「every field has a default so older stored JSON still decodes」），形态开关在按屏 `ScreenPlacement`；视图侧统一 `if 开关 && !数据空`（`HoverPanelView.swift:101/111/119` 实证三例） |
| **数据驱动行** | 每个 agent/额度窗口一行 | `AgentDescriptor.enabled` 逐行开关（`agents.v5` 有序数组持久化），provider 发现合并（`SettingsStore.mergeDiscovered`）；新厂商默认显隐由 `VendorCatalog.startsHidden` 定——**全部默认显示，唯一例外 DeepSeek**（API 余额行默认不进 HUD，`:48`）；账号级隐藏是混合机制（`hiddenAccountIDs` 强制其全部行 off，`SettingsStore.swift:220-222`） |

持久化只有一处：**UserDefaults 三个 JSON 键**（`settings.v1` / `agents.v5` / `onboarding.completed.v1`，`SettingsStore.swift:21-25`），每次变更立即重编码落盘；数据（SQLite 台账、缓存）在 Application Support，与配置分家。引导页**不问任何组件选择**（「no choices」，`OnboardingView.swift:5`），全部选择都在设置窗。唯一的「仅内存」开关是运行时 glow 隐藏（`UsageStore.glowHidden`），故意不持久化。

对 Beacon 的三条可迁移结论：

1. **开关跟着「内容」走，不跟着「窗」走**——面板分区、meter 部件、逐行 agent 各用各的开关层，没有一个大总闸。
2. **「偏好 ∧ 有数据」双门**是它面板不空转的全部秘密：关掉 = 少一区，数据空 = 同样少一区，两种情况渲染结果一致，不存在「开关开了但区域空白占位」。
3. **默认值=现状**：偏好开关默认全 true（DeepSeek 行是数据层例外）；「关」是给被某个部件困扰的人的出口，不是初始状态。

### 10.2 本仓组件三层划分与清单

| 层 | 组件 | 说明 |
|---|---|---|
| **常驻（无开关）** | L1 状态胶囊、托盘图标+托盘菜单、L2 面板骨架与开合入口、设置窗、坍缩宿主（手势目标） | 拆掉任何一个 app 就失去操作入口，不做可选（参考同层同理由） |
| **偏好开关（本单新增）** | tile 子部件：进度条、百分比读数、状态灯、多窗明细；坍缩徽章的进度环（`components.collapsedRing`）；L2 三分区：RECENT EVENTS / 需要关注 / 正在运行；Toast 通知 | 机制：`config.json` 新增 `components` 段（与 `appearance` 同级）；既有开关先例：`app.edgeCollapseEnabled`（默认开，`AppConfig.cs:35`）、`appearance.progressStyle`（bar 默认/pool 可选，2026-10-11 用户令「圆池绝不做默认路径」）、`uiOpacity` 四面同值（批3-补10）、动效三档 |
| **数据驱动行（已有，不动）** | widget 实例本身（`widgets.json`）→ 悬浮 tile（pin 双入口）、面板行、坍缩徽章的生成；通知规则（`notifications.json`） | 与参考 `agents.v5`+`enabled` 同构：行由用户创建，行级可见性已有（pin/unpin），无需新机制 |

L0 tile 的 `bar|pool` 是**样式二选一**不是有无，故进度条开关语义 = 「不画条」（off 后读数/灯仍在）；多窗明细本就条件渲染（≥2 窗才出，`WidgetValueHint.MultiWindow`），开关只是再加一道偏好门。

### 10.3 默认配置（推荐：全部 = 现状行为，全开）

| 开关 | 默认 | 理由 |
|---|---|---|
| tile 进度条 / 读数 / 状态灯 / 多窗明细 | 开 | 与已发布行为零漂移，升级用户看不到突变 |
| `components.collapsedRing` | 开 | 环本就是条件渲染（无进度不画，§0/§7），默认开不产生任何常驻元素 |
| L2 三分区 | 开 | 三区全条件渲染（空区不渲是既有口径），关=少一区、空=同样少一区 |
| Toast 通知 | 开（按既有通知规则） | 已有 rulesProvider 与规则引擎，不另设总闸 |

**为什么默认全开不是噪音**：噪音的来源是「无语义的常驻元素」，而上面每个部件都是条件渲染——无进度不画条/环、无数据不渲区、无行不出徽章；真正默认关的是动效 `full` 档（现状 `reduced`）与 `pool` 样式（现状 `bar`），这两处在既有令里已定，本单不重复设。参考项目同类偏好开关（`showsRings/showsNumbers/botLogos`）也全默认 true，唯一默认隐藏的 DeepSeek 是数据驱动行的 `startsHidden`，不是偏好开关。若装机走查证明多窗明细在窄 tile 上噪音大，再降 `tileMultiWindow` 默认（见 10.6 P8），不预设。

### 10.4 坍缩态联动规则（用户令 #3 的正面回答）

坍缩徽章不是 tile 的缩略图，是独立形态——**坍缩态只吃两个输入：widget 存在性（数据层）与 `collapsedRing`（偏好层）**。L0 子部件开关（灯/读数/多窗）不外溢到坍缩态，坍缩态本来就没有那些位置：

| 组件可见性状态 | 坍缩体表现 |
|---|---|
| widget 正常钉选 | 徽章 = logo + 环（`Progress != null` 且 `collapsedRing` 开时画环） |
| widget 取消钉选 / 删除（数据层关） | **无徽章**，不出空位（徽章随 widget 生成，无 widget 无徽章） |
| `components.collapsedRing` 关（偏好层关进度通道） | 每枚徽章**只剩 logo**，染级别色——即 §4.4「无进度」行的形态原样复用（用户举例：关掉进度组件，坍缩态就只剩 logo，正是此行） |
| 钉选全空 | **收起禁用**：拖贴边不收起、已收起的立即弹回（与 `app.edgeCollapseEnabled=false` 同语义）——空收起体是一个看不见的命中区，交互黑洞；面板保持展开 |
| L2 分区开关关 | 面板少一区不占位（空数据同款渲染），坍缩态不受影响（面板分区与坍缩体无耦合） |

级别色通道自查：`collapsedRing` 关后，徽章上级别色的唯一载体 = logo 染色（§4.4 右列），通道不丢；tile 状态灯开关只影响 tile 自己，级别色在胶囊/托盘/坍缩徽章各有独立载体，任一单点关闭都不产生「级别色全灭」的窗口期。

### 10.5 持久化与恢复（用户令 #5）

- **唯一真源 = `config.json` 新 `components` 段**（与 `appearance`/动效同级），原子写沿用既有 settings 落盘链（SaveGeneral/SaveApp → SettingsApplied → 落盘）；设置页开关只是编辑器，与参考「UserDefaults 唯一真源、设置窗只是编辑器」同构。
- **缺字段回退默认**：老 config 无 `components` 段 → 逐键取默认（参考 `Settings.swift:42` 同一原则：每个字段带默认值，旧 JSON 永远可解码），升级零迁移。
- **重启不丢** = config.json 落盘即满足，无额外机制。
- **导入导出自动携带**：config.json 已是导出包成员（B-802），组件选择随包迁移、导入后恢复，不加新逻辑。
- 设置页落「外观」页（与透明度/动效/进度样式同族），文案进 `UiStrings` 双语字典（批3-补53 机制）。
- 不做「仅内存」的临时隐藏类开关（参考 glowHidden 是唯一例外且故意不持久化；本仓无对应需求，不引入）。

### 10.6 新增待拍板点

| # | 问题 | 建议 |
|---|---|---|
| P5 | 钉选全空时收起是否禁用（10.4 第 4 行） | 建议禁用收起（面板保持展开）——空收起体不可见不可命中，违反 §3.2「悬停收起体任何位置都在展开面板内」不变量的精神 |
| P6 | L2 分区开关粒度 | 建议三区各一（RECENT / 需要关注 / 正在运行），不做整组总闸——参考同层就是逐区布尔 |
| P7 | tile「进度条+读数」是否合并为一键 | 建议分开两键：bar off + 读数 on（纯数字形态）与 bar on + 读数 off（纯条形态）都是合理终态，合并会堵死其中之一 |
| P8 | `tileMultiWindow` 默认值 | 建议先开（条件渲染本就 ≥2 窗才出）；装机走查若窄 tile 噪音大再降默认，不改机制 |
