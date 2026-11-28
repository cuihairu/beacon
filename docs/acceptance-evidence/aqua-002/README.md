# 注水桶波感优化（BEACON-AQUA-002，2026-11-28）imac 真机取证

代码：b1c2fb4（Core AquariumWave 自适应函数+漂移/高光常量、MotionEngine 时长改读 Core+StartGlint/StopGlint、PinTile 自适应接线+1 DIP 高光线、Core 测试 1456 全绿含新增 7 例锁值）。

## 真机通道

imac schtask `BAqua002`（IMAC\cui Interactive，session 1）：起 Beacon（bare，正常启动）→ 枚窗 → PrintWindow 截 pinned tile。**注意 bare 启动必须走 schtask**——ssh 直跑落在 session 0，WinUI 必崩 0xC000027B（22:30 三连 WER 实证，历史病同款：#10163 ssh session 0 不支持）；`--settings` 从 schtask 会话起窗所以 theme002 通道一直无恙。取证对象=imac 现用注水桶档（全局 progressStyle=aquarium，motion full）的 GLM tile（真实额度水位 61%/5h）。

## 改前/改后对照（同机同 tile，只换二进制）

| | 二进制 | 振幅（公式） | 实测波带（物理 px，2x DPI） | 漂移周期 | 高光 |
| --- | --- | --- | --- | --- | --- |
| 改前 | 810ed69 | 3.0 DIP（旧常量） | 6 px（=3 DIP 预测，与 aquarium-style 档案「5-7px」吻合） | 3.8s/5.4s | 无 |
| 改后 | b1c2fb4 | clamp(桶高35/8, 3, 6)=4.375 DIP | 8 px（=4.375 DIP 预测 8.75 精确命中） | 2.2s/3.4s | 1 DIP 亮线 6s 间歇扫过 |

文件：`before-tile.png` / `after-tile.png`（原图 524x70）与 `-3x.png`（最近邻放大）；测量=逐行水体色（橙）右缘的 body/crest/trough 统计，`full_band=crest-trough` 是振幅不变量（两帧相位不同：改前 crest_lead=6，改后 trough_recede=7，带宽 6→8 即振幅 3→4.375 DIP）。

## 高光扫过证据

`glint-frame7.png` / `glint-frame8.png`（1s 间隔连拍 8 帧中的两帧）帧间差分：**row 50-52 全水体宽（x 23-318）水平带移动**——1 DIP 横向亮线（Width=已灌宽、纵向扫过）的唯一空间特征；其余行差异=波峰前沿相位移动。帧间差分非零本身即超历史：CI 会话 Composition 动画双帧差分恒 0（G7c 降级裁决），imac 真机 session 1 逮到了动画帧。

## 覆盖口径

- Core 锁值：`AquariumWaveTests` 8 例（自适应 clamp 边界 3/6 与 28/56 恰在边界、0/负数退化输入、带内线性、标定桶 120 宽→40 周期与默认一致、自适应值喂回波形无缝循环不变式）。
- motion off 档静止门未动：`StartGlint` 与 `StartRipple` 同走 `WavesAllowed` 门（off 档内部直接不启动）。
- 高光间歇可见（6s 周期约 1.3s 在场）：静态截图 22% 概率逮到本体，差分行即其在场证据；真人目检观感（波感更活/高光不抢眼）仍属装机亲验面。
