# BM2 批本机取证归档 · toast / 托盘 / 通知中心 / detailUrl 链（2026-11-08 归档）

> 证据源：2026-11-06 12:04-12:27 用户本机（Win11，1920×1080）桌面实拍，自 `evidence-out/` 工作目录收编
> 至 `docs/acceptance-evidence/bm2-toast-tray/`。目录内 22 个文件；本台账逐张给证据面映射。

## 隐私与入库口径（用户 2026-11-08 拍板）

- 8 张全帧含微信窗口第三方聊天画面 → **裁剪后归档**：每帧只裁 Beacon 证据区（toast 横幅角 /
  悬浮数字 tile 角 / 状态 chip 角 / 通知中心面板），聊天与浏览器私区不进库。
- 6 张（BM2-desktop-full / BM2-toast-MISS / BM2-taskbar-healthy / BM2-overflow-open /
  BM2-tray-icon-error-zoom / BM2-tray-icon-healthy-zoom）已于 8f3bf5d（2026-11-06 12:06）入公开仓；
  其中 **2 张含私聊全帧（BM2-desktop-full / BM2-toast-MISS）本次以裁剪版替换 HEAD**。
  git 历史仍保留原帧（彻底清除需 history rewrite = force push，按纪律不动；HEAD 面已收敛）。
- 干净帧 7 张原样归档（托盘×2、任务栏条、溢出区、B1-browser / B1-detail / B2-toast）。
- 对照：openclaw 真机 Toast PASS 半证据在 `openclaw-20261106/banner-t2.png` + `notification-center.png`
  （2026-11-06 续批）；本批为用户本机的补强面，两批机器不同源（md5 互异）。

## 逐张映射（todo 证据面）

| 文件 | 内容（实拍核实） | todo 证据面 |
|---|---|---|
| `BM2-desktop-full-toast-banner.png` | **OS toast 横幅实拍**：Beacon.App「GLM · 5h 额度已耗尽 (02-14 00:26 重置)」Last update 08:48:32（12:04 帧） | todo :39 Toast 发送+呈现——本机 OS 横幅 PRESENT 铁证 |
| `BM2-toast-MISS-toast-region.png` | 同横幅区域 12:05 帧：横幅**未现**（已过时效/未再发） | todo :39 MISS 反例（呈现时效面） |
| `BM2-desktop-full-float-tiles.png` 等tiles 系列 7 张（desktop-full / MISS / app-win1 / app-NOSCREEN / os-win1 / os-NOSCREEN / BM2c） | 右上悬浮数字 tile（GLM / DeepSeek，Last update 12:05） | todo :56 悬浮形态（numericFloating）展示面 |
| `BM2b-*-chip.png` / `BM2c-center-open-chip.png` 共 6 张 | 右下状态 chip「1 warn · 1 err · Last update 12:05」 | 聚合状态 chip 面与悬浮 tile 同框自证 |
| `BM2b-notification-center-panel.png` | **通知中心真开**（12:10）：Beacon.App 两条（「Beacon 真机验证 / OS pipeline probe 直接发送（非应用）」12:09、「DeepSeek · ¥420.50 (含赠 ¥20.00)」12:05:31）+11 个通知 | todo :39 通知中心呈现面 |
| `BM2c-center-open-tiles.png` | 通知中心开启后桌面右上 tile 仍在渲染 | todo :39/:56 同上 |
| `BM2-tray-icon-error-zoom.png` | 托盘图标红态（error）240% 放大 | todo :43 托盘图标颜色两态 |
| `BM2-tray-icon-healthy-zoom.png` | 托盘图标绿态（healthy）放大 | todo :43 同上（与 error 同位两态） |
| `BM2-taskbar-healthy.png` | 任务栏全条：托盘 healthy 态在栏 | todo :43 |
| `BM2-overflow-open.png` | 溢出区弹层绿色图标特写 | todo :43 图标不进溢出的对照面 |
| `B2-toast.png` | **Beacon 详情窗 GLM Failed 态**（95%，bigmodelusage，Error · Failed，fetched 08:47:10；DETAILS 百分比/总量/重置日；ACTIONS=Open） | todo :39 detailUrl 链起点：详情窗 Failed 态 + Open 动作 |
| `B1-detail.png` | 详情窗 GLM **Success 态**（38%，fetched 08:46:28） | todo :39 详情窗 Success/Failed 双态对照 |
| `B1-browser.png` | Open 后浏览器落 `open.bigmodel.cn/usage`（智谱用量页） | todo :39 detailUrl 打开面：详情窗 Open → 浏览器导航成功 |

## 断言口径

- 横幅 PRESENT（12:04）→ MISS（12:05）：同一分钟相邻帧，证明 OS 横幅有时效（Win11 toast 约 5-10s 自动收进
  通知中心），MISS 非发送失败——通知中心面板帧（12:10）里两条 Beacon 通知在栏佐证。
- detailUrl 链：B2（详情窗 Failed+Open）→ B1-browser（usage 页打开）= 详情窗 Open→浏览器半段真机 PASS；
  **OS toast 横幅本体点击→激活路由仍 OPEN**（见 2026-11-08 detailUrl 复现台账，另文）。

## 归档时间线

- 2026-11-06 12:04-12:27 本机实拍；8f3bf5d 入库 6 张。
- 2026-11-08 用户令归档 + 拍板裁剪口径；本台账成立，evidence-out/ 工作目录清退。
