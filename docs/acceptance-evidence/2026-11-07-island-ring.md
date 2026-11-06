# 岛圆环进度徽标 + 组件化两扩围 · 取证（ISLAND-RING-1，2026-11-07）

> 用户令链（03:1x–03:3x 连发四令）：①岛要有圆圈进度展示（BrimDeck 技法学照、原生实现）；
> ②进度环/进度条收编为组件中心两个组件（勾选即变、无默认样式概念）；③岛消费 hiddenComponents
>（勾掉的组件不进岛的状态聚合）；④展示载体（悬浮岛/悬浮框/看板）统一进组件中心勾选，托盘只留设置。
> 待用户装机亲验后闭项。

## 交付

- `RingProgressBadge`（底环+进度弧+中心数字）：BrimDeck `UI.Ring` 技法学照（源码对照读法，实现是本仓
  自己的 WinUI 代码）——弧起笔 12 点方向、ArcSegment 终点 (center−r·sinθ, center−r·cosθ)、
  IsLargeArc 过半翻转、Counterclockwise、Round 端帽、≥.999 整圆 Ellipse 兜毛边。
  WinUI 落地差异两处（CI 实证）：ArcSegment 无 WPF 式位置构造器（属性初始化器）、无 IsStroked。
- `BarProgressBadge`（轨道+左锚填充）：与环同数据源（ok 份额+级别色）。
- 进度环/进度条 = 组件中心「悬浮岛」组两个虚拟组件（`island.progressring` / `island.progressbar`，
  Core `ComponentCenterCatalog` 单源）：勾环显环、勾条显条、都勾都显（环+条横排）、都不勾不显。
- 岛消费 hiddenComponents：**过滤在 `StatusAggregator` 源头**（隐藏集 provider 实时读 config），
  勾掉的组件不进任何聚合消费面（岛灯/环/条/徽标/横幅、托盘灯同源同口径）；离线横幅只数仍持有
  可见组件的离线连接。**取证踩坑**：首版在消费端用 App 层 lastStates 重投影聚合，实机日志
  （island-trace 04:12:39，raw `Error:1` → filtered `err=0`）抓到聚合事件先于组件状态事件的顺序
  竞态抹掉真实级别——修正为源头过滤（d98e674）。
- 展示载体收编：组件中心页首「展示载体」区三项勾选（悬浮岛→旧字段 `CapsuleIsland`、悬浮框→
  `NumericFloatingEnabled`、看板→增量可选字段 `boardEnabled` 缺键=true 向后兼容）+ 悬浮形态下拉
  收编同区；设置页旧开关位置只留跳转提示；托盘菜单收编为「设置+退出」（CI E2/E3 键盘导航
  down-count 同步改 0/1）；看板全部唤起面（热键/岛点击/L0 下钻/Toast 回退）走单一 ToggleBoard 门。

## 机器与构建口径

- 机器：imac（192.168.5.117，Windows，200% DPI 多屏，console 会话解锁运行中）。
- 代码：`origin/main` c78200e（含 d98e674 源头过滤修正）现场构建：`dotnet build` 0 错误、
  四测试套全绿（Core 1001 / Actions 27 / Connections 673 / Storage 41）、
  `dotnet publish -r win-x64 --self-contained true`。
- 取证：PerMonitorV2 DPI 感知 + 窗口级 `PrintWindow(PW_RENDERFULLCONTENT)`（200% DPI 实测教训：
  非 DPI-aware 进程 GetWindowRect 返回虚拟化坐标，截图被裁成左半）。
- mock：本机 HttpListener 双端点（`/a.json`=ok、`/b.json`=failed），http.status 双组件
  （打包机 A=Success、部署流水线=Error）→ 真实聚合 1 ok / 1 err → 环弧 50% 红。

## 证据（evidence-out/island-ring/ → docs/acceptance-evidence/island-ring/）

| 证据 | 内容 |
|---|---|
| `island-ring-bar-both.png` | 环+条都勾（默认全勾）：红弧 ~50% + 红条 ~50% + 中心未读数 + Beacon 词标 |
| `island-widget-hidden-ok.png` | **组件中心勾掉故障组件（hiddenComponents=[http-b.status]）→ 岛回绿满环+满条**（红态消失=「勾掉组件→岛对应元素消失」行为实证；重启口径=与设置页写同一 config） |
| `island-ring-only.png` | 只勾进度环（勾掉 island.progressbar）：条消失 |
| `island-bar-only.png` | 只勾进度条：环消失，计数跟条走 |
| `island-progress-none.png` | 环条都不勾：岛只剩品牌标（无默认样式兜底） |
| `island-expanded.png` | 悬停展开（BEACON_DEBUG_ISLAND_PREVIEW=1）：展开态行结构不变（级别点+来源+摘要），收起态环/条在场 |
| `probe-t5s/t30s.png` + island-trace `status ok=1 err=1 ratio=0.50 ring=#F85149` | 修复后数据链实证（像素计数 red=829/green=0） |
| `island-vs-brimdeck-side-by-side.png` | 并排大图（本方三态实拍；BrimDeck 列占位——见下） |

## BrimDeck 参照列说明（诚实口径）

imac 已装 BrimDeck（C:\Users\cui\AppData\Local\Programs\BrimDeck）且已试拍三轮：其主窗为
5120×832 全宽条带（WPF HwndWrapper），PrintWindow 渲染黑底（透明 WPF 面）、屏面裁切与全幅顶带
扫描（9440px VirtualScreen 顶 300px）均未见刘海面渲染——当前配置下 BrimDeck 刘海模块未出画面。
参照列回退占位，待 BrimDeck 侧启用刘海后补真图。

## 门禁与账目

- 本地全量：Core 1001 / Actions 27 / Connections 673 / Storage 41 全绿（新增行为锁：
  IslandProgressComponentTests / StatusAggregatorHiddenTests / CarrierCatalogTests，均红先行）。
- push 链：1e4f581 → ad7043f → e80fa89 → 1614560 → d98e674 → c78200e（每验证单元一推）。
- 待用户装机亲验后闭项（todo ISLAND-RING-1 保持 OPEN）。
