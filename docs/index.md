# Beacon

<p align="center">
  <img src="img/logo.svg" width="64" alt="Beacon logo">
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

常驻 Windows 桌面的开发者状态与操作中心：聚合 GitHub / CI 等开发环境状态，主动提醒，一键操作。**Signal + Action，不是 Dashboard。**

## 文档导航

| 文档 | 内容 |
|---|---|
| [任务书](任务书.md) | 产品定位 / 核心概念 / 分期 / MVP 验收标准（原始方案见同目录摘录） |
| [RFC-001 技术方案](rfc/RFC-001-技术方案.md) | 技术方案：架构 / 领域模型 / 显示层架构（L0 悬浮组件 + 四层交互）/ 刷新 / 通知 / 存储 / 风险 |
| [Solution 与项目目录](solution-项目目录.md) | Solution / 项目结构 / 依赖规则 / 脚手架命令 |
| [MVP Issue 列表](MVP-Issue列表.md) | 35 个 MVP Issue（P0–P8，含验收标准与依赖关系） |

## 路线

1. ✅ 任务书 + 技术 RFC + Solution 目录 + MVP Issue 列表
2. ✅ M1 Windows Shell（Tray / 胶囊 / 热键 / 自启）
3. ✅ M2 Core 内核（刷新调度 / 状态聚合 / 缓存 / 事件总线）
4. ✅ M3 GitHub 闭环（PR / Actions Provider，ETag + 限额感知）
5. 🔨 M4 通知与面板（Action 执行器 / Toast / 通知中心 / 托盘着色）
6. ⬜ M5 L0 悬浮组件 + Quick Panel = MVP

## 构建要求

Windows + .NET 10 SDK + Windows App SDK（WinUI 3 不支持跨平台构建）。
