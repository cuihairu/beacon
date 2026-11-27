# 岛展开态白板 + 底部残留 imac 真机取证（ISLAND-FLANK-1 / ISLAND-BOTTOM-1）

- 日期：2026-11-26（构建部署 03:49 UTC，取证 03:55–04:08 UTC）
- 机器：imac（Windows 控制台会话 1 活跃，主屏 5120×2880 物理 @200% DPI，岛顶中锚定）
- 构建：`2026.11.25+d8b52e8`（nightly run 45670616849 资产，SHA256 `bdcf5bfe…` 与发布页一致）
- 部署路径：`C:\beacon-verify\beacon-app`（scp zip + Expand-Archive 覆盖，ProductVersion 核对）

## 病灶回归检查（对 ISLAND-FLANK-1 / ISLAND-BOTTOM-1）

### 展开态侧板（FLANK-1 主诉：左右各露 38px 白色底板）

程序化首展（`BEACON_DEBUG_ISLAND_PREVIEW=1`，与 CI 场景 AH 同一钩子、同一 `ShowPreview()` 渲染路径）后实测：

- 展开窗框 (2319,−3) 482×192 物理（96 DIP 高）；clientRect (2322,0) 476×186
- band `BeaconIslandEdgeBand` (2318,−4) 484×194 = clientRect 各向外扩 4px，**与修复口径精确吻合**
- 翼板采样（CI AH1d 同点位：窗缘内 4 DIP / 16 DIP，半高 y=93）：左 (0,0,0)、右 (0,0,0)，**四点三通道和全 0（近黑实着，无白色通道/露背板）**
- 全帧扫描：框内白色像素 1133/87216 = 1.3%（内容文字），无成片白板
- 药丸横向 x 322..800 vs 窗框 320..802——**直边压窗缘全宽**（ShoulderDips=0 生效，侧通道物理消失）

### 底部残留 + 空白悬浮条（BOTTOM-1 主诉）

- 顶窗枚举（会话 1 全量）：仅 `Beacon Status`（岛主窗）+ `BeaconIslandEdgeBand`（描边带）+ `Beacon Pinned`（桌面 tile）三窗，**无未透明化空壳窗**（`_hasFrame` 门生效，空白胶囊条不再复现）
- 展开态底缘（中心列 y 180..197）：黑 (8,8,8)→(0,0,0) 至窗底，y=188 一行 166 灰（AA 抗锯齿过渡），y≥189 即背景（桌面内容 198–206 浅灰）——**无白色/灰带残留**
- 收起态同口径干净（`imac-collapsed-200pct.png`：药丸 302..780 vs 窗框 300..782，底缘黑→背景直接过渡）

## 证据文件

| 文件 | 内容 |
|---|---|
| `imac-expanded-200pct.png` | 展开态区域截屏（含岛 + 带窗 + 两侧背景） |
| `imac-collapsed-200pct.png` | 收起态区域截屏（底缘干净） |
| `imac-fullscreen-expanded.png` | 展开态整屏（5120×2880，空白条全屏扫描底稿） |

## CI 面佐证

- nightly run 45670616849（head d8b52e8）Visual evidence 全绿：AH1d 展开态翼板实着断言、AH5d 底角月牙、AH5e band 几何 ±1px 硬门 + 岛底条带零外来墨（d8b52e8 行持续双门）
- 悬停往返（PointerEntered/Exited 真实事件链）由 CI 场景 AH1a–AH1c 门控（同 run 绿）；真机侧 SendInput 绝对坐标在 imac 三混合 DPI 屏虚拟桌面映射落点偏移，未做真机悬停往返（交互链 CI 已门，本单病灶是几何非交互）

## 验收状态

CI 像素断言绿 ✅ + imac 真机截屏无白板/无残留/无空壳窗 ✅ ——**待用户 vision 亲验通过才闭项**（岛面问题第四、五单，从严；agent 不自评闭项）。

## 2026-11-28 用户报障复核（圆角白边仍在 + 底角形态质疑）

用户 2026-11-28 报「圆角还是有白边，底下的角是圆角，底部的是外向的圆角，你抄都抄不对」，
并强调本机已是最新版本（imac 实测 ProductVersion=2026.11.26+c0215aa，nightly e954a21 之后一版，
含 BEACON-FIX-1126 全部修复）。

vision 对三张既有证据图复核：
- `imac-collapsed-200pct.png`（收起态）：顶角=直角、底角=外凸、无白月牙 —— 与用户「顶角不该直」诉求矛盾，
  说明 4b934bc 的顶角弧修复在收起态没生效或该图抓的是旧构建。
- `imac-expanded-200pct.png`（展开态）：**底边及两底角弧线外侧确认存在细浅灰白月牙状亮边**（上轮取证漏检）。
- `imac-fullscreen-expanded.png`：同样确认底边/底角外侧细白月牙 + 角部小白残片。

结论：用户报的「圆角白边」为真，上轮 2026-11-26 取证把它漏检为「干净」（视觉断言采样点未覆盖
底角弧线外月牙的细白亮边，与 2026-11-18「只采顶部漏掉底部」同型盲区）。reopen 复核，修复前先
在真机抓「坏状态」特写作为 before 证据（弧线外侧 2-4px 放大裁图），修复后同点位 after 对比。
