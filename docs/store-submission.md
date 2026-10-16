[English](store-submission.en.md) | [中文](store-submission.md)

# 商店提交前置清单

Beacon 本体仍是 **unpackaged 自包含**（RFC §10，`WindowsPackageType=None`）——商店提交只是多产一个 MSIX 包，把 publish 输出整体包起来，包内 Identity 给商店识别用；应用运行形态不变。本页记三件事：MSIX 怎么产、隐私政策页要什么字段、商店素材要备什么。

## 状态（2026-10-12）

| 项 | 状态 |
|---|---|
| SignPath 免费签名 | **参数已接真**（2026-10-12）：`api-token` 与 `organization-id` 走仓库 Secrets（`SIGNPATH_API_TOKEN` / `SIGNPATH_ORGANIZATION_ID`，不硬编码），`project-slug: beacon`、`signing-policy-slug: beacon`；`artifact-configuration-slug` 为可选参数，省略 = 用项目默认 Artifact Configuration（用户在控制台建 Portable Executable + deep signing 配置后如需指定可再填）。**剩用户侧一步（不由 agent 代做）**：在 SignPath 控制台把 API token 所属用户加进 signing policy「beacon」的 **Submitters** 列表——做完后下一次 `v*` tag 起自动签名；未做前版本 tag 构建会在签名步红（fail closed，不静默发未签名正式版）。政策见 README「代码签名政策」 |
| MSIX 打包 | **已在 Windows runner 验证通过并字节级验包**（2026-10-12 两轮 dry run 全绿，包内 manifest 版本替换实测发生）：MakeAppx 就位、清单被接受、约 66 MB 包产出。打包逻辑与 daily-build 版本 tag 通道共用 `.github/workflows/scripts/msix-pack.ps1` |
| 商店应用预留 | 未开始。Partner Center 预留应用名后才确定 `appxmanifest` 的 `Identity Name` / `Publisher` |
| 隐私政策页 | **已就绪**：<https://cuihairu.github.io/beacon/privacy-policy/>（`docs/privacy-policy.md`，随 docs 工作流发布；字段对照见第 1 节） |
| 商店素材 | 图标已备；截图 / 说明 / 分类 / 分级问卷未备（见第 2 节） |

## 1. 隐私政策页必填字段

商店规则：应用访问个人数据或具备网络访问能力时，必须提供**公开可访问**的隐私政策 URL。Beacon 有网络访问（GitHub API + 各 AI 平台用量端点）且在本地保存 API token，属必填项。

页面至少写清这些字段（左列是字段，右列是 Beacon 的实际答案，可直接抄）：

| 字段 | Beacon 的实际答案 |
|---|---|
| 收集哪些数据 | API token（GitHub PAT、各 AI 平台 key）；用户配置（连接、刷新间隔、主题、组件布局）；本机状态读数（CPU / 内存 / 磁盘 / 网络速率 / IP） |
| 数据存在哪 | 全部本地：token 写 DPAPI 密钥库，配置写 `%AppData%\Beacon\config.json`（仅存 credentialRef，**绝无明文 token**），日志写 `%AppData%\Beacon\logs` |
| 数据如何离开本机 | 只发给用户自己配置的 API 端点（GitHub API、各 AI 平台控制台/用量端点）；不发给任何第三方，无遥测上报 |
| 数据用途 | 仅本地展示状态与执行本地操作；token 只用于向对应平台鉴权查询 |
| 保留与删除 | 随安装目录删除；卸载走系统「添加或删除程序」；无云端副本 |
| 联系方式 | 待填（邮箱或 GitHub profile） |

落地要求：

- [x] 公开 URL 已就绪：`https://cuihairu.github.io/beacon/privacy-policy/`（`docs/privacy-policy.md`，docs 工作流发布，提交页直接填它）
- [ ] 提交前该 URL 必须已可访问（商店会抓取）
- [ ] 不能是 `localhost` / 内网地址 / 需登录才能看的页面

## 2. 商店素材清单

