# 主题即时切换不重启（BEACON-THEME-001）imac 真机取证

代码：a2cd117（Core ThemeResolution 纯函数 + ThemeRuntime 实时单源 + 三窗广播链 + 启动根初值，Core 17 例锁值）。

## 证据（与 BEACON-THEME-002 同链同窗：卡片 SaveThemeFromCard → ThemeRuntime.Apply → SettingsApplied 三窗广播）

BTheme002 schtask 两轮 PASS（详见 ../theme-002/README.md）：

- **pid 不变翻转**：before=dark→after=light（samePid=True，settingsDiff 34.7%）与 before=system→after=dark（samePid=True，settingsDiff 87.4%）——同一进程内根元素 RequestedTheme 重设+两段广播完成，无重启。
- `before-light.png` / `after-dark.png`：同窗两主题整窗渲染对照（浅色白底/深色墨蓝背板，61ec2ab 背板跟随后全窗可读）。
- `verify-log.txt`：THEME-AFTER + pid-same=True + THEME002-VERDICT PASS 全文。

Core 单测：ThemeResolution 17 例（上会话全绿）；CI 场景 SJ 主题档（c679667 Invoke 版）2026-11-28 nightly 46154272071 实证通过——`theme-card-light`/`theme-card-dark` 两轮 InvokePattern+config 回读+勾标在场全绿（「场景 SJ 通过：浅深色落盘 + 占用失败可见 + 原键保留 + 成功即时生效——SET-UX-11」；该 run 整体红系 AH5d-B 岛 region 旧账，与主题链无关）。
