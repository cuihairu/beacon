# ISLAND-DWM-1 岛窗口白边/外凸底角修复取证（2026-11-28）

## 病灶与根因

用户真机报「圆角有白边、底部外向圆角」。像素取证（build c0215aa @200%，
`../ui-spacing-1127/imac-200/island-faces-collapsed.png`）：底两角弧外 2-3px 纯白
（255）弧带、亮于壁纸 ~200；顶角同位置零白。根因 = Win11 DWM 给每个顶层窗画 1px
亮色边框 + 系统圆角，叠在 SetWindowRgn 边界上；窗顶 −4px 出屏藏掉顶部 DWM 边框，
底边全暴露。BrimDeck（WPF AllowsTransparency）天生没这层，抄代码时漏抄这条结构性差异。

## 修复内容（commit 14c9fff）

- CapsuleWindow.ApplyIslandDwm：岛态 DWMWA_NCRENDERING_POLICY=DISABLED +
  DWMWA_WINDOW_CORNER_PREFERENCE=DONOTROUND + DWMWA_BORDER_COLOR=NONE；经典态回默认。
- IslandEdgeBandWindow.BandPenWidthPx 3→4（200% DPI 内半 2px 盖 region 内缩+路径 AA 缝）。

## 取证结果（imac session 1，2026-11-28 02:35）

- CI 编译门 run 46046724071：build-test success（2m17s），我手改的三处 App C# 编译过关。
- imac 本地 publish（源码 tar 传机、`dotnet publish -f net10.0-windows10.0.19041.0 -r win-x64`）：
  PUBLISH-EXIT 0 / EXE True（详见 `publish-dwm.txt` 原件在 imac `C:\beacon-verify\`）。
- scheduled task `IMAC\cui` Interactive session 1 启动修复构建：ALIVE pid=18972；
  island rect=8024,−4,8508,128（484×132，展开态）。
- **API 级断言绿**：`island-trace.txt` 七条
  `dwm chrome island=True hr=0x00000000,0x00000000,0x00000000`（三项属性全部 S_OK），
  同时 `region ok points=101 client=238x62 offset=3,3`——DWM 关断生效且轮廓链未受损。

## 未完成

- 底角像素复测：凌晨 02:35 显示器休眠（CopyFromScreen 两次全黑，SendInput 撞醒失败），
  `dwm-verify-frame-dark.png` 为暗帧存档，不构成白边裁决。待显示器唤醒后重跑
  `C:\beacon-verify\island-dwm-task.ps1`（scheduled task 同款）取 `dwm-verify-frame.png`
  逐像素核弧外零纯白。
- 用户装机亲验通过才闭项（岛面问题第六单，从严）。
