# P2 收尾 · 模板感知尺寸防裁切 + progressStyle 优先级 · 取证（2026-11-08）

> 外部评审 2026-10-22 展示组件选择机制「P2 收尾」两项：模板感知尺寸防裁切真机确认 +
> 全局 progressStyle 与实例样式优先级统一（代码半 f9a9aac 已落）。本轮 imac 200% DPI
> 真机探针一次完成两项确认，探针还顺带抓出并修掉两个真实缺陷（ff708ec/c7b0cb2）。

## 探针设计（imac 200% DPI，钉选窗单窗口同框）

- tile A（高度探针）：`http.quota` + `components.tile` = **数字卡模板树**（column/metric+text，
  `DisplayTemplates.Identify` 反解 → `MinTileHeightDips=48`），尺寸档缺省 32 →
  `EffectiveTileHeight = max(32, 48) = 48`。mock `{"data":{"total":100,"remaining":66}}` → 大数字 34%。
- tile B（对照 + 优先级探针）：`http.status` 无声明树 = 传统面（尺寸档 32）；config 全局
  `appearance.progressStyle: "pool"`，该 widget `config.progressStyle: "bar"`。
- 钉选窗 430×198 物理 px = 215×99 DIP = 48 + 32 + 面板 padding/间距，尺寸自洽。

## 结果

| # | 断言 | 结果 |
|---|---|---|
| 1 | 模板感知高度放大（max(档,模板min)）真机生效 | ✅ 数字卡 tile 明显高于 32 对照行，大数字 34% 与 caption 行完整不裁（`pinned-tiles.png`） |
| 2 | 真机探针抓到叠印缺陷 | 槽位面 `ComponentLayout.Columns` 只给三列定义（Auto/Auto/Star）却 `SetColumn(_declarativeHost, 3)`——WinUI Grid 列序超出定义数钳到末列，数据槽位与标签同格叠印（大数字直接压在标签字上，`before-host-label-overlap-10x.png`）。**ff708ec 修**：补第四列定义（Auto×3+Star），复证干净 |
| 3 | 次生硬裁（修叠印后暴露） | host 右移后 caption 行无截断属性，被窗缘硬裁半字（`mid-caption-hardclip-10x.png`）。**c7b0cb2 修**：`ComponentText.Caption` 补 `TextTrimming.CharacterEllipsis`（Body 同款兜底），复证「剩…」省略号收尾无半字 |
| 4 | 实例 progressStyle 压过全局 | 全局 pool 下对照 tile **无圆池环**、走 bar 空轨（http.status 无进度=空轨加载态）——instance-over-global 真机实证；代码口径 `PinnedHostWindow.cs`（widget `config.progressStyle` 优先，缺省回 `_motion.ProgressStyle`） |
| 5 | 全局 progressStyle 优先级代码半 | `AppConfig.Appearance.ProgressStyle`（默认 bar，2026-10-11 用户令翻转）+ 设置页 progressStyle 开关声明，f9a9aac 落地；本轮代码读核确认 |

## 机器与构建口径

- 机器：imac（192.168.5.117，200% DPI 多屏，console 解锁运行中）；origin/main c7b0cb2 同源
  现场 `dotnet build` 0 错误 + publish（每轮修复即同步该文件重建重拍，三轮取证对应三棵树：
  修前 a413ec0 源、ff708ec、c7b0cb2）。
- CI：ff708ec run 42400083245 绿、c7b0cb2 run 42400558637 绿。
- 取证：窗口级 `PrintWindow(PW_RENDERFULLCONTENT)` + 整屏 CopyFromScreen；mock HttpListener
  双端点（/quota.json、/status.json）。

## 证据（docs/acceptance-evidence/p2-tile-sizing/）

| 证据 | 内容 |
|---|---|
| `before-host-label-overlap-10x.png` | 修前叠印铁证：34% 大数字压在 Quota 标签上（三列定义 vs SetColumn(3)） |
| `mid-caption-hardclip-10x.png` | ff708ec 后：叠印消失、caption「剩 66 pct」被窗缘硬裁（无省略号） |
| `pinned-tiles.png` / `pinned-tiles-10x.png` | 终态（c7b0cb2）：48 档数字卡 + 32 档对照同框、caption「剩…」省略号、无叠印无半字 |
| `desktop-pinned.png` | 钉选窗桌面实拍 |

## 验收口径

- 待用户装机确认：组件中心选「数字卡」模板的钉选 tile 行高放大不裁字、caption 长摘要省略号收尾；
  全局进度样式选「圆池」+ 组件级 progressStyle=bar 的 tile 仍走进度条。
