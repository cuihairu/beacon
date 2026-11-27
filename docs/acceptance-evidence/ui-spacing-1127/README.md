# UI-SPACING-1127 逐窗间距现状 · CI 取证半（2026-11-27）

来源：nightly run 45948548406（head be16a1a）visual-evidence 工件，Windows 100% DPI。
对应 todo.md「UI-SPACING-1127」治理方案①的 CI 半；imac 200% 真机半与用户指认最差窗口
待真机在线后补（本单红线：无截图不动代码，本目录即截图证据半，不含代码改动）。

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

## 待办（imac 在线后）

① 200% 真机逐窗截图（用户指认最差窗口优先）；② 按窗收编 DesignTokens；
③ App.xaml 资源字典镜像；④ CI 病灶 C 门推广到全窗口；⑤ 用户逐窗亲验。
