# BEACON-ISLAND-AA-1 证据（岛边缘 AA 全轮廓 + 收起态三数据病灶）

派单：编排方巡检单 BEACON-ISLAND-AA-1（岛窗口面问题第七单）。本文件收 agent 侧改动清单与
本地测试证据；像素扫描前后对比、收起态三病灶前后截图、装机亲验为后续硬门，见文末「待验门」。

## 病灶 A：边带环四边 AA 不对称

**诊断修正**：编排方任务书写「IslandEdgeBandWindow 目前只沿轮廓画顶部弧段附近环带」与当前
main 不符——`BuildPath` 早已遍历全部 segs（4 直边+4 角弧闭合）整周描环。真根因在绘制方式：

- 旧实现 = 单笔 4px **不透明**黑描轮廓中心线，边缘软化完全依赖 GDI+ 几何 AA；
- 轴对齐直边（左右侧缘、底缘）的笔缘恰好落整像素界 → 覆盖率 0/1 跳变，**几何 AA≈0**；
- 只有角弧/肩部斜线有真实 AA → 实测「顶缘 4px 软、侧缘 1px、底缘 ≈0」三质感同岛。

**修**（commit 见 todo 行）：单笔改三重同心笔 alpha 渐变（6px/α95 → 4px/α135 → 2px/α200，
同一路径叠描，source-over 合成账 1-∏(1-α/255)）：

| 距轮廓（物理px） | 合成 α | 亮壁纸 ~245 下合成亮度 | 60<亮度<180 过渡窗 |
|---|---|---|---|
| +2.5（最外） | 95 | ≈154 | ✓ |
| +1.5 | 180 | ≈72 | ✓ |
| +0.5 / −0.5 | 239 | ≈16 | 内环近不透明，盖 region 硬切边+路径 AA 白边 |

每条直边外侧恰好 2px 落进扫描门（目标 2-4px ✓），角弧叠几何 AA 约 3px；四边+四角同一渐变
=与顶缘同质感（BrimDeck 矢量 AA 对标）。位图外扩 BandMarginPx=4 仍够（最外 3px+AA<4）。

## 病灶 B1：双数字去重

- `IslandDisplayPlan.Compute`：徽标可见（未读/异常>0）时虚拟环/条 `face.Text` 置空——计数只在
  徽标位显示一次；徽标隐藏时环心保留**进度百分比读数**（`PercentText`，整数%，invariant），
  非计数。
- App 渲染面零改动：ring 面本就以 `face.Text` 作环心读数（双数字现场），置空即消。

## 病灶 B2：无数据不画满

- 新 `IslandAggregateFacts`（Core 纯函数）：`OkShare` total==0 → **0.0**（旧 App.xaml.cs 兜底
  1.0 = 零数据画整圆/满条假健康）；`NoData` 标记；`OfflineBannerText` 文案裁决。
- `IslandContext` 增 `NoData` 位（App 喂）；计划侧 NoData → 虚拟环/条 `Placeholder=true`
  （面级标记，与类文档「零数据一律占位面」口径一致）+ Progress=0 + Text=""=空轨道无读数。
- App.xaml.cs 喂数改走 `IslandAggregateFacts.OkShare/NoData`；`_islandNoData` 初始 true
  （启动首帧无数据=空轨道，不再先画满环）。

## 病灶 B3：离线横幅并入岛体

- XAML：`IslandOfflineRow`（无底色/无描边/无圆角，空心灯+文字）插进 `SurfaceStack`
  PillArea 之后 = 岛体第二行，曲面内无缝长出；经典态恒 Collapsed。
- `UpdateStatus`：按形态二选一（岛→曲面内行、经典→RootBorder 外旧卡片横幅逐字节同旧）；
  文案双喂。断连 fresh（最近成功拉取 <5 分钟）显示「（重试中）」，久断静默旧值
  （`IslandAggregateFacts.OfflineBannerText`）。
- `ApplyIslandChrome` 两分支：离线中切形态横幅行互换（岛内行不漏进经典态）。
- `MeasureSizePx`：岛宽 238 下限与变高判据把 `IslandOfflineRow.Visibility` 并入
  （否则岛态离线被 32px 恒高裁掉）。

## 本地测试（Core，Linux 可测面）

- `dotnet test tests/Beacon.Core.Tests -c Release`：**1430/1430 全绿**（原 1420 + 新 10）。
- 新增：`IslandAggregateFactsTests`（OkShare 零总数归 0 / 混合占比 / NoData 边界 /
  横幅文案两态）；`IslandDisplayPlanTests` 翻 2 处徽标互斥断言（"2"/"3"→""）+ 新
  `Compute_BadgeHidden_RingCarriesPercentReadout`（75%）、
  `Compute_NoData_VirtualFacesEmptyTrackNoReadout`（占位+空读数+份额 0）。
- App 侧（Linux 不编译）已做标识符→命名空间表逐项自查：`IslandAggregateFacts` 两处引用
  文件均有 `using Beacon.Core.Services;`；`IslandOfflineRow/IslandOfflineText` 为同 partial
  类 x:Name 生成字段；无新增 App 方法（无撞名面）；CI Windows 腿为编译门。

## 待验门（本单验收未完项）

1. CI Windows 编译门绿（ci.yml，本 push 触发）。
2. nightly 绿 + 既有 AH 门（展开态/经典胶囊回归：本单不得改展开几何与经典观感）。
3. imac 真机像素扫描（编排方口径）：四边 60<亮度<180 过渡像素 2-4px 均匀；双数字消失；
   无数据态 ring/bar 空轨道。需新构建部署（含本批 commit）。
4. **用户装机亲验通过才闭项（硬门）**。
