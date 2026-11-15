# island-data · 岛视觉证据（nightly visual-evidence AG/AH 场景）

来源：nightly run 43679247117（2026-11-15，main d555834 = D-4 收起宽贴内容，全绿）。
首轮 run 43662875796 建 AG/AH 场景（AH1b 假红）→ run 43671206004 修后首绿（main 202cc26）→
本 run 为 D-4 宽度改版后的重取证（收起岛宽=收起行+2×Inset 30：single 1024→648 crop、
carousel 1024→784，stack/expanded 持平——4 面内容与展开下限 238 不受 D-4 影响）。
场景代码：`.github/workflows/scripts/visual-evidence.ps1` AG/AH 段。

| 文件 | 场景 | 断言锚 | 内容 |
|---|---|---|---|
| `AG-island-stack.png` | AG1 stack 档 | UIA `island-face-{island.progressring,island.progressbar,bigmodel.usage:GLM,deepseek.balance:ds}` 全在场 | 收起行 4 面（环 9/条/条/chip DeepSeek + 品牌标），宽 238 不变 |
| `AG-island-single.png` | AG2 single 档 | 环在场、条/GLM/DS face 不在场 | 序首环面常驻，宽贴内容（环+品牌标） |
| `AG-island-carousel.png` | AG3 carousel 档 | 12 轮采样 ≥2 个不同 face id | 单面轮转在走，宽随轮转面内容浮动 |
| `AH-island-expanded.png` | AH1 展开态 | 窗高 ≥1.6×收起基线 + 预览行含 GLM 文本 | 探针首展（BEACON_DEBUG_ISLAND_PREVIEW=1），宽保 238 下限 |
| `AH-island-collapsed-after-hover.png` | AH1a→AH1b | 指针真进（PointerEntered）→移出→窗高回基线 | 悬停往返的收起帧 |
| `AH-island-reexpanded.png` | AH1c | 指针移回→窗高再长出 | 悬停往返的再展帧 |
| `AH-island-placeholder.png` | AH2 占位面 | `island-face-bigmodel.usage:ghost` 在场（enabled:false 无状态） | 空心灯点+淡字占位（不造数据） |
| `AH3-drilldown-panel.png` | AH3 下钻链 | 岛 face 点击 → 看板定位 `chip-智谱` | 组件级下钻的快速面板落点 |

以后岛类 todo 条目验收=本目录场景证据（巡检单 beacon-island-parity Block B ③），
不再出现「只有单测绿」的岛勾项。
