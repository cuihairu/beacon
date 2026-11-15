# 岛形态对标 Dynamic Island · 并排对比与差异清单（2026-11-15 巡检单 beacon-island-parity）

> 任务书：`~/.hermes/cache/scratch/tasks/beacon-island-parity.md`（2026-11-15 用户令「界面叫你抄都抄不明白」）。
> 对抄=对着看效果做参考：代码自写、不拷 Apple 资产；参照截图仅入档对比。
> **验收状态：待用户装机亲验，agent 不闭项。**

## 参照素材（docs/review/island-ref/，Apple 官方 newsroom 新闻稿图集，Wayback 快照 20220907184041）

| 文件 | 来源 | 展示的 DI 状态 |
|---|---|---|
| `di-01-multitask-3up.jpg` | newsroom/videos/iphone-14-pro-3up poster | 紧凑态（三机并列，中机特写） |
| `di-02-expanded-faceid.jpg` | newsroom/videos/iphone-14-pro-dynamic-island-face-id poster | 展开态（白底特写，Face ID 动画帧） |
| `di-03-compact-lockscreen.jpg` | newsroom/images iOS-16-Lock-Screen inline | 紧凑态（锁屏，深壁纸） |
| `di-03b-compact-livertext.jpg` | newsroom images（相机取景态帧） | 紧凑态变体（绿色相机指示点） |
| `di-04-minimal-always-on.jpg` | newsroom/videos always-on-display poster | 最小态（AOD 锁屏，亮度需增强才可见） |

## beacon 侧截图（既有真机证据 + nightly visual-evidence）

- 真机（imac 200% DPI，2026-11-08 a413ec0，`docs/acceptance-evidence/island-head-closeout/`）：
  `01-island-collapsed.png` / `02-island-collapsed-10x.png`（收起）、`06-island-expanded.png`（悬停展开）、
  `08-island-critical.png`（critical 徽标）、`09-capsule-classic.png`（经典胶囊回归对照）。
- nightly visual-evidence（本轮新增场景 AG/AH）：run 43662875796（本文写作时在跑），
  截图落 `AG-island-*.png` / `AH-island-*.png`，run 绿后路径抄录进下表。

## 并排对比图（docs/review/island-compare/，同尺度同底色重排）

| 对比图 | 内容 | 量化读数 |
|---|---|---|
| `cmp-01-collapsed.png` | DI 紧凑（di-03 crop）vs beacon 收起（01 crop），高归一 120px | DI w/h=3.98 全圆角（r=h/2）；beacon w/h=7.44 顶角 0、底角 r=12/h=0.375 |
| `cmp-02-expanded.png` | DI 展开（di-02 crop）vs beacon 展开（06 crop） | DI 展开仍全圆角；beacon 展开同刘海轮廓（顶角 0） |
| `cmp-03-ref-minimal.png` | DI 最小态（di-04 autocontrast 增强） | AOD 下岛融入壁纸（亮度差 <4/255） |
| `cmp-04-ref-multi.png` | DI 多活动（di-01 中机特写） | 双活动并列：左活动区+右实时图标区 |

## 差异清单（每项给截图证据；修复=重截图重对比；归零或「设计取舍」才闭）

### D-1 顶缘形态：贴顶刘海 vs 悬浮胶囊 ⚠️ 设计取舍（结构性，不修）

- 证据：cmp-01 左（DI 四周留缝悬浮药丸，di-03 锁屏上缘可见壁纸缝隙）vs 右（beacon 顶缘贴屏幕顶，上两角 0）。
- beacon 口径：`CapsuleIslandMath`（src/Beacon.Core/Services/CapsuleIslandMath.cs:5-27）明确「岛=刘海式贴顶形态……
  顶缘恒贴屏幕顶（y=屏幕顶，永不下移）」，对拍对象是 BrimDeck Notch 而非 Apple DI。
- 裁决：**保持贴顶刘海**。beacon 是 Windows 桌面常驻工具，顶部留缝会漏出桌面内容并引入持续的
  「悬浮物遮挡感」；贴顶是 11-08 收口（a413ec0）后的既定形态且已过用户装机一轮验证。差异记录在案，
  不按 DI 改。

### D-2 圆角率：DI 全圆角（r=h/2）vs beacon 底角 12 DIP ⚠️ 设计取舍（随 D-1）

- 证据：cmp-01 读数行；`IslandSurfaceOutline.Build`（src/Beacon.Core/Services/IslandSurfaceOutline.cs:45-94）
  上两角恒 0、底角 clamp 到 min(h-肩深, h/2)。
- 裁决：随 D-1。贴顶形态的上角不可能有圆角；底角 12 DIP（h=32 的 37.5%）读数上已是「半圆收尾」，
  DI 的 r=h/2=50% 在贴顶约束下不可达（16 DIP 半径需 h≥32 且上角为 0 的镜像对称）。**不修**。

### D-3 收起态长宽比：DI 3.98 vs beacon 7.44 🔧 待修（ nightly 截图绿后动）

