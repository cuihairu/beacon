import{_ as s,o as n,c as p,a2 as e}from"./chunks/framework.DGk0zlJw.js";const u=JSON.parse('{"title":"2. 产品定位","description":"","frontmatter":{},"headers":[],"relativePath":"zh/mission-excerpt.md","filePath":"zh/mission-excerpt.md"}'),t={name:"zh/mission-excerpt.md"};function i(l,a,o,c,d,h){return n(),p("div",null,[...a[0]||(a[0]=[e(`<p>可以。下面这份我会按<strong>可以直接交给其他 Code Agent 执行</strong>的程度来写，不只是产品想法，而是把定位、UX、架构、模块、MVP、插件机制、Windows 实现、后续演进和验收标准都明确下来。</p><p>:::writing{variant=</p><p>══════</p><p>u0026 Action Center**</p><blockquote><p><strong>See what needs your attention.</strong></p></blockquote><hr><h2 id="_1-项目概述" tabindex="-1">1. 项目概述 <a class="header-anchor" href="#_1-项目概述" aria-label="Permalink to &quot;1. 项目概述&quot;">​</a></h2><h3 id="_1-1-项目名称" tabindex="-1">1.1 项目名称 <a class="header-anchor" href="#_1-1-项目名称" aria-label="Permalink to &quot;1.1 项目名称&quot;">​</a></h3><p><strong>Beacon</strong></p><p>Beacon 的含义是：</p><blockquote><p>信标、灯塔、指示信号。</p></blockquote><p>Beacon 不定位为传统的 Web Dashboard，而定位为一个<strong>常驻 Windows 桌面的开发者状态与操作中心</strong>。</p><p>它持续收集开发环境中的重要状态，并在需要时提供快速操作。</p><p>核心理念：</p><blockquote><p><strong>Beacon watches your development environment, so you don&#39;t have to.</strong></p></blockquote><p>用户无需不断打开：</p><ul><li>GitHub</li><li>GitHub Actions</li><li>GitLab</li><li>CI/CD</li><li>Jenkins</li><li>构建机器</li><li>Agent 管理页面</li><li>Docker</li><li>服务器</li><li>各种内部工具</li></ul><p>而是通过 Beacon 在桌面上快速知道：</p><blockquote><p><strong>现在发生了什么？什么需要我的注意？我现在可以做什么？</strong></p></blockquote><hr><h1 id="_2-产品定位" tabindex="-1">2. 产品定位 <a class="header-anchor" href="#_2-产品定位" aria-label="Permalink to &quot;2. 产品定位&quot;">​</a></h1><h2 id="_2-1-不是什么" tabindex="-1">2.1 不是什么 <a class="header-anchor" href="#_2-1-不是什么" aria-label="Permalink to &quot;2.1 不是什么&quot;">​</a></h2><p>Beacon 不应该成为：</p><ul><li>传统 BI Dashboard</li><li>大型监控系统</li><li>Grafana 替代品</li><li>Jenkins 替代品</li><li>GitHub 替代品</li><li>CI/CD 平台</li><li>任务管理软件</li><li>全功能工作流平台</li></ul><p>这些系统已经负责自己的领域。</p><p>Beacon 的职责是：</p><blockquote><p><strong>把开发环境的重要状态聚合到桌面，并提供低成本的下一步操作。</strong></p></blockquote><hr><h1 id="_3-核心用户场景" tabindex="-1">3. 核心用户场景 <a class="header-anchor" href="#_3-核心用户场景" aria-label="Permalink to &quot;3. 核心用户场景&quot;">​</a></h1><p>Beacon 首先面向个人开发者和小型开发团队。</p><p>典型工作环境：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Windows PC</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Developer</span></span></code></pre></div><hr><h1 id="_4-核心产品模型" tabindex="-1">4. 核心产品模型 <a class="header-anchor" href="#_4-核心产品模型" aria-label="Permalink to &quot;4. 核心产品模型&quot;">​</a></h1><p>Beacon 的所有功能最终围绕 7 个核心概念展开。</p><h2 id="_4-1-widget" tabindex="-1">4.1 Widget <a class="header-anchor" href="#_4-1-widget" aria-label="Permalink to &quot;4.1 Widget&quot;">​</a></h2><p><strong>Widget = 信息和交互的基本单位</strong></p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub PR Widget</span></span>
<span class="line"><span>CI Status Widget</span></span>
<span class="line"><span>Build Widget</span></span>
<span class="line"><span>Agent Widget</span></span>
<span class="line"><span>Machine Widget</span></span>
<span class="line"><span>Workflow Widget</span></span>
<span class="line"><span>Notification Widget</span></span></code></pre></div><p>Widget 负责：</p><ul><li>展示数据</li><li>状态</li><li>用户交互</li><li>快捷操作</li></ul><hr><h1 id="_5-connection" tabindex="-1">5. Connection <a class="header-anchor" href="#_5-connection" aria-label="Permalink to &quot;5. Connection&quot;">​</a></h1><p><strong>Connection = 外部数据源和服务连接</strong></p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub</span></span>
<span class="line"><span>GitLab</span></span>
<span class="line"><span>Jenkins</span></span>
<span class="line"><span>Generic REST API</span></span>
<span class="line"><span>Docker</span></span>
<span class="line"><span>Beacon Server</span></span>
<span class="line"><span>Custom API</span></span></code></pre></div><p>Connection 负责：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Authentication</span></span>
<span class="line"><span>API Endpoint</span></span>
<span class="line"><span>Credentials</span></span>
<span class="line"><span>Request</span></span>
<span class="line"><span>Response</span></span>
<span class="line"><span>Refresh</span></span>
<span class="line"><span>Error</span></span></code></pre></div><p>一个 Connection 可以被多个 Widget 使用。</p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub Connection</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Workflow Action</span></span></code></pre></div><p>避免每个 Widget 都重复配置 GitHub Token。</p><hr><h1 id="_6-action" tabindex="-1">6. Action <a class="header-anchor" href="#_6-action" aria-label="Permalink to &quot;6. Action&quot;">​</a></h1><p><strong>Action = 用户可以立即执行的操作</strong></p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build</span></span>
<span class="line"><span>Test</span></span>
<span class="line"><span>Deploy</span></span>
<span class="line"><span>Restart</span></span>
<span class="line"><span>Run Workflow</span></span>
<span class="line"><span>Cancel Workflow</span></span>
<span class="line"><span>Open PR</span></span>
<span class="line"><span>Open Repository</span></span>
<span class="line"><span>Open Logs</span></span>
<span class="line"><span>Run Agent</span></span></code></pre></div><p>Action 可以来自：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>HTTP API</span></span>
<span class="line"><span>Webhook</span></span>
<span class="line"><span>Local Command</span></span>
<span class="line"><span>PowerShell</span></span>
<span class="line"><span>Shell Script</span></span>
<span class="line"><span>Beacon Workflow</span></span>
<span class="line"><span>External URL</span></span></code></pre></div><p>核心目标：</p><blockquote><p>从“看到问题”到“执行操作”只需要一次点击。</p></blockquote><hr><h1 id="_7-workflow" tabindex="-1">7. Workflow <a class="header-anchor" href="#_7-workflow" aria-label="Permalink to &quot;7. Workflow&quot;">​</a></h1><p><strong>Workflow = 多步骤自动化任务</strong></p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build Client</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Notify</span></span></code></pre></div><p>Workflow 是 Beacon 的高级能力。</p><p>MVP 不应该一开始实现复杂 Workflow Engine。</p><p>第一阶段只支持简单 Action。</p><hr><h1 id="_8-notification" tabindex="-1">8. Notification <a class="header-anchor" href="#_8-notification" aria-label="Permalink to &quot;8. Notification&quot;">​</a></h1><p><strong>Notification = 主动提醒</strong></p><p>Beacon 应该区分：</p><h3 id="信息" tabindex="-1">信息 <a class="header-anchor" href="#信息" aria-label="Permalink to &quot;信息&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Build failed</span></span></code></pre></div><p>Notification 可以来自：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub</span></span>
<span class="line"><span>CI/CD</span></span>
<span class="line"><span>Machine</span></span>
<span class="line"><span>Workflow</span></span>
<span class="line"><span>Beacon Server</span></span>
<span class="line"><span>Widget Threshold</span></span></code></pre></div><p>最终通过 Windows Notification 展示。</p><hr><h1 id="_9-dock" tabindex="-1">9. Dock <a class="header-anchor" href="#_9-dock" aria-label="Permalink to &quot;9. Dock&quot;">​</a></h1><p>Dock 是 Beacon 最有特色的桌面交互之一。</p><p>它不是传统意义上的 Windows Taskbar Dock。</p><p>而是：</p><blockquote><p>一个可以常驻桌面的轻量状态入口。</p></blockquote><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Exit</span></span></code></pre></div><hr><h1 id="_11-三层交互模型" tabindex="-1">11. 三层交互模型 <a class="header-anchor" href="#_11-三层交互模型" aria-label="Permalink to &quot;11. 三层交互模型&quot;">​</a></h1><p>Beacon 不应该一打开就是一个巨大 Dashboard。</p><p>采用 Progressive Disclosure。</p><h2 id="level-1-status-capsule" tabindex="-1">Level 1：Status Capsule <a class="header-anchor" href="#level-1-status-capsule" aria-label="Permalink to &quot;Level 1：Status Capsule&quot;">​</a></h2><p>桌面常驻。</p><p>只显示：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>[Build] [Test] [Deploy]</span></span></code></pre></div><hr><h2 id="level-3-detail-window" tabindex="-1">Level 3：Detail Window <a class="header-anchor" href="#level-3-detail-window" aria-label="Permalink to &quot;Level 3：Detail Window&quot;">​</a></h2><p>需要深入查看时：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build #1024</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Status: Failed</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Repository:</span></span>
<span class="line"><span>cuihairu/example</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Branch:</span></span>
<span class="line"><span>main</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Duration:</span></span>
<span class="line"><span>12m 31s</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Stage:</span></span>
<span class="line"><span>Compile</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Error:</span></span>
<span class="line"><span>...</span></span>
<span class="line"><span></span></span>
<span class="line"><span>[Open GitHub]</span></span>
<span class="line"><span>[Retry]</span></span>
<span class="line"><span>[Cancel]</span></span></code></pre></div><p>这样可以保证 Beacon：</p><blockquote><p>平时不打扰，出问题时迅速进入细节。</p></blockquote><hr><h1 id="_12-ui-设计原则" tabindex="-1">12. UI 设计原则 <a class="header-anchor" href="#_12-ui-设计原则" aria-label="Permalink to &quot;12. UI 设计原则&quot;">​</a></h1><h2 id="_12-1-小" tabindex="-1">12.1 小 <a class="header-anchor" href="#_12-1-小" aria-label="Permalink to &quot;12.1 小&quot;">​</a></h2><p>Beacon 默认不能占据大量桌面空间。</p><h2 id="_12-2-快" tabindex="-1">12.2 快 <a class="header-anchor" href="#_12-2-快" aria-label="Permalink to &quot;12.2 快&quot;">​</a></h2><p>打开面板应该接近瞬时。</p><h2 id="_12-3-信息密度高" tabindex="-1">12.3 信息密度高 <a class="header-anchor" href="#_12-3-信息密度高" aria-label="Permalink to &quot;12.3 信息密度高&quot;">​</a></h2><p>优先显示：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Status</span></span>
<span class="line"><span>Severity</span></span>
<span class="line"><span>Progress</span></span>
<span class="line"><span>Next Action</span></span></code></pre></div><p>而不是大量描述文字。</p><h2 id="_12-4-操作优先" tabindex="-1">12.4 操作优先 <a class="header-anchor" href="#_12-4-操作优先" aria-label="Permalink to &quot;12.4 操作优先&quot;">​</a></h2><p>每一个重要状态尽可能存在：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Open</span></span>
<span class="line"><span>Retry</span></span>
<span class="line"><span>Run</span></span>
<span class="line"><span>Cancel</span></span>
<span class="line"><span>Deploy</span></span>
<span class="line"><span>Restart</span></span></code></pre></div><p>等下一步动作。</p><hr><h1 id="_13-windows-平台定位" tabindex="-1">13. Windows 平台定位 <a class="header-anchor" href="#_13-windows-平台定位" aria-label="Permalink to &quot;13. Windows 平台定位&quot;">​</a></h1><p>Beacon <strong>只支持 Windows</strong>。</p><p>因此不需要：</p><ul><li>macOS</li><li>Linux</li><li>Tauri 跨平台抽象</li><li>Electron</li></ul><p>优先使用 Windows 原生技术。</p><hr><h1 id="_14-技术栈" tabindex="-1">14. 技术栈 <a class="header-anchor" href="#_14-技术栈" aria-label="Permalink to &quot;14. 技术栈&quot;">​</a></h1><p>推荐：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Language:</span></span>
<span class="line"><span>C#</span></span>
<span class="line"><span></span></span>
<span class="line"><span>UI:</span></span>
<span class="line"><span>WinUI 3</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Runtime:</span></span>
<span class="line"><span>.NET</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Windows:</span></span>
<span class="line"><span>Windows App SDK</span></span></code></pre></div><p>辅助：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>CommunityToolkit.WinUI</span></span></code></pre></div><p>Windows API 用于：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>System Tray</span></span>
<span class="line"><span>Global Hotkey</span></span>
<span class="line"><span>Window Position</span></span>
<span class="line"><span>Always On Top</span></span>
<span class="line"><span>Startup</span></span>
<span class="line"><span>Multi Monitor</span></span>
<span class="line"><span>Toast Notification</span></span>
<span class="line"><span>Credential Storage</span></span></code></pre></div><hr><h1 id="_15-为什么不用-tauri" tabindex="-1">15. 为什么不用 Tauri <a class="header-anchor" href="#_15-为什么不用-tauri" aria-label="Permalink to &quot;15. 为什么不用 Tauri&quot;">​</a></h1><p>Beacon 是明确的 Windows-only 产品。</p><p>Tauri 的主要价值是：</p><blockquote><p>跨平台桌面应用。</p></blockquote><p>如果没有跨平台需求，它会引入不必要的：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Web UI</span></span>
<span class="line"><span>Rust Bridge</span></span>
<span class="line"><span>WebView</span></span>
<span class="line"><span>IPC</span></span></code></pre></div><p>Beacon 更适合：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>C#</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Read</span></span></code></pre></div><hr><h1 id="_18-配置系统" tabindex="-1">18. 配置系统 <a class="header-anchor" href="#_18-配置系统" aria-label="Permalink to &quot;18. 配置系统&quot;">​</a></h1><p>Beacon 应该坚持：</p><blockquote><p><strong>Local First</strong></p></blockquote><p>默认不要求用户注册 Beacon Cloud。</p><p>配置保存在本机。</p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>%AppData%</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span></code></pre></div><p>可以包含：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>config.json</span></span>
<span class="line"><span>widgets.json</span></span>
<span class="line"><span>connections.json</span></span>
<span class="line"><span>workflows.json</span></span></code></pre></div><p>但是：</p><blockquote><p><strong>Secrets 不允许直接放进普通 JSON。</strong></p></blockquote><hr><h1 id="_19-secret-storage" tabindex="-1">19. Secret Storage <a class="header-anchor" href="#_19-secret-storage" aria-label="Permalink to &quot;19. Secret Storage&quot;">​</a></h1><p>Windows 使用：</p><blockquote><p>Windows Credential Manager / DPAPI</p></blockquote><p>保存：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub Token</span></span>
<span class="line"><span>API Key</span></span>
<span class="line"><span>Password</span></span>
<span class="line"><span>Webhook Secret</span></span></code></pre></div><p>普通配置只保存：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>credentialId</span></span></code></pre></div><p>而不是：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>token=ghp_xxxxxxxxx</span></span></code></pre></div><hr><h1 id="_20-导入导出" tabindex="-1">20. 导入导出 <a class="header-anchor" href="#_20-导入导出" aria-label="Permalink to &quot;20. 导入导出&quot;">​</a></h1><p>用户应该可以：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Export Configuration</span></span>
<span class="line"><span>Import Configuration</span></span></code></pre></div><p>导出：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Widgets</span></span>
<span class="line"><span>Connections Metadata</span></span>
<span class="line"><span>Actions</span></span>
<span class="line"><span>Workflows</span></span>
<span class="line"><span>Appearance</span></span></code></pre></div><p>默认：</p><blockquote><p>不导出 Secrets。</p></blockquote><p>导入后提示用户重新配置 Credential。</p><hr><h1 id="_21-github-集成" tabindex="-1">21. GitHub 集成 <a class="header-anchor" href="#_21-github-集成" aria-label="Permalink to &quot;21. GitHub 集成&quot;">​</a></h1><p>第一阶段优先实现 GitHub。</p><p>GitHub Connection：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Running</span></span></code></pre></div><p>提供：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Run</span></span>
<span class="line"><span>Cancel</span></span>
<span class="line"><span>Retry</span></span>
<span class="line"><span>Open</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>[Run Build]</span></span></code></pre></div><p>实际上调用：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub Actions workflow_dispatch</span></span></code></pre></div><hr><h1 id="_23-ci-cd-抽象" tabindex="-1">23. CI/CD 抽象 <a class="header-anchor" href="#_23-ci-cd-抽象" aria-label="Permalink to &quot;23. CI/CD 抽象&quot;">​</a></h1><p>不要把 CI/CD 逻辑写死成 GitHub Actions。</p><p>抽象：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>CI Provider</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub Actions</span></span>
<span class="line"><span>Jenkins</span></span>
<span class="line"><span>GitLab CI</span></span>
<span class="line"><span>Generic REST</span></span></code></pre></div><p>统一状态：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Queued</span></span>
<span class="line"><span>Running</span></span>
<span class="line"><span>Success</span></span>
<span class="line"><span>Failed</span></span>
<span class="line"><span>Cancelled</span></span>
<span class="line"><span>Skipped</span></span>
<span class="line"><span>Unknown</span></span></code></pre></div><p>这样 UI 不需要关心底层 CI 系统。</p><hr><h1 id="_24-machine" tabindex="-1">24. Machine <a class="header-anchor" href="#_24-machine" aria-label="Permalink to &quot;24. Machine&quot;">​</a></h1><p>未来 Beacon 可以管理：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Local Machine</span></span>
<span class="line"><span>Build PC</span></span>
<span class="line"><span>Test PC</span></span>
<span class="line"><span>Agent PC</span></span>
<span class="line"><span>Server</span></span></code></pre></div><p>Machine Widget：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build-PC-01</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Running</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Task:</span></span>
<span class="line"><span>Build Apollo</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Elapsed:</span></span>
<span class="line"><span>08:21</span></span>
<span class="line"><span></span></span>
<span class="line"><span>[Open]</span></span>
<span class="line"><span>[Stop]</span></span></code></pre></div><p>这一层未来可以连接你的中央 Agent / Worker 系统。</p><p>但：</p><blockquote><p><strong>不要把 Agent Server 写死进 Beacon。</strong></p></blockquote><p>应该作为 Connection + Provider。</p><hr><h1 id="_26-generic-rest" tabindex="-1">26. Generic REST <a class="header-anchor" href="#_26-generic-rest" aria-label="Permalink to &quot;26. Generic REST&quot;">​</a></h1><p>Beacon 必须支持 Generic REST。</p><p>用户可以配置：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GET https://example.com/api/status</span></span></code></pre></div><p>或者：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>POST https://example.com/build</span></span></code></pre></div><p>然后将结果映射到 Widget。</p><p>这是保证 Beacon 可扩展性的关键。</p><hr><h1 id="_27-action-button" tabindex="-1">27. Action Button <a class="header-anchor" href="#_27-action-button" aria-label="Permalink to &quot;27. Action Button&quot;">​</a></h1><p>所有重要 Widget 都可以配置 Action。</p><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build Widget</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Status:</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Build Client</span></span></code></pre></div><hr><h1 id="_31-notification-engine" tabindex="-1">31. Notification Engine <a class="header-anchor" href="#_31-notification-engine" aria-label="Permalink to &quot;31. Notification Engine&quot;">​</a></h1><p>Notification 不是简单 Toast。</p><p>Beacon 应有：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Notification Rule</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>IF</span></span>
<span class="line"><span>CI status == Failed</span></span>
<span class="line"><span></span></span>
<span class="line"><span>THEN</span></span>
<span class="line"><span>Toast</span></span>
<span class="line"><span>Sound</span></span>
<span class="line"><span>Dock turns red</span></span></code></pre></div><p>或者：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>IF</span></span>
<span class="line"><span>Build duration &gt; 15 min</span></span>
<span class="line"><span></span></span>
<span class="line"><span>THEN</span></span>
<span class="line"><span>Notification</span></span></code></pre></div><hr><h1 id="_32-状态聚合" tabindex="-1">32. 状态聚合 <a class="header-anchor" href="#_32-状态聚合" aria-label="Permalink to &quot;32. 状态聚合&quot;">​</a></h1><p>Beacon 最重要的能力之一：</p><blockquote><p>将多个系统压缩成一个简单的总体状态。</p></blockquote><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Beacon</span></span></code></pre></div><p>Dock 只需要告诉用户：</p><blockquote><p>有问题。</p></blockquote><p>用户点击后再查看：</p><blockquote><p>什么出了问题。</p></blockquote><hr><h1 id="_33-severity" tabindex="-1">33. Severity <a class="header-anchor" href="#_33-severity" aria-label="Permalink to &quot;33. Severity&quot;">​</a></h1><p>统一：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Info</span></span>
<span class="line"><span>Success</span></span>
<span class="line"><span>Warning</span></span>
<span class="line"><span>Error</span></span>
<span class="line"><span>Critical</span></span></code></pre></div><p>对应：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span></span></span>
<span class="line"><span>══════</span></span></code></pre></div><p>不要让每个 Provider 自己定义完全不同的状态。</p><hr><h1 id="_34-refresh-strategy" tabindex="-1">34. Refresh Strategy <a class="header-anchor" href="#_34-refresh-strategy" aria-label="Permalink to &quot;34. Refresh Strategy&quot;">​</a></h1><p>不要所有数据每秒刷新。</p><p>不同数据采用不同策略：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub PR       30~120s</span></span>
<span class="line"><span>CI              10~30s</span></span>
<span class="line"><span>Machine         5~30s</span></span>
<span class="line"><span>Agent           5~15s</span></span>
<span class="line"><span>Workflow        Event / 10s</span></span>
<span class="line"><span>Static          Manual</span></span></code></pre></div><p>支持：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Polling</span></span>
<span class="line"><span>Event</span></span>
<span class="line"><span>Manual Refresh</span></span></code></pre></div><hr><h1 id="_35-cache" tabindex="-1">35. Cache <a class="header-anchor" href="#_35-cache" aria-label="Permalink to &quot;35. Cache&quot;">​</a></h1><p>每个 Connection / Widget 应该支持缓存。</p><p>目标：</p><blockquote><p>Beacon 打开时不要因为网络请求而空白等待。</p></blockquote><p>启动：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Load Cache</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Unable to refresh</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Last successful update:</span></span>
<span class="line"><span>2 minutes ago</span></span>
<span class="line"><span></span></span>
<span class="line"><span>[Retry]</span></span></code></pre></div><p>而不是：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Exception...</span></span></code></pre></div><hr><h1 id="_37-offline-mode" tabindex="-1">37. Offline Mode <a class="header-anchor" href="#_37-offline-mode" aria-label="Permalink to &quot;37. Offline Mode&quot;">​</a></h1><p>Beacon 应该能够离线运行。</p><p>离线时：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Last known state</span></span></code></pre></div><p>仍然可见。</p><p>并明确：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Offline</span></span>
<span class="line"><span>Last update:</span></span>
<span class="line"><span>10:31</span></span></code></pre></div><hr><h1 id="_38-hotkeys" tabindex="-1">38. Hotkeys <a class="header-anchor" href="#_38-hotkeys" aria-label="Permalink to &quot;38. Hotkeys&quot;">​</a></h1><p>至少支持：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Ctrl + Alt + B</span></span></code></pre></div><p>打开/隐藏 Beacon。</p><p>未来：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Ctrl + Alt + 1</span></span>
<span class="line"><span>Build</span></span>
<span class="line"><span>Ctrl + Alt + 2</span></span>
<span class="line"><span>Test</span></span>
<span class="line"><span>Ctrl + Alt + 3</span></span>
<span class="line"><span>Deploy</span></span></code></pre></div><p>Hotkey 必须允许用户修改。</p><hr><h1 id="_39-startup" tabindex="-1">39. Startup <a class="header-anchor" href="#_39-startup" aria-label="Permalink to &quot;39. Startup&quot;">​</a></h1><p>支持：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Start Beacon with Windows</span></span></code></pre></div><p>启动后：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Tray</span></span>
<span class="line"><span>+</span></span>
<span class="line"><span>Dock</span></span></code></pre></div><p>不应该默认打开大型窗口。</p><hr><h1 id="_40-multi-monitor" tabindex="-1">40. Multi Monitor <a class="header-anchor" href="#_40-multi-monitor" aria-label="Permalink to &quot;40. Multi Monitor&quot;">​</a></h1><p>Beacon 必须考虑多显示器。</p><p>Dock 应该支持：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Primary Monitor</span></span>
<span class="line"><span>Current Monitor</span></span>
<span class="line"><span>Specific Monitor</span></span></code></pre></div><p>用户可以选择：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Bottom Right</span></span>
<span class="line"><span>Top Right</span></span>
<span class="line"><span>Bottom Left</span></span></code></pre></div><hr><h1 id="_41-always-on-top" tabindex="-1">41. Always on Top <a class="header-anchor" href="#_41-always-on-top" aria-label="Permalink to &quot;41. Always on Top&quot;">​</a></h1><p>Dock 默认：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Always on Top</span></span></code></pre></div><p>但不能遮挡用户工作。</p><p>因此需要：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Opacity</span></span>
<span class="line"><span>Auto Hide</span></span>
<span class="line"><span>Compact Mode</span></span></code></pre></div><hr><h1 id="_42-appearance" tabindex="-1">42. Appearance <a class="header-anchor" href="#_42-appearance" aria-label="Permalink to &quot;42. Appearance&quot;">​</a></h1><p>Beacon 不需要一开始做大量主题系统。</p><p>MVP：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Light</span></span>
<span class="line"><span>Dark</span></span>
<span class="line"><span>System</span></span></code></pre></div><p>以及：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Opacity</span></span>
<span class="line"><span>Compact</span></span>
<span class="line"><span>Normal</span></span>
<span class="line"><span>Large</span></span></code></pre></div><p>后续再增加：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Accent Color</span></span>
<span class="line"><span>Widget Appearance</span></span>
<span class="line"><span>Background</span></span>
<span class="line"><span>Font</span></span></code></pre></div><hr><h1 id="_43-plugin-architecture" tabindex="-1">43. Plugin Architecture <a class="header-anchor" href="#_43-plugin-architecture" aria-label="Permalink to &quot;43. Plugin Architecture&quot;">​</a></h1><p>Beacon 长期应该拥有 Plugin。</p><p>Plugin 可以提供：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Connection</span></span>
<span class="line"><span>Widget</span></span>
<span class="line"><span>Action</span></span>
<span class="line"><span>Workflow Trigger</span></span>
<span class="line"><span>Notification Provider</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.GitHub</span></span>
<span class="line"><span>Beacon.Jenkins</span></span>
<span class="line"><span>Beacon.Docker</span></span>
<span class="line"><span>Beacon.Agent</span></span></code></pre></div><p>但：</p><blockquote><p><strong>插件系统不要在 MVP 阶段过早设计成复杂动态程序集生态。</strong></p></blockquote><p>第一阶段先使用内部 Provider 接口。</p><hr><h1 id="_44-provider-抽象" tabindex="-1">44. Provider 抽象 <a class="header-anchor" href="#_44-provider-抽象" aria-label="Permalink to &quot;44. Provider 抽象&quot;">​</a></h1><p>推荐：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>IConnectionProvider</span></span>
<span class="line"><span></span></span>
<span class="line"><span>IWidgetProvider</span></span>
<span class="line"><span></span></span>
<span class="line"><span>IActionProvider</span></span>
<span class="line"><span></span></span>
<span class="line"><span>INotificationProvider</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHubProvider</span></span>
<span class="line"><span>JenkinsProvider</span></span>
<span class="line"><span>RestProvider</span></span>
<span class="line"><span>LocalCommandProvider</span></span></code></pre></div><p>后续再把它们变成 Plugin。</p><hr><h1 id="_45-mvp" tabindex="-1">45. MVP <a class="header-anchor" href="#_45-mvp" aria-label="Permalink to &quot;45. MVP&quot;">​</a></h1><p>Beacon 第一版不要追求 Paul’s Dashboard 的全部能力。</p><p>MVP 只做：</p><h3 id="desktop" tabindex="-1">Desktop <a class="header-anchor" href="#desktop" aria-label="Permalink to &quot;Desktop&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Tray</span></span>
<span class="line"><span>Dock</span></span>
<span class="line"><span>Quick Panel</span></span>
<span class="line"><span>Settings</span></span></code></pre></div><h3 id="connection" tabindex="-1">Connection <a class="header-anchor" href="#connection" aria-label="Permalink to &quot;Connection&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub</span></span>
<span class="line"><span>Generic REST</span></span></code></pre></div><h3 id="widget" tabindex="-1">Widget <a class="header-anchor" href="#widget" aria-label="Permalink to &quot;Widget&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub PR</span></span>
<span class="line"><span>GitHub Actions</span></span>
<span class="line"><span>Generic Status</span></span></code></pre></div><h3 id="action" tabindex="-1">Action <a class="header-anchor" href="#action" aria-label="Permalink to &quot;Action&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Open URL</span></span>
<span class="line"><span>HTTP Request</span></span>
<span class="line"><span>Run Local Command</span></span>
<span class="line"><span>GitHub Workflow Dispatch</span></span></code></pre></div><h3 id="notification" tabindex="-1">Notification <a class="header-anchor" href="#notification" aria-label="Permalink to &quot;Notification&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Windows Toast</span></span></code></pre></div><h3 id="hotkey" tabindex="-1">Hotkey <a class="header-anchor" href="#hotkey" aria-label="Permalink to &quot;Hotkey&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Show / Hide</span></span></code></pre></div><h3 id="storage" tabindex="-1">Storage <a class="header-anchor" href="#storage" aria-label="Permalink to &quot;Storage&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Local JSON</span></span>
<span class="line"><span>Windows Credential Storage</span></span></code></pre></div><p>这已经足够形成完整产品。</p><hr><h1 id="_46-mvp-第一阶段开发顺序" tabindex="-1">46. MVP 第一阶段开发顺序 <a class="header-anchor" href="#_46-mvp-第一阶段开发顺序" aria-label="Permalink to &quot;46. MVP 第一阶段开发顺序&quot;">​</a></h1><h2 id="phase-1-—-windows-shell" tabindex="-1">Phase 1 — Windows Shell <a class="header-anchor" href="#phase-1-—-windows-shell" aria-label="Permalink to &quot;Phase 1 — Windows Shell&quot;">​</a></h2><p>完成：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.exe</span></span>
<span class="line"><span>Tray</span></span>
<span class="line"><span>Startup</span></span>
<span class="line"><span>Dock</span></span>
<span class="line"><span>Window</span></span>
<span class="line"><span>Global Hotkey</span></span></code></pre></div><p>验收：</p><blockquote><p>Beacon 启动后可以常驻 Windows，并且可以通过 Tray / Hotkey / Dock 打开。</p></blockquote><hr><h2 id="phase-2-—-core" tabindex="-1">Phase 2 — Core <a class="header-anchor" href="#phase-2-—-core" aria-label="Permalink to &quot;Phase 2 — Core&quot;">​</a></h2><p>实现：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Configuration</span></span>
<span class="line"><span>Event Bus</span></span>
<span class="line"><span>Connection</span></span>
<span class="line"><span>Widget</span></span>
<span class="line"><span>Action</span></span>
<span class="line"><span>Notification</span></span></code></pre></div><p>建立稳定内部模型。</p><hr><h2 id="phase-3-—-github" tabindex="-1">Phase 3 — GitHub <a class="header-anchor" href="#phase-3-—-github" aria-label="Permalink to &quot;Phase 3 — GitHub&quot;">​</a></h2><p>实现：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>GitHub Authentication</span></span>
<span class="line"><span>Repository</span></span>
<span class="line"><span>PR</span></span>
<span class="line"><span>Actions</span></span></code></pre></div><hr><h2 id="phase-4-—-action" tabindex="-1">Phase 4 — Action <a class="header-anchor" href="#phase-4-—-action" aria-label="Permalink to &quot;Phase 4 — Action&quot;">​</a></h2><p>实现：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Open URL</span></span>
<span class="line"><span>HTTP</span></span>
<span class="line"><span>GitHub Workflow Dispatch</span></span>
<span class="line"><span>Local Command</span></span></code></pre></div><hr><h2 id="phase-5-—-notification" tabindex="-1">Phase 5 — Notification <a class="header-anchor" href="#phase-5-—-notification" aria-label="Permalink to &quot;Phase 5 — Notification&quot;">​</a></h2><p>实现：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Toast</span></span>
<span class="line"><span>Dock Status</span></span>
<span class="line"><span>Error State</span></span>
<span class="line"><span>Read / Unread</span></span></code></pre></div><hr><h2 id="phase-6-—-quick-panel" tabindex="-1">Phase 6 — Quick Panel <a class="header-anchor" href="#phase-6-—-quick-panel" aria-label="Permalink to &quot;Phase 6 — Quick Panel&quot;">​</a></h2><p>最终形成：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>        Desktop</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>GitHub  CI   Actions</span></span></code></pre></div><hr><h1 id="_47-第二阶段" tabindex="-1">47. 第二阶段 <a class="header-anchor" href="#_47-第二阶段" aria-label="Permalink to &quot;47. 第二阶段&quot;">​</a></h1><p>MVP 稳定后增加：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Jenkins</span></span>
<span class="line"><span>GitLab</span></span>
<span class="line"><span>Docker</span></span>
<span class="line"><span>Machines</span></span>
<span class="line"><span>Agents</span></span>
<span class="line"><span>Generic REST Widget</span></span>
<span class="line"><span>Workflow</span></span>
<span class="line"><span>Schedule</span></span>
<span class="line"><span>Webhook</span></span></code></pre></div><hr><h1 id="_48-第三阶段" tabindex="-1">48. 第三阶段 <a class="header-anchor" href="#_48-第三阶段" aria-label="Permalink to &quot;48. 第三阶段&quot;">​</a></h1><p>增加：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Plugin</span></span>
<span class="line"><span>Workflow Engine</span></span>
<span class="line"><span>Agent Integration</span></span>
<span class="line"><span>Advanced Notifications</span></span>
<span class="line"><span>Charts</span></span>
<span class="line"><span>History</span></span>
<span class="line"><span>Local AI</span></span></code></pre></div><hr><h1 id="_49-ai-不应该成为第一优先级" tabindex="-1">49. AI 不应该成为第一优先级 <a class="header-anchor" href="#_49-ai-不应该成为第一优先级" aria-label="Permalink to &quot;49. AI 不应该成为第一优先级&quot;">​</a></h1><p>Beacon 可以支持 AI，但 AI 不应该成为产品核心。</p><p>未来可以：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Ask Beacon</span></span></code></pre></div><p>例如：</p><blockquote><p>为什么刚才的 Build 失败？</p></blockquote><p>Beacon 可以收集：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>CI Result</span></span>
<span class="line"><span>Logs</span></span>
<span class="line"><span>Git Commit</span></span>
<span class="line"><span>PR</span></span>
<span class="line"><span>Changes</span></span></code></pre></div><p>然后交给：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Local LLM</span></span>
<span class="line"><span>OpenAI</span></span>
<span class="line"><span>Other Provider</span></span></code></pre></div><p>但是：</p><blockquote><p>AI 是增强层，不是 Beacon 的核心架构。</p></blockquote><hr><h1 id="_50-本地-ai" tabindex="-1">50. 本地 AI <a class="header-anchor" href="#_50-本地-ai" aria-label="Permalink to &quot;50. 本地 AI&quot;">​</a></h1><p>长期支持：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Ollama</span></span>
<span class="line"><span>Local Model</span></span>
<span class="line"><span>OpenAI-compatible API</span></span></code></pre></div><p>例如：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Build Failed</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span></code></pre></div><p>然后：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>[Run Task]</span></span></code></pre></div><p>把任务发送到 Agent。</p><p>这会让 Beacon 从：</p><blockquote><p>Desktop Dashboard</p></blockquote><p>逐渐成为：</p><blockquote><p><strong>Developer Control Plane</strong></p></blockquote><p>但这属于后期能力。</p><hr><h1 id="_52-不应该做的事情" tabindex="-1">52. 不应该做的事情 <a class="header-anchor" href="#_52-不应该做的事情" aria-label="Permalink to &quot;52. 不应该做的事情&quot;">​</a></h1><p>Beacon 必须避免产品膨胀。</p><p>不要一开始加入：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>完整项目管理</span></span>
<span class="line"><span>完整聊天系统</span></span>
<span class="line"><span>完整 CI Server</span></span>
<span class="line"><span>完整监控系统</span></span>
<span class="line"><span>完整日志系统</span></span>
<span class="line"><span>完整 AI IDE</span></span>
<span class="line"><span>完整 Jenkins</span></span>
<span class="line"><span>完整 Grafana</span></span></code></pre></div><p>Beacon 的价值不是：</p><blockquote><p>什么都自己实现。</p></blockquote><p>而是：</p><blockquote><p><strong>把已有工具连接起来。</strong></p></blockquote><hr><h1 id="_53-与-paul-s-dashboard-的关系" tabindex="-1">53. 与 Paul&#39;s Dashboard 的关系 <a class="header-anchor" href="#_53-与-paul-s-dashboard-的关系" aria-label="Permalink to &quot;53. 与 Paul&#39;s Dashboard 的关系&quot;">​</a></h1><p>Paul&#39;s Dashboard 可以作为 Beacon 的主要产品参考。</p><p>可以吸收：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Widget</span></span>
<span class="line"><span>Connection</span></span>
<span class="line"><span>Action</span></span>
<span class="line"><span>Workflow</span></span>
<span class="line"><span>Notification</span></span>
<span class="line"><span>Dock</span></span>
<span class="line"><span>Local First</span></span></code></pre></div><p>但不要复制其：</p><blockquote><p>大量通用 Dashboard Widget。</p></blockquote><p>Beacon 应该更加垂直：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Developer</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>一键操作</span></span></code></pre></div><p>传统 Dashboard：</p><blockquote><p>Look at it.</p></blockquote><p>Beacon：</p><blockquote><p><strong>It watches for you.</strong></p></blockquote><p>这是 Beacon 最重要的产品区别。</p><hr><h1 id="_55-产品核心闭环" tabindex="-1">55. 产品核心闭环 <a class="header-anchor" href="#_55-产品核心闭环" aria-label="Permalink to &quot;55. 产品核心闭环&quot;">​</a></h1><p>整个 Beacon 最终应该围绕：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Observe</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Success</span></span></code></pre></div><p>这就是 Beacon 的核心闭环。</p><hr><h1 id="_56-第一版成功标准" tabindex="-1">56. 第一版成功标准 <a class="header-anchor" href="#_56-第一版成功标准" aria-label="Permalink to &quot;56. 第一版成功标准&quot;">​</a></h1><p>Beacon 第一版完成后，一个开发者应该能够做到：</p><h3 id="_1" tabindex="-1">1 <a class="header-anchor" href="#_1" aria-label="Permalink to &quot;1&quot;">​</a></h3><p>开机后 Beacon 自动启动。</p><h3 id="_2" tabindex="-1">2 <a class="header-anchor" href="#_2" aria-label="Permalink to &quot;2&quot;">​</a></h3><p>桌面角落一直看到：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Build succeeded</span></span></code></pre></div><p>整个过程不需要：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>打开浏览器</span></span>
<span class="line"><span>登录 GitHub</span></span>
<span class="line"><span>寻找 Repository</span></span>
<span class="line"><span>寻找 Actions</span></span>
<span class="line"><span>寻找 Workflow</span></span>
<span class="line"><span>寻找 Retry</span></span></code></pre></div><p>这就是 MVP 是否成功的最直接判断标准。</p><hr><h1 id="_57-第一阶段明确不做" tabindex="-1">57. 第一阶段明确不做 <a class="header-anchor" href="#_57-第一阶段明确不做" aria-label="Permalink to &quot;57. 第一阶段明确不做&quot;">​</a></h1><p>MVP 不做：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>复杂插件市场</span></span>
<span class="line"><span>复杂 Workflow DSL</span></span>
<span class="line"><span>AI Agent</span></span>
<span class="line"><span>Jenkins 全功能管理</span></span>
<span class="line"><span>Docker 全功能管理</span></span>
<span class="line"><span>数据库</span></span>
<span class="line"><span>云账号</span></span>
<span class="line"><span>用户系统</span></span>
<span class="line"><span>团队协作</span></span>
<span class="line"><span>云同步</span></span>
<span class="line"><span>复杂图表</span></span>
<span class="line"><span>复杂主题</span></span></code></pre></div><hr><h1 id="_58-产品命名" tabindex="-1">58. 产品命名 <a class="header-anchor" href="#_58-产品命名" aria-label="Permalink to &quot;58. 产品命名&quot;">​</a></h1><p>正式名称：</p><h1 id="beacon" tabindex="-1">Beacon <a class="header-anchor" href="#beacon" aria-label="Permalink to &quot;Beacon&quot;">​</a></h1><p>副标题：</p><blockquote><p>**Windows Developer Status</p></blockquote><p>══════</p><p>u0026 Action Center**</p><p>推荐宣传语：</p><blockquote><p><strong>See what needs your attention.</strong></p></blockquote><p>或者：</p><blockquote><p><strong>Your development signal on Windows.</strong></p></blockquote><p>产品概念：</p><blockquote><p><strong>Developer Cockpit</strong></p></blockquote><p>这里可以保留 Cockpit 这个概念，但不作为项目名称。</p><hr><h1 id="_59-推荐最终产品结构" tabindex="-1">59. 推荐最终产品结构 <a class="header-anchor" href="#_59-推荐最终产品结构" aria-label="Permalink to &quot;59. 推荐最终产品结构&quot;">​</a></h1><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>                         Beacon</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Command</span></span></code></pre></div><hr><h1 id="_60-最终架构原则" tabindex="-1">60. 最终架构原则 <a class="header-anchor" href="#_60-最终架构原则" aria-label="Permalink to &quot;60. 最终架构原则&quot;">​</a></h1><h3 id="原则-1" tabindex="-1">原则 1 <a class="header-anchor" href="#原则-1" aria-label="Permalink to &quot;原则 1&quot;">​</a></h3><p><strong>Local First</strong></p><p>不依赖 Beacon Cloud。</p><h3 id="原则-2" tabindex="-1">原则 2 <a class="header-anchor" href="#原则-2" aria-label="Permalink to &quot;原则 2&quot;">​</a></h3><p><strong>Provider First</strong></p><p>不要把 GitHub / Jenkins / Agent 写死在 Core。</p><h3 id="原则-3" tabindex="-1">原则 3 <a class="header-anchor" href="#原则-3" aria-label="Permalink to &quot;原则 3&quot;">​</a></h3><p><strong>Action First</strong></p><p>展示信息只是第一步，下一步操作更重要。</p><h3 id="原则-4" tabindex="-1">原则 4 <a class="header-anchor" href="#原则-4" aria-label="Permalink to &quot;原则 4&quot;">​</a></h3><p><strong>Progressive Disclosure</strong></p><p>默认小而安静，需要时展开。</p><h3 id="原则-5" tabindex="-1">原则 5 <a class="header-anchor" href="#原则-5" aria-label="Permalink to &quot;原则 5&quot;">​</a></h3><p><strong>Windows Native</strong></p><p>充分利用 Windows 原生能力。</p><h3 id="原则-6" tabindex="-1">原则 6 <a class="header-anchor" href="#原则-6" aria-label="Permalink to &quot;原则 6&quot;">​</a></h3><p><strong>Composable</strong></p><p>Connection、Widget、Action、Workflow 可以组合。</p><h3 id="原则-7" tabindex="-1">原则 7 <a class="header-anchor" href="#原则-7" aria-label="Permalink to &quot;原则 7&quot;">​</a></h3><p><strong>Extensible</strong></p><p>先 Provider，后 Plugin。</p><h3 id="原则-8" tabindex="-1">原则 8 <a class="header-anchor" href="#原则-8" aria-label="Permalink to &quot;原则 8&quot;">​</a></h3><p><strong>Don&#39;t Rebuild Existing Tools</strong></p><p>Beacon 连接工具，而不是替代工具。</p><hr><h1 id="_61-最终定位" tabindex="-1">61. 最终定位 <a class="header-anchor" href="#_61-最终定位" aria-label="Permalink to &quot;61. 最终定位&quot;">​</a></h1><p>Beacon 不应该被定义为：</p><blockquote><p>一个 Windows Dashboard。</p></blockquote><p>而应该定义为：</p><blockquote><p><strong>一个常驻 Windows 桌面的开发者状态与操作中心。</strong></p></blockquote><p>它帮助开发者回答三个问题：</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>现在发生了什么？</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>我现在应该做什么？</span></span></code></pre></div><p>最终目标：</p><blockquote><p><strong>让开发者不需要不断打开各种开发工具来确认状态。</strong></p></blockquote><p>Beacon 在桌面上持续发出信号。</p><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>           Beacon</span></span>
<span class="line"><span></span></span>
<span class="line"><span>══════</span></span>
<span class="line"><span></span></span>
<span class="line"><span>Developer</span></span></code></pre></div><p><strong>Beacon 的核心不是 Dashboard，而是 Signal + Action。</strong> :::</p><p>如果后续真正开始实现，我建议<strong>第一步不要直接让 Code Agent “开发 Beacon”</strong>，而是先让它把这份计划转换成一份 <strong>技术 RFC + Solution/项目目录 + MVP Issue 列表</strong>，然后再按 Phase 1</p><p>══════</p><p>Phase 2 逐步实现。这样更不容易一开始就把 Beacon 做成一个过度复杂的 Dashboard。</p><hr><p>If you want, I can:</p><ul><li>继续描述本地 AI 支持的实现细节</li><li>展开介绍Beacon的插件机制设计与发展规划</li><li>列出Beacon在MVP阶段重点实现的关键功能清单</li></ul>`,506)])])}const b=s(t,[["render",i]]);export{u as __pageData,b as default};
