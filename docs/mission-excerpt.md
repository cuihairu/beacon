可以。下面这份我会按**可以直接交给其他 Code Agent 执行**的程度来写，不只是产品想法，而是把定位、UX、架构、模块、MVP、插件机制、Windows 实现、后续演进和验收标准都明确下来。

:::writing{variant=

══════

u0026 Action Center**
>
> **See what needs your attention.**

---

## 1. 项目概述

### 1.1 项目名称

**Beacon**

Beacon 的含义是：

> 信标、灯塔、指示信号。

Beacon 不定位为传统的 Web Dashboard，而定位为一个**常驻 Windows 桌面的开发者状态与操作中心**。

它持续收集开发环境中的重要状态，并在需要时提供快速操作。

核心理念：

> **Beacon watches your development environment, so you don't have to.**

用户无需不断打开：

- GitHub
- GitHub Actions
- GitLab
- CI/CD
- Jenkins
- 构建机器
- Agent 管理页面
- Docker
- 服务器
- 各种内部工具

而是通过 Beacon 在桌面上快速知道：

> **现在发生了什么？什么需要我的注意？我现在可以做什么？**

---

# 2. 产品定位

## 2.1 不是什么

Beacon 不应该成为：

- 传统 BI Dashboard
- 大型监控系统
- Grafana 替代品
- Jenkins 替代品
- GitHub 替代品
- CI/CD 平台
- 任务管理软件
- 全功能工作流平台

这些系统已经负责自己的领域。

Beacon 的职责是：

> **把开发环境的重要状态聚合到桌面，并提供低成本的下一步操作。**

---

# 3. 核心用户场景

Beacon 首先面向个人开发者和小型开发团队。

典型工作环境：

```text
Windows PC

══════

Developer
```

---

# 4. 核心产品模型

Beacon 的所有功能最终围绕 7 个核心概念展开。

## 4.1 Widget

**Widget = 信息和交互的基本单位**

例如：

```text
GitHub PR Widget
CI Status Widget
Build Widget
Agent Widget
Machine Widget
Workflow Widget
Notification Widget
```

Widget 负责：

- 展示数据
- 状态
- 用户交互
- 快捷操作

---

# 5. Connection

**Connection = 外部数据源和服务连接**

例如：

```text
GitHub
GitLab
Jenkins
Generic REST API
Docker
Beacon Server
Custom API
```

Connection 负责：

```text
Authentication
API Endpoint
Credentials
Request
Response
Refresh
Error
```

一个 Connection 可以被多个 Widget 使用。

例如：

```text
GitHub Connection

══════

Workflow Action
```

避免每个 Widget 都重复配置 GitHub Token。

---

# 6. Action

**Action = 用户可以立即执行的操作**

例如：

```text
Build
Test
Deploy
Restart
Run Workflow
Cancel Workflow
Open PR
Open Repository
Open Logs
Run Agent
```

Action 可以来自：

```text
HTTP API
Webhook
Local Command
PowerShell
Shell Script
Beacon Workflow
External URL
```

核心目标：

> 从“看到问题”到“执行操作”只需要一次点击。

---

# 7. Workflow

**Workflow = 多步骤自动化任务**

例如：

```text
Build Client

══════

Notify
```

Workflow 是 Beacon 的高级能力。

MVP 不应该一开始实现复杂 Workflow Engine。

第一阶段只支持简单 Action。

---

# 8. Notification

**Notification = 主动提醒**

Beacon 应该区分：

### 信息

```text

══════

Build failed
```

Notification 可以来自：

```text
GitHub
CI/CD
Machine
Workflow
Beacon Server
Widget Threshold
```

最终通过 Windows Notification 展示。

---

# 9. Dock

Dock 是 Beacon 最有特色的桌面交互之一。

它不是传统意义上的 Windows Taskbar Dock。

而是：

> 一个可以常驻桌面的轻量状态入口。

例如：

```text

══════

Exit
```

---

# 11. 三层交互模型

Beacon 不应该一打开就是一个巨大 Dashboard。

采用 Progressive Disclosure。

## Level 1：Status Capsule

桌面常驻。

只显示：

```text

══════

[Build] [Test] [Deploy]
```

---

## Level 3：Detail Window

需要深入查看时：

```text
Build #1024

Status: Failed

Repository:
cuihairu/example

Branch:
main

Duration:
12m 31s

Stage:
Compile

Error:
...

[Open GitHub]
[Retry]
[Cancel]
```

这样可以保证 Beacon：

> 平时不打扰，出问题时迅速进入细节。

---

# 12. UI 设计原则

## 12.1 小

Beacon 默认不能占据大量桌面空间。

## 12.2 快

打开面板应该接近瞬时。

## 12.3 信息密度高

优先显示：

```text
Status
Severity
Progress
Next Action
```

而不是大量描述文字。

## 12.4 操作优先

每一个重要状态尽可能存在：

```text
Open
Retry
Run
Cancel
Deploy
Restart
```

等下一步动作。

---

# 13. Windows 平台定位

Beacon **只支持 Windows**。

因此不需要：

- macOS
- Linux
- Tauri 跨平台抽象
- Electron

优先使用 Windows 原生技术。

---

# 14. 技术栈

推荐：

```text
Language:
C#

UI:
WinUI 3

Runtime:
.NET

Windows:
Windows App SDK
```

辅助：

```text
CommunityToolkit.WinUI
```

Windows API 用于：

```text
System Tray
Global Hotkey
Window Position
Always On Top
Startup
Multi Monitor
Toast Notification
Credential Storage
```

---

# 15. 为什么不用 Tauri

Beacon 是明确的 Windows-only 产品。

Tauri 的主要价值是：

> 跨平台桌面应用。

如果没有跨平台需求，它会引入不必要的：

```text
Web UI
Rust Bridge
WebView
IPC
```

Beacon 更适合：

```text
C#

══════

Read
```

---

# 18. 配置系统

Beacon 应该坚持：

> **Local First**

默认不要求用户注册 Beacon Cloud。

配置保存在本机。

例如：

```text
%AppData%

══════

```

可以包含：

```text
config.json
widgets.json
connections.json
workflows.json
```

但是：

> **Secrets 不允许直接放进普通 JSON。**

---

# 19. Secret Storage

Windows 使用：

> Windows Credential Manager / DPAPI

保存：

```text
GitHub Token
API Key
Password
Webhook Secret
```

普通配置只保存：

```text
credentialId
```

而不是：

```text
token=ghp_xxxxxxxxx
```

---

# 20. 导入导出

用户应该可以：

```text
Export Configuration
Import Configuration
```

导出：

```text
Widgets
Connections Metadata
Actions
Workflows
Appearance
```

默认：

> 不导出 Secrets。

导入后提示用户重新配置 Credential。

---

# 21. GitHub 集成

第一阶段优先实现 GitHub。

GitHub Connection：

```text
GitHub

══════

Running
```

提供：

```text
Run
Cancel
Retry
Open
```

例如：

```text
[Run Build]
```

实际上调用：

```text
GitHub Actions workflow_dispatch
```

---

# 23. CI/CD 抽象

不要把 CI/CD 逻辑写死成 GitHub Actions。

抽象：

```text
CI Provider
```

例如：

```text
GitHub Actions
Jenkins
GitLab CI
Generic REST
```

统一状态：

```text
Queued
Running
Success
Failed
Cancelled
Skipped
Unknown
```

这样 UI 不需要关心底层 CI 系统。

---

# 24. Machine

未来 Beacon 可以管理：

```text
Local Machine
Build PC
Test PC
Agent PC
Server
```

Machine Widget：

```text
Build-PC-01

══════

Running

Task:
Build Apollo

Elapsed:
08:21

[Open]
[Stop]
```

这一层未来可以连接你的中央 Agent / Worker 系统。

但：

> **不要把 Agent Server 写死进 Beacon。**

应该作为 Connection + Provider。

---

# 26. Generic REST

Beacon 必须支持 Generic REST。

用户可以配置：

```text
GET https://example.com/api/status
```

或者：

```text
POST https://example.com/build
```

然后将结果映射到 Widget。

这是保证 Beacon 可扩展性的关键。

---

# 27. Action Button

所有重要 Widget 都可以配置 Action。

例如：

```text
Build Widget

Status:

══════

Build Client
```

---

# 31. Notification Engine

Notification 不是简单 Toast。

Beacon 应有：

```text
Notification Rule
```

例如：

```text
IF
CI status == Failed

THEN
Toast
Sound
Dock turns red
```

或者：

```text
IF
Build duration > 15 min

THEN
Notification
```

---

# 32. 状态聚合

Beacon 最重要的能力之一：

> 将多个系统压缩成一个简单的总体状态。

例如：

```text
GitHub

══════

Beacon
```

Dock 只需要告诉用户：

> 有问题。

用户点击后再查看：

> 什么出了问题。

---

# 33. Severity

统一：

```text
Info
Success
Warning
Error
Critical
```

对应：

```text

══════

```

不要让每个 Provider 自己定义完全不同的状态。

---

# 34. Refresh Strategy

不要所有数据每秒刷新。

不同数据采用不同策略：

```text
GitHub PR       30~120s
CI              10~30s
Machine         5~30s
Agent           5~15s
Workflow        Event / 10s
Static          Manual
```

支持：

```text
Polling
Event
Manual Refresh
```

---

# 35. Cache

每个 Connection / Widget 应该支持缓存。

目标：

> Beacon 打开时不要因为网络请求而空白等待。

启动：

```text
Load Cache

══════

Unable to refresh

Last successful update:
2 minutes ago

[Retry]
```

而不是：

```text
Exception...
```

---

# 37. Offline Mode

Beacon 应该能够离线运行。

离线时：

```text
Last known state
```

仍然可见。

并明确：

```text
Offline
Last update:
10:31
```

---

# 38. Hotkeys

至少支持：

```text
Ctrl + Alt + B
```

打开/隐藏 Beacon。

未来：

```text
Ctrl + Alt + 1
Build
Ctrl + Alt + 2
Test
Ctrl + Alt + 3
Deploy
```

Hotkey 必须允许用户修改。

---

# 39. Startup

支持：

```text
Start Beacon with Windows
```

启动后：

```text
Tray
+
Dock
```

不应该默认打开大型窗口。

---

# 40. Multi Monitor

Beacon 必须考虑多显示器。

Dock 应该支持：

```text
Primary Monitor
Current Monitor
Specific Monitor
```

用户可以选择：

```text
Bottom Right
Top Right
Bottom Left
```

---

# 41. Always on Top

Dock 默认：

```text
Always on Top
```

但不能遮挡用户工作。

因此需要：

```text
Opacity
Auto Hide
Compact Mode
```

---

# 42. Appearance

Beacon 不需要一开始做大量主题系统。

MVP：

```text
Light
Dark
System
```

以及：

```text
Opacity
Compact
Normal
Large
```

后续再增加：

```text
Accent Color
Widget Appearance
Background
Font
```

---

# 43. Plugin Architecture

Beacon 长期应该拥有 Plugin。

Plugin 可以提供：

```text
Connection
Widget
Action
Workflow Trigger
Notification Provider
```

例如：

```text
Beacon.GitHub
Beacon.Jenkins
Beacon.Docker
Beacon.Agent
```

但：

> **插件系统不要在 MVP 阶段过早设计成复杂动态程序集生态。**

第一阶段先使用内部 Provider 接口。

---

# 44. Provider 抽象

推荐：

```text
IConnectionProvider

IWidgetProvider

IActionProvider

INotificationProvider
```

例如：

```text
GitHubProvider
JenkinsProvider
RestProvider
LocalCommandProvider
```

后续再把它们变成 Plugin。

---

# 45. MVP

Beacon 第一版不要追求 Paul’s Dashboard 的全部能力。

MVP 只做：

### Desktop

```text
Tray
Dock
Quick Panel
Settings
```

### Connection

```text
GitHub
Generic REST
```

### Widget

```text
GitHub PR
GitHub Actions
Generic Status
```

### Action

```text
Open URL
HTTP Request
Run Local Command
GitHub Workflow Dispatch
```

### Notification

```text
Windows Toast
```

### Hotkey

```text
Show / Hide
```

### Storage

```text
Local JSON
Windows Credential Storage
```

这已经足够形成完整产品。

---

# 46. MVP 第一阶段开发顺序

## Phase 1 — Windows Shell

完成：

```text
Beacon.exe
Tray
Startup
Dock
Window
Global Hotkey
```

验收：

> Beacon 启动后可以常驻 Windows，并且可以通过 Tray / Hotkey / Dock 打开。

---

## Phase 2 — Core

实现：

```text
Configuration
Event Bus
Connection
Widget
Action
Notification
```

建立稳定内部模型。

---

## Phase 3 — GitHub

实现：

```text
GitHub Authentication
Repository
PR
Actions
```

---

## Phase 4 — Action

实现：

```text
Open URL
HTTP
GitHub Workflow Dispatch
Local Command
```

---

## Phase 5 — Notification

实现：

```text
Toast
Dock Status
Error State
Read / Unread
```

---

## Phase 6 — Quick Panel

最终形成：

```text
        Desktop

══════

GitHub  CI   Actions
```

---

# 47. 第二阶段

MVP 稳定后增加：

```text
Jenkins
GitLab
Docker
Machines
Agents
Generic REST Widget
Workflow
Schedule
Webhook
```

---

# 48. 第三阶段

增加：

```text
Plugin
Workflow Engine
Agent Integration
Advanced Notifications
Charts
History
Local AI
```

---

# 49. AI 不应该成为第一优先级

Beacon 可以支持 AI，但 AI 不应该成为产品核心。

未来可以：

```text
Ask Beacon
```

例如：

> 为什么刚才的 Build 失败？

Beacon 可以收集：

```text
CI Result
Logs
Git Commit
PR
Changes
```

然后交给：

```text
Local LLM
OpenAI
Other Provider
```

但是：

> AI 是增强层，不是 Beacon 的核心架构。

---

# 50. 本地 AI

长期支持：

```text
Ollama
Local Model
OpenAI-compatible API
```

例如：

```text
Build Failed

══════

```

然后：

```text
[Run Task]
```

把任务发送到 Agent。

这会让 Beacon 从：

> Desktop Dashboard

逐渐成为：

> **Developer Control Plane**

但这属于后期能力。

---

# 52. 不应该做的事情

Beacon 必须避免产品膨胀。

不要一开始加入：

```text
完整项目管理
完整聊天系统
完整 CI Server
完整监控系统
完整日志系统
完整 AI IDE
完整 Jenkins
完整 Grafana
```

Beacon 的价值不是：

> 什么都自己实现。

而是：

> **把已有工具连接起来。**

---

# 53. 与 Paul's Dashboard 的关系

Paul's Dashboard 可以作为 Beacon 的主要产品参考。

可以吸收：

```text
Widget
Connection
Action
Workflow
Notification
Dock
Local First
```

但不要复制其：

> 大量通用 Dashboard Widget。

Beacon 应该更加垂直：

```text
Developer

══════

一键操作
```

传统 Dashboard：

> Look at it.

Beacon：

> **It watches for you.**

这是 Beacon 最重要的产品区别。

---

# 55. 产品核心闭环

整个 Beacon 最终应该围绕：

```text
Observe

══════

Success
```

这就是 Beacon 的核心闭环。

---

# 56. 第一版成功标准

Beacon 第一版完成后，一个开发者应该能够做到：

### 1

开机后 Beacon 自动启动。

### 2

桌面角落一直看到：

```text

══════

Build succeeded
```

整个过程不需要：

```text
打开浏览器
登录 GitHub
寻找 Repository
寻找 Actions
寻找 Workflow
寻找 Retry
```

这就是 MVP 是否成功的最直接判断标准。

---

# 57. 第一阶段明确不做

MVP 不做：

```text
复杂插件市场
复杂 Workflow DSL
AI Agent
Jenkins 全功能管理
Docker 全功能管理
数据库
云账号
用户系统
团队协作
云同步
复杂图表
复杂主题
```

---

# 58. 产品命名

正式名称：

# Beacon

副标题：

> **Windows Developer Status

══════

u0026 Action Center**

推荐宣传语：

> **See what needs your attention.**

或者：

> **Your development signal on Windows.**

产品概念：

> **Developer Cockpit**

这里可以保留 Cockpit 这个概念，但不作为项目名称。

---

# 59. 推荐最终产品结构

```text
                         Beacon

══════

Command
```

---

# 60. 最终架构原则

### 原则 1

**Local First**

不依赖 Beacon Cloud。

### 原则 2

**Provider First**

不要把 GitHub / Jenkins / Agent 写死在 Core。

### 原则 3

**Action First**

展示信息只是第一步，下一步操作更重要。

### 原则 4

**Progressive Disclosure**

默认小而安静，需要时展开。

### 原则 5

**Windows Native**

充分利用 Windows 原生能力。

### 原则 6

**Composable**

Connection、Widget、Action、Workflow 可以组合。

### 原则 7

**Extensible**

先 Provider，后 Plugin。

### 原则 8

**Don't Rebuild Existing Tools**

Beacon 连接工具，而不是替代工具。

---

# 61. 最终定位

Beacon 不应该被定义为：

> 一个 Windows Dashboard。

而应该定义为：

> **一个常驻 Windows 桌面的开发者状态与操作中心。**

它帮助开发者回答三个问题：

```text
现在发生了什么？

══════

我现在应该做什么？
```

最终目标：

> **让开发者不需要不断打开各种开发工具来确认状态。**

Beacon 在桌面上持续发出信号。

```text
           Beacon

══════

Developer
```

**Beacon 的核心不是 Dashboard，而是 Signal + Action。**
:::

如果后续真正开始实现，我建议**第一步不要直接让 Code Agent “开发 Beacon”**，而是先让它把这份计划转换成一份 **技术 RFC + Solution/项目目录 + MVP Issue 列表**，然后再按 Phase 1

══════

Phase 2 逐步实现。这样更不容易一开始就把 Beacon 做成一个过度复杂的 Dashboard。

---

If you want, I can:

- 继续描述本地 AI 支持的实现细节
- 展开介绍Beacon的插件机制设计与发展规划
- 列出Beacon在MVP阶段重点实现的关键功能清单