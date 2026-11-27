# UI-SPACING-1127 逐窗间距现状取证（2026-11-27）

CI 半来源：nightly run 45948548406（head be16a1a）visual-evidence 工件，Windows 100% DPI。
imac 200% 真机半：`imac-200/`（2026-11-27，session 1 UIA + PrintWindow，包 c0215aa）。
对应 todo.md「UI-SPACING-1127」治理方案①的截图证据；本单红线「无截图不动代码」——
本目录只有取证，不含代码改动。

## 逐窗观察（icon-text 间距口径）

| 窗口 | 截图 | 观察 |
|---|---|---|
| 设置 · 常规页 | settings-general.png | 侧栏模块行（GitHub/Jenkins/本机/Herald/智谱 GLM）图标-标题/副标题间隙清晰，右侧表单标签-控件-提示正常——17c5772 收编后无回归 |
| 设置 · 桌面显示页 | settings-desktop-page.png | 状态条三选一卡片、窗口与看板区正常；侧栏同上 |
| 组件中心画廊 | gallery.png | 模板清单行距均匀、预览/JSON/按钮区正常，无贴死 |
| 看板 | board.png | OVERVIEW 来源卡品牌图标-文字有间隙（智谱 Z 卡、DeepSeek 卡）；时间线行距正常 |
| 岛收起行 | island-collapsed.png | 「GLM 5h 38%」「DeepSeek」徽标点-文字间隙清晰 |

## 结论与病灶面缩小

CI 有截图/UIA 通道的全部窗口在当前 nightly 上 icon-text 间距**均无「贴死」病灶**。
用户 2026-11-27 报障「文字和 icon 之间的空隙都没有了」的可能落点：

1. 用户机器装的是旧包——间距收编 17c5772（BEACON-FIX-1126 病灶 C）2026-11-26 才进
   nightly，报障时点的安装包可能早于该修复；
2. 病灶在 CI 无通道的面：详情窗字段区、连接向导表单、岛展开态 200% 真机渲染
   （CI 100% 与 imac 200% 的 DIP 布局同源，但渲染路径差异只能真机看）。

## 候补收编点（代码考古，不动代码）

- QuickPanelWindow.Sections.cs 来源卡 topRow `Row(6)`：品牌图标-文字间距 6 DIP，
 低于 IconTextGap=10，board.png 实测间隙可读不贴死；是否上调待用户指认后统一拍板。
- DetailWindow Column 词汇件 Spacing=10，已与 IconTextGap 等值。
- 其余窗口 inline 字面量清单见 todo.md 病根定性段（2026-11-27）。

## imac 200% 真机半（2026-11-27）

会话：ssh imac → 定时任务 `IMAC\cui` Interactive session 1（避开 WinUI session 0 崩）；
包 `C:\beacon-verify\beacon-app-sdk27\Beacon.App.exe`（2026.11.26+c0215aa，含 17c5772 间距收编）；
脚本 `C:\beacon-verify\realmachine-spacing1127.ps1`，逐面 PrintWindow 截图 + UIA BoundingRectangle
直读量间距，取证结束恢复用户原 config（备份 `config.json.bak1127`）。

| 面 | 截图 | 观察（200% 物理像素） |
|---|---|---|
| 设置 · 常规页 | imac-200/settings-general.png | 侧栏 icon→文字 UIA 直读 = 20 px = 10 DIP × 2，与 IconTextGap 常量一致，无贴死 |
| 设置 · 数据来源页 | imac-200/connection-list.png | 连接行文字、状态 chip、开关列、按钮组间距清晰；开关不被文字挤压 |
| 连接向导表单 | imac-200/connection-wizard.png | 表单标签/输入框/按钮行距正常，无 icon-text 贴死 |
| 看板 | imac-200/board.png | 来源卡品牌图标（智谱 Z）与文字有间隙；时间线行圆点-时刻-摘要间距清晰 |
| 详情窗 | imac-200/detail.png | 时间线圆点-时刻-摘要间距清晰；**另见下方独立发现** |

**独立发现（非本单报障口径，如实记录）**：详情窗 DETAILS 区 `status`/`endpoint` 两行在 200%
物理像素下出现文字纵向重叠（前一行下沿压到后一行上沿）。该现象属「文字行重叠」而非用户报的
「文字与 icon 之间无空隙」；本单红线「无截图不动代码」，不在 UI-SPACING-1127 项下改码，
另立条目或由用户裁定后再动。

岛面补取（同日第二轮，`imac-200/island-uia.txt` + `island-faces-collapsed.png`）：
按 ISLAND-FLANK-1 成功脚本口径把岛钉主屏 DISPLAY1 + 上岛三组件，UIA `island-face-*` 树拿到：
`island-face-http-a.status` rect=(2522,17,91,30)，其子 Text「Pack A」left=2548 →
**面徽标→文字 26px = 13 DIP @200%**（非贴死）；进度环/条两虚拟面同在树。
展开态本轮未触发（`NEVER-EXPANDED`，height 134px 收起档；预览钩子与鼠标悬停均未令窗高超过 160px），
展开态 200% 截图沿用 ISLAND-FLANK-1 已归档件（`docs/acceptance-evidence/island-flank-verify/`），
本单不重复取证。

## 待办

① 用户指认最差窗口（agent 侧五面截图均无「icon 贴死」）；② 按窗收编 DesignTokens；
③ App.xaml 资源字典镜像；④ CI 病灶 C 门推广到全窗口；⑤ 用户逐窗亲验。
另：详情窗 DETAILS 文字行重叠待裁定是否单开条目。
