# SET-UX-11 无障碍与观感真机取证（2026-11-27，imac session 1）

通道：`IMAC\cui` Interactive scheduled task（console session 1，WinUI 不支持 session 0，
见 `../beacon-fix-1126-verify/README.md`）；二进制 `beacon-app-sdk27` =
nightly run 45867924409 资产（2026.11.26+9cb26ef，EXE 0.1.2.0）。脚本
`realmachine-ux11.ps1`（原机 C:\beacon-verify），日志 `ux11-log.txt`。

## 1. 浅色观感（theme 渲染翻转）

- 种子 `theme=light` 重启 → `U11-light.png`：整窗浅色渲染，深色文字可读，主题下拉=「浅色」；
- 对照 `theme=dark` 重启 → `U11-dark.png`：整窗深色渲染，同页同布局；
- 像素均值（全窗 7px 网格采样）：light **240.6** vs dark **22.6**，Δ=**218** —— 渲染真实翻转；
- SJ CI 门已锁 config 落盘（light/dark 双拍），本取证补齐实际渲染翻转半；
- 生效时机标注同帧可目检：「主题（重启生效）」「全局热键即时生效…」「语言（看板与托盘即时生效）」「默认刷新间隔即时重建调度…」。

## 2. 窄窗（600 DIP @200% DPI = 1200 物理 px）

- 默认 1786x1351 → SetWindowPos 1200x1351 → `U11-narrow.png`；
- 热键/语言/刷新间隔/状态条四条 hint 全部换行不截断，控件完整；
- UIA 溢出断言：`NARROW-OVERFLOW=0`（全部可见具名元素 BoundingRectangle 右缘 ≤ 窗右缘+4px）。

## 3. 读屏名（Narrator 播报面）——发现真缺陷并已修

UIA 全树走查（dump 原始文件 `u11-narrator-general.txt` / `u11-narrator-sources.txt`；
交互=支持 Invoke/Toggle/SelectionItem/Value/ExpandCollapse/RangeValue 任一 pattern 或可键盘聚焦）：

| 页 | 元素总数 | 交互数 | 匿名交互 | 匿名构成 |
|---|---|---|---|---|
| 常规 | 140 | 51 | **40** | `module-*` 行 Button ×23 + `module-toggle-*` 开关 ×17 |
| 数据来源 | 156 | 60 | **40** | 同一侧栏（页面区无新增匿名） |

病灶：侧栏模块行与开关**有 AutomationId 无 Name**，开关 On/OffContent 又为空
（`SettingsWindow.Navigation.cs` row/toggle、`SettingsWindow.Connections.cs` conn-toggle
同构造）——读屏只播「按钮 / 切换开关」，无法分辨 GitHub 与 Jenkins。

与 tabwalk `ANON=0` 不矛盾：tabwalk 判据是「AutomationId 与 Name 双空」（取证可识别口径），
读屏播报要的是 Name——判据过松，本轮按读屏口径重测暴露。

修复 = **c0215aa**（SET-UX-11 读屏名）：
`SetName(row, module.DisplayName)`、`SetName(toggle, "{DisplayName} 启用")`、
`SetName(conn-toggle, "{connection.Id} 启用")`。画廊/组件中心行已有 Content 名不受累
（gallery ListViewItem Content=文本；pin 开关 OnContent=「钉」；删除/组件树/展示样式按钮有文字）。

复验：nightly run 45888292592（head c0215aa，2026-11-26T20:04Z 触发，conclusion **success**
全场景绿）资产部署 imac 后重跑 NarrDump（`ux11v-log.txt`）——**全绿**：

| 页 | 交互数 | 修复前 anon | 修复后 anon |
|---|---|---|---|
| 常规 | 51 | 40 | **0** |
| 数据来源 | 60 | 40 | **0** |
| 自定义 HTTP（conn 行面，修复前未 dump） | 81 | — | **0** |

具名抽检：`module-github`=「GitHub」、`module-toggle-github`=「GitHub 启用」、
`conn-toggle-http-a`=「http-a 启用」、`test-http-a`=「测试」、`conn-state-http-a`=「已保存 · 待验证」
（dump 原始文件 `u11v-narrator-{general,sources,http}.txt`）。读屏名面闭环。

## 4. 缩放

- 本取证即 **200%** 实机（imac 主屏 5120x2880 @200%）；
- **100%** = CI windows-latest（visual-evidence 全部场景断言与截图在 100% 跑）；
- **150%** 无现成物理档（imac 单real输出 200%、CI 100%）→ 留装机清单用户复核。

## 取证后状态

config 已从备份还原（theme=system / showCapsule=false / capsuleIsland=false /
settingsWindowWidth=880 / Height=640），取证实例已停止。