- 证据：cmp-01 读数。DI 紧凑胶囊 477×120px（di-03 crop 实测）≈ 3.98:1；beacon 收起面 2856×384px
  （01 实测，482×70 物理窗 @200%，曲面 238 DIP 宽）= 7.44:1。
- 根因：`CapsuleIslandMath.DefaultWidthDips=238`（CapsuleIslandMath.cs:34）在 4 面 stack 场景下
  拉满（1 面 placeholder + 3 真实面的证据图来自 ring+bar+GLM+DeepSeek 四面场景）。
- 动作：收起面宽按面数自适应已在（ApplySize 实测 + 238 下限）；差异主要来自 beacon 一行塞 4 面
  而 DI 紧凑态最多 2 活动区。**修复方向=收起行 max 2 组件面**（与 DI「紧凑态=左活动+右图标」
  同构），超出部分只进展开面。改动点：`IslandDisplayPlan.Compute`（Core 纯函数）collapsed 段
  Take(2)，展开段不变。测试锁口径在 Core.Tests。

### D-4 内容内边距：DI 左右≈0.5×h vs beacon 1.66×h 🔧 待修

- 证据：beacon 01 实测 glyph 左 inset 636px = 1.66×surface-h；DI di-02 实测左 inset 3px（相机帧特写）
  / di-03 胶囊左右留白≈胶囊半高。
- 根因：`CapsuleIslandMath.InsetDips=30`（两端留白）+ `PillRow.HorizontalAlignment=Center`
  （IslandChrome.cs:91）在 238 默认宽下面少时两侧各留 ~90 DIP 空白，读作「字缩在中间」。
- 动作：收起行内容居中改为**跟面数收缩曲面宽**（曲面宽=内容宽+2×inset，inset 降到 0.75×h=24 DIP
  对齐 DI 视觉），下限仍保 MinWidthDips=72。改动点：`ApplySize`（CapsuleWindow.Positioning.cs）
  的收起宽计算 + `InsetDips` 语义拆分（内容留白 vs 形态最小 inset）。

### D-5 字体层级：DI 图标≈文字同高、无多级字号 vs beacon 11px 单级 ✅ 已对齐（复核通过）

- 证据：DI compact 双活动区字形高≈胶囊高 60%（di-01 特写读数）；beacon 面 chip 字 11px+
  灯点 8px（CapsuleWindow.IslandFaces.cs:88-111），在 32 DIP 面高占比≈46%。
- 裁决：beacon 的信息密度（4 组件标签）高于 DI 双活动，11px 是 32 DIP 面内可读下限；
  层级（灯点色→标签字）已由色相承担。**不修**，装机验字重再议。

### D-6 活动图标布局：DI 左活动+右圆图标 vs beacon 顺序平铺 🔧 待修（与 D-3 同一改动）

- 证据：cmp-04（DI 多活动=左文本活动区+右侧实时活动圆图标独立成区）；cmp-01 右（beacon 4 面
  等间距平铺，无主次分区）。
- 动作：收起行分区=主面（环/条虚拟面，靠左）+ 次面区（chip，靠右），两区间以固定 12 DIP 间隔
  分组而非等距平铺。改动点：`BuildIslandContent`（CapsuleWindow.IslandFaces.cs:36-70）行内布局。

### D-7 展开动画时长/曲线：360ms Bounce vs DI ≈0.5s spring ✅ 已对齐（复核通过）

- 证据：`IslandMotion.OpenMs=360`、Bounce 阻尼 0.91/衰减 6.2（IslandMotion.cs:14-48，BrimDeck
  Curves 同式）；DI 官方视频逐帧约 0.45-0.55s（newsroom demo poster 帧），感知差异 <100ms。
- 裁决：BrimDeck 口径已实测自然（11-08 装机未点名动画）；360→500ms 的绝对对齐收益低于引入
  回归的风险。**不修**。

### D-8 模糊边缘：DI 深底融合（AOD 亮度差<4/255）vs beacon 近实色黑+发丝描边 ⚠️ 设计取舍

- 证据：cmp-03（DI minimal 在 AOD 壁纸上几乎隐身，autocontrast 才可见）；beacon 01 发丝描边
  18/255 白（ThemeColors.cs:51 IslandHairline）+ 纯黑面（IslandPill #000）。
- 裁决：DI 的「隐身」依赖 iOS 壁纸联动与 AOD；Windows 桌面壁纸不可控（亮壁纸下纯黑面对比
  恰是 11-06「看不清」教训的修正），发丝描边是形态可读性的下限保障。**不修**。

## 验收口径

- 修 D-3/D-4/D-6 后重跑 nightly visual-evidence（AG/AH 场景）重出对比图，差异清单逐项更新读数；
- 视觉类条目一律「待用户装机亲验」：装机确认①收起面宽随面数收缩、②内容不再「缩中间」、
  ③主/次面分区可辨，D-3/D-4/D-6 才由用户闭项；
- 本轮 agent 不勾任何岛类 todo 条目。
