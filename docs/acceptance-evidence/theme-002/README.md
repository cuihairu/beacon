# 外观页主题卡片选择器（BEACON-THEME-002）imac 真机取证

代码：bebb901+810ed69（卡片式主题选择器+色卡组+搜索索引迁移）、c679667（卡片 Border→真 Button：Border 版 SendInput 坐标点击在高 DPI 宿主上 UIA rect 与真实布局错位、三连点全空转，Button InvokePattern 一发即中，兼键盘可达）、61ec2ab（设置窗根背板显式 Surface+翻转重刷：窗无 SystemBackdrop，WinUI 启动基线深色窗底在浅色翻转后仍透出，浅色主文本深墨色打在黑底不可读）。

## schtask 取证（BTheme002，IMAC\cui Interactive session 1）

脚本：起 app `--settings` → UIA Invoke `module-appearance` → FindFirst `theme-card-*` → **InvokePattern.Invoke**（零几何依赖）→ config 回读 + `theme-check-*` 勾角标在树断言（Collapsed 元素离树：找得到=该卡选中）→ PrintWindow 截图 → PASS 后点回原卡还原。

两轮双向 PASS：

| 轮 | before | target | after | samePid | settingsDiff | checkInTree | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | dark | theme-card-light | light | True | 34.7% | True | PASS |
| 2 | system | theme-card-dark | dark | True | 87.4% | True | PASS |

- `before-light.png` / `after-dark.png`（1800x1359）：浅色轮全窗白底可读（背板修复实证）、深色轮墨蓝背板清晰；卡片三枚（跟随系统/浅色/深色）迷你预览+选中边框+勾角标两向都正确。
- `verify-log.txt`：THEME002-VERDICT PASS 两轮全文（含还原链 THEME-RESTORED）。
- pid 不变+diff 34.7%/87.4% = 不重启即时切换（THEME-001 链同窗实证，theme-001/ 目录同证据）。
- CI 场景 SJ 同步改版：visual-evidence.ps1 主题段改卡片 InvokePattern+config 回读+勾标断言（c679667）。
- imac 机器状态已还原：config theme=system（验证脚本 PASS 路径自动点回原卡，22:18 轮 THEME-RESTORED system）。

遗留观察：QuickPanelWindow/CapsuleWindow 无显式背板（亚克力/自绘深色系），浅色适配未在本单范围，待用户目验反馈。
