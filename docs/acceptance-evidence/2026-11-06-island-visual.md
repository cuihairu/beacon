# 悬浮岛视觉重做 · 真机验收证据（USER-RPT-1，2026-11-06）

> 用户 2026-11-06 实测「简直无法看」——岛形态从未做过真机视觉验收即报完成。
> 本轮按 Dynamic Island 观感标准重做并真机三态取证。

## 机器与构建口径

- 机器：10.240.0.5（Win11 26H2，1920×1080 @100% 单屏，host `cuihaitao`）。
- 代码：`main` `8f3bf5d` + 岛视觉 patch（`CapsuleWindow.cs/.xaml`、`ThemeColors.cs`、`MotionEngine.cs`）+ 评审修复两处（环缓存恒更新、MorphIn 仅岛形态）。
- 现场构建（13:35，`C:\beacon-verify\build\beacon-island2`，patch 标记门禁 markers=1,2,1,1,1 先行校验防「无补丁假构建」）：`dotnet build` 0 错误、`dotnet test` **TEST_EXIT=0**（同 patch 基线 12:40 日志 1670 通过/0 失败：Core 929 + Connections 673 + Storage 41 + Actions 27；两处修复均在 App UI 层不影响测试计数）、`dotnet publish -r win-x64 --self-contained true` → `publish-island`（401 文件，13:35:04）。
- 取证方式：窗口级 `PrintWindow(PW_RENDERFULLCONTENT)`（控制台锁屏时 `CopyFromScreen` 只得壁纸帧）；悬停展开用 SendInput 绝对坐标注入（INPUT 队列级，XAML 输入管线与真实鼠标同路径）。
- 教训记录：13:00 一轮 `git apply` 在构建机静默失败（scoop `current` 结点路径）导致整轮构建/截图跑在无补丁树上、截图显示旧环+蓝徽标——后续改为直接 scp 覆盖四个源文件 + 构建前标记校验硬门禁；publish 必须带 `--self-contained true`（框架依赖版在真机缺 .NET 10 x64 运行时启动即弹错误框，见 .NET Runtime 事件日志 13:02:19）。

## 三态取证（最终构建实际截取）

| 状态 | 证据 | 窗口实测 |
|---|---|---|
| ① 收起（健康） | `island-collapsed.png` / `-4x` | 135×39 @ (892,369)，水平顶中（中心≈960；y=369 为拖动持久化位置，islandLayout） |
| ② 徽标变色（error 注入） | `island-badge-abnormal.png` / `-4x` | 135×39；徽标 23 底色 蓝→**红**，灯点同变红，无蓝残留 |
| ③ 悬停展开 | `island-expanded.png` / `-4x` | 162×94 @ (879,369)，顶缘不动向下长出（MorphIn 缩放+淡入；仅岛形态启用） |
| ③a 收回复核 | `island-recollapsed.png` | 出窗即收，135×39 还原 |
| 附：点击唤起看板 | `island-board-open.png` / `island-under-board.png` | 看板 736×576（默认 720×560+边框），岛保持顶中 |

## 视觉诊断（vision 自查）

- **收起态**：近实色黑药丸 + 发丝白描边，健康环已退场（不再读作 spinner、无灯点双绿冗余），单灯点+晕扛状态，品牌字恒近白——暗底白字对比恒定，亮壁纸不再稀释（旧帧 `docs/img/config-effects-src/capsule-island-on.png`：灰底半透明「看不清」，即用户投诉原貌）。
- **徽标变色**：error 注入后徽标底色 accent 蓝→聚合级别红，与红灯同色系——异常压过未读的取色优先级修复生效，同一时刻只讲一个颜色的故事。
- **展开态**：预览面板同黑底白描边随形变长出，行内级别点/来源名/摘要三级对比清晰（来源名白、摘要灰、截尾省略号），无错位无溢出。
- 前后对比总图：`island-visual-before-after.png`（左起：BEFORE 旧视觉 / AFTER 收起 / AFTER 徽标变色 / AFTER 展开）。

## 验收口径

USER-RPT-1 的闭项条件是「用户装最新包亲眼确认可看」——本轮交付达到的子项：patch 落源（含评审修复）、真机构建部署、三态截图+视觉自查通过、前后对比证据落库。用户确认前 todo 保持 OPEN。
