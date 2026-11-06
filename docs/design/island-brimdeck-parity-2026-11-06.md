# 悬浮岛 × BrimDeck 对拍差异表（USER-RPT-4，2026-11-06）

> 参照：`~/workspaces/brimdeck-ref` @ `5cbf5596`（C#/WPF Dynamic Island 实现，Apache 2.0）。
> 红线执行：**未拷任何 BrimDeck 图标/图片素材，未复用其源码文件**；几何与动效参数按其源码逐点同式重写进我方 Core 纯函数（`CapsuleIslandMath` / `IslandSurfaceOutline` / `IslandMotion`），由 `Beacon.Core.Tests` 锁值。
> 口径：上席的肩部曲线方案与 BrimDeck 不一致处（内缩 16、顶中下移 8 DIP、工作区锚定）**一律以 BrimDeck 为准**改齐，见 §5。

## 1. 收起三形态几何（BrimDeck `CollapsedLimits` / `CollapsedGeometry.Resolve` / `IslandBorder.BuildOutline`）

| 形态 | BrimDeck 口径 | 我们现状（本轮修正后） | 判定 |
|---|---|---|---|
| **Notch（刘海）** | 默认 238×32（宽 min 72 / max 8000，高 24–64）；`surface.Y = 0` 贴**屏幕**顶（越过顶部任务栏），水平跨度 = 屏宽；肩 16、肩深 = 16×7/16 = 7、底角 12、**上角 0**；内容内缩 = `max(NotchInset 30, 肩+角 28) = 30`；底角/肩随 `PanelScale` 缩放 | 默认宽 238（`DefaultWidthDips`，min 72、max 屏宽-16），高 32；`y = 屏幕矩形顶`（外框另按实测「客户区顶-外框顶」补偿），只 X 偏移可持久化；肩/肩深/底角/上角由 `IslandSurfaceOutline.Build` 逐控制点同式生成；内缩 = `InsetDips 30` | ✅ 已对齐（§5 修正①②③） |
| **Capsule（胶囊）** | 默认 196×32，`top = 工作区顶 + 5`（可下探到屏底、可盖底部任务栏），全圆角（r = h/2），内缩 16，**无肩** | 经典胶囊 = 我方历史形态（工作区右下角锚 + `RadiusPill` 药丸），**逐字节不动**（这是另一形态开关 `capsuleIsland=false` 的口径） | ⚠ 差异：我们没有「下移 5 DIP 的悬浮胶囊」第二悬浮形态——产品只开一个形态开关，不做双形态并存 |
| **Indicator（指示条）** | 56×4（厚 2–8，0.5 步进），`top = 工作区顶 + 4`，全圆角、无内容；感知区外扩 x±8、下 2 | **无** | ⚠ 差异（真差距，见 §3/§6）：BrimDeck 在「最大化窗」时把岛降级成这条线，我们没有线形态 |
| 面板缩放 | `PanelScale` 0.75–2（保存的高/宽/角 × scale，偏移不缩） | 无该设置（固定 1×） | ⚠ 差异：不做缩放档位（我方动效档由既有 `appearance.motion` 承担） |

## 2. 悬停展开触发区（BrimDeck `CollapsedHover`）

| 项 | BrimDeck 口径 | 我们现状 | 判定 |
|---|---|---|---|
| 感知区 | 曲面 + 顶上「指针够不到也点得到」的桥带：Notch/Line 桥到屏顶（y=0），Capsule 只桥到工作区顶且 `gap ≤ 20 DIP`，超了只自身可开 | 岛=贴顶 Notch → 桥带高度为 0，感知区 = 窗体自身（`PointerEntered` 展开 / `PointerExited` 收起） | ✅ 等价 |
| 打开/关闭延迟 | `OpenDelay` / `CloseDelay` 默认 **0**（0–2000 可设） | 无延迟，入窗即展 | ✅ 同默认口径 |
| Line 附加感知 | x±8、下 2 | 无 Line | —（随 §1 Line 缺席） |
| 额外门控 | 无（其窗不参与拖动/尺寸互抢） | 按住进入、拖动中、无数据不展开 | ⚠ 我方多出门（必要：WinUI 窗口尺寸=外框，展开 Resize 会与拖拽 Move 互抢），非 BrimDeck 口径缺失 |

