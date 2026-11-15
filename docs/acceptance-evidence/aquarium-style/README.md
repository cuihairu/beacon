# 额度显示方式第三档「整行水族箱」（AQUA-001）CI 取证

- run：nightly [43755252181](https://github.com/cuihairu/beacon/actions/runs/43755252181)（head 4195e2d，2026-11-15 全绿）
- 代码链：220f6cd（Core AquariumWave+设置档）/ da7437e（PinTile 渲染与档位接线）/ 83998ef（场景 AI 取证）/ 3c5c279（逐件翻 Visible）/ c471f35（水体分层色断言）/ b54a5cd（Core 周期对齐采样）/ ac7a8f3（pool 已证渲染形态）/ b72fb72（crest 露出断言）/ 9e90454（crest 提到水面线上）/ 4195e2d（watchdog 抬线）
- Core 测试：1312 全绿（其中 AquariumWaveTests 7 例：点数/窄宽/周期对齐/无缝循环/振幅相位/半周期翻转/慢波量化锚）

## 断言口径（场景 AI 四阶段）

| 阶段 | mock 档 | 断言 |
| --- | --- | --- |
| 20% | p20（limits 20/15/20） | 带 12DIP 底带像素 ≥120 且水体色=success 绿 (63,185,80) ≥120；crest rows 2-5 露出色 ≥40（60% 态起用） |
| 60% | p60（60/10/20） | 水体色=warning 橙 (210,153,34) ≥120；crest rows 2-5 ≥40；双帧差分仅作校准 |
| 95% | 注入 95 | 水体色 ≥120（ProgressTiers 阈值 0.8→warning 橙非 error 红） |
| off | p60 + motion off | 水位照常渲染；静止差分硬断言仅在活水可证时启用 |

档位色单源 BarFillColor→ProgressTiers.Level；底轨冷色 (62,72,91) 与档位色相距远，tol 45 无串扰。

## 本地逐行复核（对 AI-*-band.png 6x 近景源像素）

三态水位与波峰位置全部吻合几何预测（12DIP 带，waterline=12−fraction×12，crest 顶=waterline−1.6DIP）：

- 20% 绿：首个色行 DIP 8.0 = 9.6−1.6 波峰顶预测值；水上（波峰露出）2448px
- 60% 橙：首行 DIP 3.0 ≈ 4.8−1.6 预测；水上 2832px
- 95% 橙：首行 0 = 波峰顶 −1.0DIP 被 host Clip 裁顶的预期边界行为
- off 档：像素分布与 60% 全同（水位照常渲染、波纹静止）——motion off=静止不是消失

## CI 会话不可证项（G7c 降级，转装机目检）

双帧差分两次均为 0（AI-motion-frame1/2、AI-off-frame1/2）：CI 会话 Composition 属性动画本就不可证（呼吸灯同款裁决）。波纹活水观感、幅度/频率是否「小且慢不抢眼」、切换设置后即时生效的手感——**待用户装机亲验，视觉条目不闭项**。