| 项 | 要求 | Beacon 现状 |
|---|---|---|
| 应用图标 | 310x310 PNG；manifest 内 `Logo`（商店列表小图标）用 50x50 | 已备：`packaging/msix/Square310x310Logo.png`、`StoreLogo.png`（由 `docs/img/logo.svg` 光栅化，同批还有 44/71/150/310x150 供包内磁贴用） |
| 截图 | 至少 1 张，建议 4 张以上；PNG 或 JPG；常见尺寸 1920x1080（也接受 1366x768 / 2560x1440）；单文件大小上限按提交页提示 | 未备。可从 `docs/img/evidence/` 现有取证截图里选底，但那些多为暗色主题特写，需补浅色主题 + 多屏 + 设置页 |
| 应用说明 | 最多 10000 字符；写产品定位 / 功能点 / 支持场景，可含关键词 | 未备。可复用 README 的安装段 + `docs/mission.md` 的定位段改写 |
| 分类 | 主分类 + 次分类，从商店分类列表里选（开发者工具 / 效率是候选） | 未拍板 |
| 年龄分级问卷 | IARC 问卷逐项答：暴力、恐怖或惊悚、粗俗语言、烟酒、色情或裸露、用户交互、应用内购买 | 未答。Beacon 无内容风险项，预期结果为「Everyone」 |
| 支持 URL | 需填（GitHub issues 即可） | 未拍板 |

> 具体尺寸上限、可选分类值与问卷措辞以提交时 Partner Center 页面为准——本表的数字是常见口径，提交前对照一次。

## 3. MSIX 打包：卡点与替代方案

打包链路（`.github/workflows/daily-build.yml`，仅 `v*` tag 通道）：

```
publish/（自包含输出）+ packaging/msix/appxmanifest.xml（版本按 tag 替换）
  → 暂存目录 AppxManifest.xml + publish 全部文件
  → makeappx pack /d <暂存> /p <asset>.msix
  → 进 SHA256SUMS + gh release upload
```

- **版本对齐**：manifest 里 `Version="0.0.0.0"` 是占位，工作流按 `v*` tag 四段化替换（`v0.1.3` → `0.1.3.0`）；exe 自身的 `Version` 由 publish 步骤注入同一 tag 版本，两者恒等。
- **未签名 MSIX 是有意为之**：商店提交阶段由 Microsoft 代签，本地不签。sideload 测试需自备开发证书（`makeappx pack /o` + 自签证书），属提交前的本地验证步骤，不在 CI 里做。
- **提交前必须替换的占位**：`Identity Name="Beacon.CuiHairu"` → Partner Center 预留的应用名；`Publisher="CN=cuihairu"` → 提交证书主题（与 `PublisherDisplayName` 一致）。
- **Windows runner 实测（2026-10-12，两轮 dry run 全绿：38230149697 + 38233094172）**：
  - MakeAppx 在 runner 上就位：`C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\makeappx.exe`
  - 手写清单被 MakeAppx 接受（命名空间 / `uap:VisualElements` / `uap:DefaultTile` / `rescap:runFullTrust` 全部有效），打包约 10s 完成
  - 包级字节验证（产物下载回本地解包）：zip 容器有效，约 66 MB / 502 条目；`AppxManifest.xml` + 6 张 Logo 全在包根且字节数与仓库文件逐一吻合；`Beacon.App.exe` 与 `Assets\` 随 publish 进包
  - 版本替换路径实测（run 38233094172，试跑台故意传 9.9.9.9 ≠ 占位）：包内 Identity 实际变为 `Version="9.9.9.9"`、占位 `0.0.0.0` 消失——「版本与 v* tag 对齐」机制被证明真实发生，不是空替换
- **当时唯一的卡点是「没法试跑」**：MSIX 步骤只在 `v*` tag 通道跑，而版本 tag 由作者手动给出（仓库红线：不打 tag），用已有 tag 试跑会往已发布 release 上传别的 commit 的资产，属交付完整性违规。解法是抽共享脚本 `.github/workflows/scripts/msix-pack.ps1` + 只建包不碰 release/tag 的 `.github/workflows/msix-dry-run.yml` 试跑台。

如果打包链路将来出问题，替代方案（按推荐顺序）：

1. **Sparse 包**（`WindowsPackageType=Sparse`）——WinUI 3 官方支持的「有包标识但仍按 unpackaged 运行」模式：csproj 加 `WindowsPackageType=Sparse` + `PackageCertificateKeyFile`，appxmanifest 由 MSBuild 自动生成（不用手写）。代价：本地开发与便携形态要跟着换模式。
2. **App Installer 分发**（`.appinstaller`）——不进商店的企业/个人分发通道。
3. **只发 zip + setup.exe**——商店不是分发必需品：便携 zip + Inno Setup 安装包已覆盖个人分发，MSIX 仅为上架。