## 3. 全屏避让（BrimDeck `ScreenContext` / `WindowBehavior` / `FullscreenRules`）

| 情形 | BrimDeck 默认 | 我们现状 | 判定 |
|---|---|---|---|
| 桌面/普通窗 | `Normal`（显示） | 显示 | ✅ |
| **无边框全屏（Borderless/Exclusive，前台）** | `Hide`（可用逐应用规则覆盖，≤32 条） | `EVENT_SYSTEM_FOREGROUND` 钩子判定「前台窗无 `WS_CAPTION` 且外框盖满岛所在屏（2px 容差）」→ 岛退场，退出全屏回场；与 `TopmostGuard` 共用 `IsGuardVisible` 门，杜绝哨兵把岛 Show 回来 | ✅ 同默认行为（不细分 Borderless/Exclusive，结果一致） |
| **最大化窗（前台）** | `Maximized = Line`（降级成指示条继续在场） | **不处理，岛保持在场** | ⚠ 真差距：我方无 Line 形态，不臆造降级行为（§6 记待决） |
| 非前台的全屏窗（BackgroundFullscreen） | 按 `Desktop` 处理 → 显示 | 只看前台 → 显示 | ✅ |
| 逐应用规则 | `FullscreenRules` 列表 | 无 | ⚠ 差异：暂不引入规则表（先要形态正确） |
| 触发同源 | 单一状态源 | 避让与置顶哨兵同钩子同事件、同读 `IsGuardVisible`，两种触发顺序终态一致 | ✅ |

## 4. 开合动效（BrimDeck `PanelMotion` / `Curves` / `Soft` / `MainWindow` Track）

| 参数 | BrimDeck 值 | 我们（`IslandMotion` + `MotionEngine`） | 判定 |
|---|---|---|---|
| 展开时长 | `Settings.AnimationDuration` 默认 360ms（100–1000，步进 20） | `OpenMs = 360` × 我方 `motion.intensity` | ✅ 同默认（intensity 是我方既有旋钮，BrimDeck 无） |
| 收起时长 | `CloseFactor = .75` → 270ms | `CloseMs = 270` | ✅ |
| 展开曲面曲线 | `Curves.Bounce`：`1 - e^(-6.2x)·cos(ωx)`，damping `.91`，过冲 `.32%` | 同式；Composition 无 Bounce 原语 → **17 个关键帧采样线性内插**（full 档） | ✅ 参数同式（表达方式不同，已说明） |
| 展开降级曲线 | `Soft.Out = (.22, .8, .28, 1)`（reduced/无弹档） | `IslandMotion.Out` 同 bezier，Composition `CubicBezierEasingFunction` 直接吃 | ✅ |
| 收起曲线 | `Soft.Drawer = (.32, .72, 0, 1)` | `IslandMotion.Drawer` 同值（内容淡出段） | ✅ |
| 内容轨 | `content = expanded ? (.12, .80) : (0, .30)`（`MainWindow.xaml.cs:749`） | `ContentInStart .12 / ContentInEnd .80 / ContentOutEnd .30` | ✅ |
| 收面启动延迟 | `CloseSurfaceDelay = .12` | 偏离：**内容走完（30% ≈ 81ms）才收面** | ⚠ 有意偏离：我方窗高=外框尺寸，边淡出边 Resize 会裁切曲面；BrimDeck 动的是曲面自身故可与内容重叠 |
| 展开起始缩放 | `Soft.Shrunk = .94` | 0.94 → 1，锚顶缘中点 | ✅ |
| 中途折返 | `MoveTurn` + `RedirectMin .4`（开一半折返按剩余行程走） | 未实现 | ⚠ 差异：我方开合是整段，反复悬停时由代际计数取消，不按剩余行程续算 |
| 材质轨 | `rim (.2,.5)` / `tint (0,.12)`（描边/底色随开合淡） | 未实现（描边恒定） | ⚠ 差异：观感影响小 |
| hover 微动效 | `Soft.Quick 160 / Short 240`（悬停/按压反馈） | 未实现 | ⚠ 差异 |

