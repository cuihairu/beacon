[English](README.md) | [中文](README.zh.md)

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
  <a href="https://github.com/cuihairu/beacon/actions/workflows/nightly.yml"><img src="https://github.com/cuihairu/beacon/actions/workflows/nightly.yml/badge.svg" alt="Daily Build"></a>
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

常驻 Windows 桌面的开发者状态与操作中心。更准确地说，它是一个常驻的**数据观察、信息呈现与交互反馈系统**：持续从不同数据源取数，标准化为一份共享状态，交给显示组件呈现，并把用户意图转化为动作，动作结果与外部状态变化再推动下一轮状态更新。**Signal + Action，不是 Dashboard。**

**一条闭环，一份事实。**数据源 → 处理 → 状态 → 显示 → 交互 → 命令执行 → 反馈 → 状态更新。显示组件只是这条闭环的消费方与交互入口，不是业务逻辑的所有者。

**边界。**Beacon 连接外部系统，不重建外部系统。不做通用 Workflow Engine、不做任意脚本执行平台、不做远程机器与服务器管理、不做云端账号、团队协作或多设备控制、不做第三方插件市场、不做完整的事件溯源。桌面外壳是原生 WinUI 3，保持原生，不引入内嵌网页渲染。复杂仪表盘、为图表而图表、复杂主题均不在范围内——默认视图只呈现需要你关注的事，而不是一墙小组件。

## 在线文档

**[https://cuihairu.github.io/beacon/](https://cuihairu.github.io/beacon/)** — 由 [docs 工作流](.github/workflows/docs.yml)自动发布到 GitHub Pages（mkdocs-material）。

## 下载

**[每日构建（nightly）](https://github.com/cuihairu/beacon/releases/tag/nightly)** — main 分支滚动构建，tag 固定 `nightly` 清旧传新，本页永远对应当前主干。Windows x86_64 自包含包（含 .NET 10 + Windows App SDK 运行时），附 `SHA256SUMS` 校验单。直链：[安装包 setup.exe](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64-setup.exe) · [便携 zip](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64.zip)。

由 [daily-build 工作流](.github/workflows/nightly.yml)每天北京时间凌晨自动发布，也可[手动触发](https://github.com/cuihairu/beacon/actions/workflows/nightly.yml)。

### 安装说明（Windows 10 1809+ / Windows 11）

**方式 A · 安装包**：下载 `beacon-nightly-windows-x86_64-setup.exe` 双击安装——装到 `%LOCALAPPDATA%\Beacon`（无需管理员），开始菜单启动，可选桌面快捷方式与开机自启，卸载走系统「添加或删除程序」。

**方式 B · 便携 zip**：下载 zip 并校验（`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` 对照 Release 内 `SHA256SUMS`），解压到任意目录运行 `Beacon.App.exe`——自包含包，无需安装 .NET 运行时。

首启自动常驻托盘并显示状态胶囊（屏幕右下角）与启动通知气泡。GitHub PAT 在托盘菜单 → 设置 → 连接里录入（token 只写 DPAPI 密钥库，配置 JSON 仅存 credentialRef，绝不落明文）。全局热键默认 `Ctrl+Alt+B` 唤出面板；开机自启默认开启（HKCU Run，设置里可关）。权限仅常规用户态——读写 `%AppData%\Beacon\`、DPAPI 加密、`Shell_NotifyIcon`、`RegisterHotKey`，无需管理员。

**双击没反应？** → [故障排查手册](docs/troubleshooting.md)（SmartScreen / 托盘无主窗 / 日志位置 / 启动失败弹窗与 crash 日志，一页说清）。

## 代码签名政策

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

当前 v0.1.x 版本**未签名**——首次运行时 Windows SmartScreen 可能拦截，选「更多信息」→「仍要运行」。SignPath 申请获批后，从下一个版本 tag 起的所有正式版将由 CI 自动签名：构建产物先提交 SignPath 签名，上传的是签好的文件。nightly 滚动构建永不签名。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/mission.md](docs/mission.md) | 产品定位 / 核心概念 / 分期 / MVP 验收标准（原始方案见同目录摘录） |
| [docs/rfc/RFC-001-technical-design.md](docs/rfc/RFC-001-technical-design.md) | 技术方案：架构 / 领域模型 / **显示层架构（L0 悬浮组件 + 四层交互）** / 刷新 / 通知 / 存储 / 风险 |
| [docs/master-plan.md](docs/master-plan.md) | 产品与技术实施总方案：产品范围与设计基线（2026-10-11）——边界 / 交互 / 架构 / 验收 / agent 约束 |
| [docs/solution-structure.md](docs/solution-structure.md) | Solution / 项目结构 / 依赖规则 / 脚手架命令（Windows 机器可执行） |
| [docs/configuration.md](docs/configuration.md) | 配置手册：全部设置项逐项列明——文件、全局设置、外观、通知规则、16 类连接（含各厂商端点与额度语义）、组件字段、钉选布局、动作、导入导出 |
| [docs/troubleshooting.md](docs/troubleshooting.md) | 故障排查手册：SmartScreen / 托盘无主窗 / 日志位置 / 启动失败弹窗与 crash 日志 |
| [docs/store-submission.md](docs/store-submission.md) | 商店提交前置清单：MSIX 打包 / 隐私政策页必填字段 / 商店素材（截图尺寸·描述·分类·年龄分级问卷） |
| [docs/mvp-issues.md](docs/mvp-issues.md) | 43 个 MVP Issue（P0–P8，含验收标准与依赖关系） |

## 路线

1. ✅ 任务书 + 技术 RFC + Solution 目录 + MVP Issue 列表
2. ✅ M1 Windows Shell（Tray / 胶囊 / 热键 / 自启）
3. ✅ M2 Core 内核（刷新调度 / 状态聚合 / 缓存 / 事件总线）
4. ✅ M3 GitHub 闭环（PR / Actions Provider，ETag + 限额感知）
5. ✅ M4 通知与面板（Action 执行器 / Toast / 通知中心 / 托盘着色）
6. 🔨 M5 L0 悬浮组件 + Quick Panel = MVP（代码级完成；余 [装机验收执行清单 71 条](docs/acceptance-checklist.md)——每条含前置条件/验证步骤/预期结果，含 B-803 真机走查与 B-804 干净 VM 验收，待 Windows 机器执行）

> 构建要求：Windows + .NET 10 SDK + Windows App SDK（WinUI 3 不支持跨平台构建）。

## 本地测试口径

CI 与本地开发按改动面三路分流——改一行文档不该全量编译。

1. **文档改动**（`*.md`、`docs/**`、图片）：不编译。主 CI workflow 的
   `paths-ignore` 已跳过这些路径。
2. **代码改动**：只跑改动子集，不全量。
   - `dotnet test <改动 csproj>`
3. **全量**（`dotnet test Beacon.sln -c Release`）：仅以下情形触发——*.csproj / *.sln、
   `.github/**`、Docker/Compose 配置、发布前。
