<p align="center">
  <img src="docs/img/logo.svg" width="64" alt="Beacon logo">
</p>

<h1 align="center">Beacon</h1>

<p align="center">
  <a href="https://github.com/cuihairu/beacon/actions/workflows/ci.yml"><img src="https://github.com/cuihairu/beacon/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/cuihairu/beacon"><img src="https://codecov.io/gh/cuihairu/beacon/branch/main/graph/badge.svg" alt="Code coverage"></a>
  <a href="https://cuihairu.github.io/beacon/"><img src="https://img.shields.io/badge/docs-online-8A2BE2" alt="在线文档"></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows11&logoColor=white" alt="Windows 10/11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-3DA639" alt="License: Apache-2.0"></a>
  <a href="https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml"><img src="https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml/badge.svg" alt="Daily Build"></a>
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

常驻 Windows 桌面的开发者状态与操作中心：聚合 GitHub / CI 等开发环境状态，主动提醒，一键操作。**Signal + Action，不是 Dashboard。**

## 在线文档

**[https://cuihairu.github.io/beacon/](https://cuihairu.github.io/beacon/)** — 由 [docs 工作流](.github/workflows/docs.yml)自动发布到 GitHub Pages（mkdocs-material）。

## 下载

**[每日构建（nightly）](https://github.com/cuihairu/beacon/releases/tag/nightly)** — main 分支滚动构建，tag 固定 `nightly` 清旧传新，本页永远对应当前主干。Windows x86_64 自包含包（含 .NET 10 + Windows App SDK 运行时），附 `SHA256SUMS` 校验单。直链：[安装包 setup.exe](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64-setup.exe) · [便携 zip](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64.zip)。

由 [daily-build 工作流](.github/workflows/daily-build.yml)每天北京时间凌晨自动发布，也可[手动触发](https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml)。

### 安装说明（Windows 10 1809+ / Windows 11）

**方式 A · 安装包**：下载 `beacon-nightly-windows-x86_64-setup.exe` 双击安装——装到 `%LOCALAPPDATA%\Beacon`（无需管理员），开始菜单启动，可选桌面快捷方式与开机自启，卸载走系统「添加或删除程序」。

**方式 B · 便携 zip**：下载 zip 并校验（`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` 对照 Release 内 `SHA256SUMS`），解压到任意目录运行 `Beacon.App.exe`——自包含包，无需安装 .NET 运行时。

首启自动常驻托盘并显示状态胶囊（屏幕右下角）与启动通知气泡。GitHub PAT 在托盘菜单 → 设置 → 连接里录入（token 只写 DPAPI 密钥库，配置 JSON 仅存 credentialRef，绝不落明文）。全局热键默认 `Ctrl+Alt+B` 唤出面板；开机自启默认开启（HKCU Run，设置里可关）。权限仅常规用户态——读写 `%AppData%\Beacon\`、DPAPI 加密、`Shell_NotifyIcon`、`RegisterHotKey`，无需管理员。

**双击没反应？** → [故障排查手册](docs/故障排查.md)（SmartScreen / 托盘无主窗 / 日志位置 / 启动失败弹窗与 crash 日志，一页说清）。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/任务书.md](docs/任务书.md) | 产品定位 / 核心概念 / 分期 / MVP 验收标准（原始方案见同目录摘录） |
| [docs/rfc/RFC-001-技术方案.md](docs/rfc/RFC-001-技术方案.md) | 技术方案：架构 / 领域模型 / **显示层架构（L0 悬浮组件 + 四层交互）** / 刷新 / 通知 / 存储 / 风险 |
| [docs/solution-项目目录.md](docs/solution-项目目录.md) | Solution / 项目结构 / 依赖规则 / 脚手架命令（Windows 机器可执行） |
| [docs/MVP-Issue列表.md](docs/MVP-Issue列表.md) | 43 个 MVP Issue（P0–P8，含验收标准与依赖关系） |

## 路线

1. ✅ 任务书 + 技术 RFC + Solution 目录 + MVP Issue 列表
2. ✅ M1 Windows Shell（Tray / 胶囊 / 热键 / 自启）
3. ✅ M2 Core 内核（刷新调度 / 状态聚合 / 缓存 / 事件总线）
4. ✅ M3 GitHub 闭环（PR / Actions Provider，ETag + 限额感知）
5. ✅ M4 通知与面板（Action 执行器 / Toast / 通知中心 / 托盘着色）
6. 🔨 M5 L0 悬浮组件 + Quick Panel = MVP（代码级完成；余 [B-803 真机走查](docs/验收-B803-MVP端到端走查.md) + B-804 干净 VM 验收，待 Windows 机器执行）

> 构建要求：Windows + .NET 10 SDK + Windows App SDK（WinUI 3 不支持跨平台构建）。