## 5. 本轮按 BrimDeck 口径已修正的项（上席在制品 → 完成）

| # | 上席在制品的问题 | 修正（以 BrimDeck 为准） | 落点 |
|---|---|---|---|
| ① | 内缩用 `CapsuleInset = 16`（Capsule 形态的值） | 改 `InsetDips = 30`（Notch 口径，`max(30, 16+12)`），并锁 `MinWidthDips 72` / `DefaultWidthDips 238` | `CapsuleIslandMath.cs` + 测试 |
| ② | `Place` 传**工作区**矩形（`WorkPx`）→ 顶部任务栏时 X 会算偏、Y 也不是屏顶 | 改传**屏幕矩形**（`MonitorPx`），`y` 恒 = 屏幕顶；另按实测「客户区顶-外框顶」补偿，保证肉眼贴顶 | `RestoreIslandPosition` / `SaveIslandPosition` |
| ③ | XAML 里 `ShoulderL/R` 是**占位空 Path**，代码侧从未喂 → 肩部根本没画 | 删占位，改为 `IslandSurface` 单路径曲面（`IslandSurfaceOutline` 由 `IslandBorder.BuildOutline` 逐控制点同式重写：肩双三次 C2 相切、肩深 ×7/16、底角 k=0.5522847498307936） | `CapsuleWindow.xaml` / `.cs` / `IslandSurfaceOutline.cs` |
| ④ | 曲面靠 `Border.CornerRadius` 药丸画法 + `Padding(14,6)` → 顶两角圆、贴顶读不出刘海 | 岛形态 `CornerRadius 0 / BorderThickness 0 / Padding 0`，内缩改由 `SurfaceStack.Margin 30` 承担（Path 必须铺满曲面），收起行 `MinHeight 32` 内容垂直居中 | `ApplyIslandChrome` |
| ⑤ | 拖动可改 Y → 岛能被拖离屏顶；旧持久化 y 仍生效 | 拖动锁 Y（岛只水平拖），落点即 `RestorePosition` 贴回屏顶；`Locate` 只记 X 分量 | `OnPointerMoved` / `OnPointerReleased` |
| ⑥ | 展开动效是 200ms 自选曲线，无收起动效 | 按 §4 换 `IslandMotion`：展开 360ms Bounce、收起 270ms 内容先撤再收面、内容轨 .12–.80 / 0–.30 | `MotionEngine.MorphIn/MorphOut` |
| ⑦ | 预览行横向 StackPanel 量到无限宽 → 展开文本会越出曲面缘 | 行改 Grid 三列（点/来源 Auto + 摘要 Star），预览 `MaxWidth = 曲面宽 - 60`，只在岛内裁尾；经典仍 92 上限观感不变 | `RebuildPreview` / `MeasureSizePx` |
| ⑧ | 无全屏避让 | §3 实现 | `CapsuleWindow` + `App.xaml.cs`（哨兵门改 `IsGuardVisible`） |

## 6. 待决 / 待用户亲验

- **最大化窗降级**：BrimDeck 用 `Line` 指示条，我们没有线形态——是否给岛加「最大化时缩成小圆点/细条」需产品定夺（本轮不动，真机证据里记录为已知差距）。
- 收面延迟取「内容走完再收」（§4），与 BrimDeck 的 0.12 拍重叠不同——观感是否可接受待用户亲验。
- 逐应用全屏规则、材质轨淡入、hover 微动效：未实现。
- 真机三态并排证据见 `docs/acceptance-evidence/2026-11-06-island-brimdeck-parity.md`（待用户亲验）。
