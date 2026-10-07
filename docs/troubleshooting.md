# 故障排查（FAQ）

> 安装与启动问题的排查手册。配合 [README 下载节](../README.md) 与 [B-803 端到端走查](acceptance-B803-e2e-walkthrough.md) 使用。

## 双击没反应？

Beacon 是**托盘常驻应用，没有主窗口**——启动成功的可见形态是：屏幕右下角状态胶囊 + 启动通知气泡 + 托盘图标（红圈 b）。三者皆无才按下面顺序排查：

1. **SmartScreen 拦截**：未签名 exe 首次运行会被拦——点「更多信息」→「仍要运行」。
2. **确认进程在不在**：任务管理器搜 `Beacon.App`。在而看不见胶囊 → 记下日志后提 Issue。
3. **查日志**：`%AppData%\Beacon\logs\beacon-*.log`（运行日志，按天滚动）。
4. **启动失败弹窗 / crash 日志**：启动阶段出错会**弹错误框**（含堆栈与日志路径），同时落盘 `%AppData%\Beacon\logs\crash-*.log`——把弹窗截图或日志内容提 Issue 即可定位。

## 便携 zip 校验

`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` 与 Release 内 `SHA256SUMS` 逐行比对；装了 Git Bash 也可 `sha256sum -c SHA256SUMS`。

解压后直接运行 `Beacon.App.exe`——自包含包，无需安装 .NET 运行时，也无需 VC++ 运行库（发布产物实测仅依赖系统自带组件）。

## 安装包

装到 `%LOCALAPPDATA%\Beacon`（无需管理员），开始菜单启动，卸载走系统「添加或删除程序」。安装向导全程简体中文。

安装器复选框/文字显示异常（溢出、看不清）多半是旧版构建—— nightly 滚动更新，重新下载最新 `setup.exe` 即可。

## 日志一览

| 日志 | 位置 | 内容 |
|---|---|---|
| 运行日志 | `%AppData%\Beacon\logs\beacon-YYYYMMDD.log` | 刷新/通知/动作执行，按天滚动 |
| 崩溃日志 | `%AppData%\Beacon\logs\crash-*.log` | 启动与运行期未处理异常堆栈 |
| 配置 | `%AppData%\Beacon\config.json` 等 | 连接/组件/钉选/外观（token 只存 credentialRef，绝不落明文） |
