# Beacon

<p align="center">
  <img src="img/logo.svg" width="64" alt="Beacon logo">
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

常驻 Windows 桌面的开发者状态与操作中心：聚合 GitHub / CI 等开发环境状态，主动提醒，一键操作。**Signal + Action，不是 Dashboard。**

## 文档导航

| 文档 | 内容 |
|---|---|
| [任务书](mission.md) | 产品定位 / 核心概念 / 分期 / MVP 验收标准（原始方案见同目录摘录） |
| [定位与首批场景](positioning.md) | P0 方向：公司打包 + AI 额度场景、Provider 三分类、已落地进度与配置示例 |
| [RFC-001 技术方案](rfc/RFC-001-technical-design.md) | 技术方案：架构 / 领域模型 / 显示层架构（L0 悬浮组件 + 四层交互）/ 刷新 / 通知 / 存储 / 风险 |
| [Solution 与项目目录](solution-structure.md) | Solution / 项目结构 / 依赖规则 / NuGet 清单 |
| [MVP Issue 列表](mvp-issues.md) | 43 个 MVP Issue（P0–P8，含验收标准与依赖关系） |
| [故障排查](troubleshooting.md) | 安装/启动 FAQ：日志位置、SmartScreen、SHA256 校验 |
| [配置项参考](configuration.md) | 全部配置逐项说明：文件与生效时机、全局设置、外观与动效、通知规则、12 类连接的端点/凭据/额度口径、组件字段、钉选布局、动作库、导入导出 |
| [文档一致性审计](审计-文档一致性.md) | 文档与源码差异台账（三类 34 条，P0 清单） |
| [装机走查引导清单](acceptance-checklist.md) | 44 项验收一页走查（装 nightly → 逐项验 → 回执） |
| [B-705 L0 行为审计](acceptance-B705-L0-behavior.md) · [B-803 端到端走查](acceptance-B803-e2e-walkthrough.md) | 验收记录与真机清单 |

## 路线

1. ✅ 任务书 + 技术 RFC + Solution 目录 + MVP Issue 列表
2. ✅ M1 Windows Shell（Tray / 胶囊 / 热键 / 自启）
3. ✅ M2 Core 内核（刷新调度 / 状态聚合 / 缓存 / 事件总线）
4. ✅ M3 GitHub 闭环（PR / Actions Provider，ETag + 限额感知）
5. ✅ M4 通知与面板（Action 执行器 / Toast / 通知中心 / 托盘着色）
6. 🔨 M5 L0 悬浮组件 + Quick Panel = MVP（代码级完成；余 B-803 真机走查 + B-804 干净 VM 验收）

## 构建要求

Windows + .NET 10 SDK + Windows App SDK（WinUI 3 不支持跨平台构建）。
