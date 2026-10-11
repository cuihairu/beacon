# 故障排查（FAQ）

> 安装与启动问题的排查手册。配合 [README 下载节](../README.md) 与 [B-803 端到端走查](acceptance-B803-e2e-walkthrough.md) 使用。

## 双击没反应？

Beacon 是**托盘常驻应用，没有主窗口**——启动成功的可见形态是：屏幕右下角状态胶囊 + 启动通知气泡 + 托盘图标（红圈 b）。三者皆无才按下面顺序排查：

1. **SmartScreen 拦截**：未签名 exe 首次运行会被拦——点「更多信息」→「仍要运行」。
2. **确认进程在不在**：任务管理器搜 `Beacon.App`。在而看不见胶囊 → 记下日志后提 Issue。
3. **查日志**：`%AppData%\Beacon\logs\beacon-*.log`（运行日志，按天滚动）。
4. **启动失败弹窗 / crash 日志**：启动阶段出错会**弹错误框**（含堆栈与日志路径），同时落盘 `%AppData%\Beacon\logs\crash-*.log`——把弹窗截图或日志内容提 Issue 即可定位。

## 升级后行为没变？

Beacon 是**单实例常驻**：安装器覆盖了文件，但托盘里驻留的还是旧进程，二次启动只会通知旧实例——「更新了还是老样子」多半是这个。

1. 托盘图标右键 → **Exit**（唯一退出路径），再重新启动；
2. 设置 → 常规底部看 **版本行**：nightly 形如 `2026.10.9+<完整 commit sha>`（`yyyy.M.d` 四段日期，取自构建当天 + publish 时的 `GITHUB_SHA`；早先的 `0.0.yyyyMMdd` 形态会触发 CS7034，已改），对一下构建日期；
3. 版本行还是旧的 = 没装上新包（重新下载 nightly 资产，见下方校验）。

## 便携 zip 校验

`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` 与 Release 内 `SHA256SUMS` 逐行比对；装了 Git Bash 也可 `sha256sum -c SHA256SUMS`。

解压后直接运行 `Beacon.App.exe`——自包含包，无需安装 .NET 运行时，也无需 VC++ 运行库（发布产物实测仅依赖系统自带组件）。

## 安装包

装到 `%LOCALAPPDATA%\Beacon`（无需管理员），开始菜单启动，卸载走系统「添加或删除程序」。安装向导全程简体中文。

安装器复选框/文字显示异常（溢出、看不清）多半是旧版构建—— nightly 滚动更新，重新下载最新 `setup.exe` 即可。

## 组件不出数（白板排查）

「一键添加」「组件向导」添加的组件**应当几秒内上板出数**（添加即钉选、即拉取，无需强刷/重启）。若板面空白，按跳排查（每跳在运行日志里都有一行）：

| 跳 | 日志关键词 | 空白时的含义 |
|---|---|---|
| ① 组件创建 | `一键示例：组件 … 已创建并钉选` | 没有这行 = 添加动作没发生（连接缺失会红字提示） |
| ② 注册+首拉 | `组件注册已重建并立即拉取 N 个` | 没有这行 = 调度器没接到新组件（旧版本 bug，升级 nightly） |
| ③ 拉取结果 | `Widget … refreshed ok: …`（成功）/ `refresh failed: …`（失败带原因） | 失败原因直显在 tile 值位（「拉取失败：…」）——网络/TLS/401/字段映射一目了然 |
| ④ 渲染 | tile 灰灯+「加载中…」→ 有数后变级色 | 恒为「加载中…」= ③一直在失败；看失败原因行 |

另查两处：组件列表（设置 → 对应模块）里该组件是否带「已钉」标记；`组件不出数且日志全无` → 确认模块开关（左侧目录）是开的——停用模块的组件不占板面。

## 小米 MiMo 卡显示 Offline / 不出数？

先分清两件事：**胶囊上的「Offline」横幅是聚合告警**——任何连接不健康（含 Degraded）都会点亮它；真正的失败原因在 **tile 值位**（「拉取失败：…」）和运行日志的 `refresh failed` 行。MiMo 常见三种：

1. **「控制台 Cookie 已失效或未录制」**：额度卡的登录态过期（会话 cookie 过期是常态）。重录：浏览器登录 `platform.xiaomimimo.com` → F12 → 应用 → Cookies → 复制 `api-platform_ph` 的值 → 设置 → 小米模块连接里粘贴（整行 Cookie 粘进来也行，会自动取该键）。
2. **只填了 API Key**：Key 只授权模型调用（`/v1/models` 连接测试绿≠Cookie 有效，两条凭据链），**额度必须另录控制台 Cookie**——Bearer/裸头带 Key 实测全 401。
3. **LiteLLM 网关记账档**：连接 Settings `litellm_spend_url` **默认空=不外发（正常，不是故障）**；填了但卡面显「未接数据库」= 你本机 LiteLLM 网关 config 没接 database——spend 面全回 `no_db_connection`，需网关侧接 DB（SQLite 即可）后自动出「本月已用 $X」。

## 悬浮框白板残留 / 品牌 icon 不显示

- **全局关「数量悬浮窗」后桌面还剩一个白板空窗**：旧版悬浮窗关闭只调 WinUI `Window.Close`，在无边框 NOACTIVATE 窗上不保证销毁窗口句柄——内容已拆、壳残留即白板。已修（Close 后句柄仍存活则硬销毁）；升级 nightly 后须托盘 Exit 重启（单实例常驻）。再出现即新问题，附运行日志反馈。
- **悬浮框/设置目录里的品牌 icon 不显示（只剩灯+文字）**：旧版 `Viewbox(PathIcon)` 组合在 WinUI 3 下量测为空、渲染成空白且挡掉兜底字形。已修（改 Path 形状直渲 + 空几何退兜底）。
- **两态截图证据**：daily-build 每个 run 的 artifact「visual-evidence」——数量悬浮窗开 = 悬浮框全屏+icon 六倍近景；关 = 桌面零残留窗（附硬断言，回归即红构建）。

## 日志一览

| 日志 | 位置 | 内容 |
|---|---|---|
| 运行日志 | `%AppData%\Beacon\logs\beacon-YYYYMMDD.log` | 刷新/通知/动作执行 + 组件管线逐跳诊断，按天滚动 |
| 崩溃日志 | `%AppData%\Beacon\logs\crash-*.log` | 启动与运行期未处理异常堆栈 |
| 配置 | `%AppData%\Beacon\config.json` 等 | 连接/组件/钉选/外观（token 只存 credentialRef，绝不落明文） |
