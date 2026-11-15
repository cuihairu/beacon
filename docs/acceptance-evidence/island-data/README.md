# island-data · 岛视觉证据（nightly visual-evidence AG/AH 场景）

来源：nightly run 43671206004（2026-11-15，main 202cc26，全绿）。
场景代码：`.github/workflows/scripts/visual-evidence.ps1` AG/AH 段。

| 文件 | 场景 | 断言锚 | 内容 |
|---|---|---|---|
| `AG-island-stack.png` | AG1 stack 档 | UIA `island-face-{island.progressring,island.progressbar,bigmodel.usage:GLM,deepseek.balance:ds}` 全在场 | 收起行 4 面（环 9/条/条/chip DeepSeek + 品牌标） |
| `AG-island-single.png` | AG2 single 档 | 环在场、条/GLM/DS face 不在场 | 序首环面常驻 |
| `AG-island-carousel.png` | AG3 carousel 档 | 12 轮采样 ≥2 个不同 face id | 单面轮转在走 |
| `AH-island-expanded.png` | AH1 展开态 | 窗高 ≥1.6×收起基线 + 预览行含 GLM 文本 | 探针首展（BEACON_DEBUG_ISLAND_PREVIEW=1） |
| `AH-island-collapsed-after-hover.png` | AH1a→AH1b | 指针真进（PointerEntered）→移出→窗高回基线 | 悬停往返的收起帧 |
| `AH-island-reexpanded.png` | AH1c | 指针移回→窗高再长出 | 悬停往返的再展帧 |
| `AH-island-placeholder.png` | AH2 占位面 | `island-face-bigmodel.usage:ghost` 在场（enabled:false 无状态） | 空心灯点+淡字占位（不造数据） |

AH3（下钻链）无独立截图落此目录——断言面在快速面板（`chip-智谱` 定位 + `AH3-drilldown-panel.png` 在
run artifact 里），岛侧证据由 AH3 的 face 点击前置覆盖。

以后岛类 todo 条目验收=本目录场景证据（巡检单 beacon-island-parity Block B ③），
不再出现「只有单测绿」的岛勾项。
