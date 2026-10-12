# Beacon 隐私政策

最后更新：2026-10-12 · 适用版本：v0.1.x（含每日构建）

## 一句话总结

**Beacon 不收集、不上传任何数据到开发者的服务器。** 所有数据只存在你自己的电脑上；网络请求只发往你自己配置的 API 端点和本项目官方仓库的更新检查接口。

## 收集哪些数据

Beacon 在**你的电脑本地**保存三类数据，全部由你自己录入或由本机运行产生：

| 类别 | 内容 | 来源 |
|---|---|---|
| 凭据 | GitHub PAT、各 AI 平台 API key（智谱 / Kimi / DeepSeek / 火山方舟 / 阿里千问 / 小米 MiMo / Claude / Codex / Copilot / OpenCode 等，按你配置的连接而定） | 你在设置页录入 |
| 配置 | 连接配置、刷新间隔、主题、组件与钉选布局、动作（含可选 webhook） | 你在设置页调整 |
| 本机状态读数 | CPU / 内存 / 磁盘 / 网络速率 / IP 地址等，仅用于本地展示 | 本机采样 |

Beacon **没有**账号体系，**没有**遥测或埋点，**不**向开发者或任何第三方上报任何数据。

## 数据存在哪里

全部在本机用户目录 `%AppData%\Beacon\`（即 `%APPDATA%\Beacon`，普通用户态，无需管理员）：

| 文件 / 目录 | 内容 | 保护方式 |
|---|---|---|
| `secrets.bin` | 所有凭据 | **DPAPI（CurrentUser）加密**——只能被同一 Windows 账户解密，文件本身不含明文 |
| `connections.json` | 连接配置 | **不含明文凭据**：只存 credentialRef 引用，密钥本体在 `secrets.bin` |
| `config.json` / `widgets.json` / `pins.json` / `actions.json` | 全局设置 / 组件 / 钉选 / 动作 | 明文 JSON，不含凭据 |
| `cache\` | 各组件最近一次状态缓存 | 明文 JSON |
| `notifications.json` | 通知历史与已读态 | 明文 JSON |
| `logs\` | 运行日志 | 明文文本（排障用） |

配置的导入导出包**永远不包含凭据**——导出文件里只有 credentialRef 引用，换机后需重新录入密钥。

## 数据如何离开本机

Beacon 主动发出的网络请求只有三类，全部可预期、可关闭：

1. **更新检查**（默认开启，设置里可关）：启动后向本项目的公开仓库接口 `api.github.com/repos/cuihairu/beacon/releases/latest` 发一次 GET——请求里只有 User-Agent（`Beacon-Update-Check`）与你的 IP（任何网络请求都会携带），**不含任何个人数据**。
2. **连接查询**：只发往**你自己配置**的 API 端点（GitHub API、各 AI 平台控制台/用量端点），鉴权用你自己录入的 key。你配置哪些连接，Beacon 就联系哪些端点；删掉连接即停止。
3. **动作**：仅当你自己配置了 HTTP / webhook 动作时，才会向你指定的 URL 发请求（webhook 带 HMAC-SHA256 签名头，密钥同样只进 DPAPI）。

除以上三类外，Beacon 不建立任何其他网络连接。

## 数据用途

本地数据只用于：在屏幕上展示状态（胶囊、悬浮组件、快速面板）、执行你配置的本地动作、维护设置与通知历史。凭据只用于向对应平台鉴权查询，不做任何其他用途。

## 保留与删除

- 数据随安装目录存续；卸载走系统「添加或删除程序」（或直接删除便携包目录），再删除 `%AppData%\Beacon\` 即可**彻底清除全部本地数据**，包括加密凭据。
- Beacon 没有云端副本——删了就是删了。
- 配置页的「导入/导出」只搬运配置（不含凭据），不影响本条。

## 联系方式

对本政策或数据处理有疑问：

- GitHub：[github.com/cuihairu](https://github.com/cuihairu)（项目仓库 [cuihairu/beacon](https://github.com/cuihairu/beacon) 提 issue 即可）
