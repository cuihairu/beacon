# Beacon

> **Windows Developer Status & Action Center** — See what needs your attention.

常驻 Windows 桌面的开发者状态与操作中心：聚合 GitHub / CI 等开发环境状态，主动提醒，一键操作。**Signal + Action，不是 Dashboard。**

## 文档（当前阶段：文档优先，未开始编码）

| 文档 | 内容 |
|---|---|
| [docs/任务书.md](docs/任务书.md) | 产品定位 / 核心概念 / 分期 / MVP 验收标准（原始方案见同目录摘录） |
| [docs/rfc/RFC-001-技术方案.md](docs/rfc/RFC-001-技术方案.md) | 技术方案：架构 / 领域模型 / **显示层架构（L0 悬浮组件 + 四层交互）** / 刷新 / 通知 / 存储 / 风险 |
| [docs/solution-项目目录.md](docs/solution-项目目录.md) | Solution / 项目结构 / 依赖规则 / 脚手架命令（Windows 机器可执行） |
| [docs/MVP-Issue列表.md](docs/MVP-Issue列表.md) | 35 个 MVP Issue（P0–P8，含验收标准与依赖关系） |

## 路线

1. ✅ 任务书 + 技术 RFC + Solution 目录 + MVP Issue 列表
2. ⬜ M1 Windows Shell（Tray / 胶囊 / 热键 / 自启）
3. ⬜ M2 Core 内核 → M3 GitHub 闭环 → M4 通知与面板 → M5 L0 悬浮组件 = MVP

> 构建要求：Windows + .NET 8 SDK + Windows App SDK（WinUI 3 不支持跨平台构建）。
