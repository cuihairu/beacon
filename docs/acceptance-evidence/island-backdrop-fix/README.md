# 岛灰板修复（方案 B：SetWindowRgn 真镂空）CI 取证

巡检单 2026-10-20（beacon-island-backdrop-fix）收口档。岛态窗矩形内、曲面轮廓外的月牙区露灰蒙底（用户实测「岛周围一层灰色底板」）的修复取证。

- run：nightly [43898913432](https://github.com/cuihairu/beacon/actions/runs/43898913432)（head 41da77d，2026-11-16 全绿；Visual evidence + Publish 双 success）
- 代码链（时间序）：c4007ce（方案 A：岛态摘 SystemBackdrop）/ 524612d+4f3702f（AH5 四角色距断言）/ a404049（方案 B：SetWindowRgn 裁曲面轮廓）/ 2349493（region 量窗矩形构建）/ 0d39458+4cbfcc3+28af522（AH3 三层取证：窗矩形 dump / avoidance trace / UIA 树 dump）/ 9acfd0b（**Progman/WorkerW 免避让**）/ 3906815+d7a3ea5（TOPMOST band 重钉）/ d40e2a9+e1fb2a7（**岛面 InvokePattern**）/ fbcad6f（真弧外月牙采样点）/ 0a0eab7（region 全路径 trace）/ 44d78ec（**region 框架落定后重建**）/ 41da77d（月牙 vs 同高参照判据）

## 方案 A 被证伪（2026-11-16 像素实证）

方案 A（岛态摘 SystemBackdrop，c4007ce）假设灰板=DesktopAcrylic 泄漏。CI 像素实证否定：岛态窗四角色距 765=3×255 满值=**纯黑背板**——窗矩形内曲面外的月牙区露的不是壁纸也不是亚克力，而是 WinUI3 窗自身背板（XAML 无内容区渲染为黑）。摘亚克力只是灰板换黑板。方案 A 代码保留（岛态恒摘 backdrop 对方案 B 仍是必要前置），「灰板=亚克力泄漏」根因判定作废。

## 根因链（三层，逐轮 CI 取证定谳）

1. **窗背板露底**：WinUI3 无逐像素透明窗，XAML 未覆盖区=黑。真镂空唯一路径=OS 级裁剪 SetWindowRgn（方案 B，a404049）。
2. **region stale**：region 在启动期构建，此刻窗仍带默认标题栏；presenter 去标题栏 + ApplySize 收窄后 region 不随动（43898913432 前一轮 AH5 批截图实证：窗顶 28px 被裁、只余底部轮廓顶缘条=带肩部卷曲的黑线）。修复 44d78ec：AppWindow.Changed(DidSizeChange) 触发重建 + OnLoadedCore 样式/尺寸落定后显式重建 + 几何去重键（防 SetWindowRgn 自触 frame 回调成环）；RestoreWindowRegion 清键（经典切回同几何也须重设）。
3. **判据采样两重假红**：①窗矩形四角=外框顶在屏外（WINDOWEDGE 3px，CopyFromScreen 采屏外恒黑）且客户区角点在轮廓多边形内（轮廓顶缘横贯全宽、肩曲线切向水平贴 y=0）→ 改采真弧外月牙（fbcad6f）；②CI 桌面宿主终端窗占屏，岛底部背后是深色终端非壁纸 → 只采顶部肩月牙两点、参照改取窗外左右各 30px 同高点（41da77d）。

## 断言口径（场景 AH5 三段）

| 批 | 配置 | 断言 |
| --- | --- | --- |
| AH5a 岛 | capsuleIsland=true，上岛白名单 bigmodel.usage:GLM | 顶部肩月牙 (4,5)/(w−5,5) DIP vs 窗外同高参照，色距 ≤24 |
| AH5b 经典 | showCapsule=true（亚克力复原） | 实测窗四角色距壁纸参照 ≥40（亚克力≠壁纸，经典观感保持红线） |
| AH5c 回切岛 | 岛配置重启第二轮 | 同 AH5a ≤24（重挂→摘→重建无残留） |

三批全过（run 43898913432 Visual evidence success）。

## 像素复核（对 AH5-island-transparent.png 源像素）

- 顶部肩月牙两点 = (255,255,255) = 壁纸白；窗外同高参照 = 同白（距 0）
- 岛内部 (512,16) = (63,185,80) = GLM 环面绿色（非黑背板）
- 裁图目检：黑药丸 + 「9」徽标 + 绿环 + 进度条 + Beacon 品牌，肩部卷曲轮廓清晰、月牙区透壁纸（crop-island-green.png）
- AH5-reisland-transparent.png 同测同值（回切无残留）

## 副产修复（取证链连带落地的三件行为修复）

- **Progman/WorkerW 免避让**（9acfd0b）：前台翻回桌面（Progman 盖满屏+无标题栏）被误判全屏应用 → 岛被 Hide 消失。CI 43822978060「GLM 面不在树」定谳+真机点桌面岛隐没同根因。GetClassNameW 判 shell 桌面窗免避让。
- **TOPMOST band 重钉**（3906815）：SetWindowRgn 可能把窗带出 TOPMOST band（窗 visible 却被非置顶窗盖住，坐标点击按 z 序落空；43850072289 实证）。region 操作后 SetWindowPos 重钉。
- **岛面 InvokePattern**（d40e2a9）：FaceHostGrid 自定义 peer 实现 IInvokeProvider，Invoke 语义=released 单击同款路由（IslandWidgetActivated）。CI 合成点击走不全 WinUI 手势链（拖拽捕获吃掉注入释放），Invoke 与坐标/z 序无关；兼为读屏可达性改进（读屏用户可直达组件下钻）。AH3 组件级下钻取证（AH3-drilldown-panel.png）即由 Invoke 驱动首绿。

## CI 会话不可证项（转装机目检，视觉条目不闭项）

亮/暗壁纸下的岛观感（CI 壁纸恒白单色）、运行时岛↔经典 10 次循环切换（CI 只证 3 次重启链）、切换瞬态有无闪烁残影、DWM 对 region 窗的阴影观感——**待用户装机亲验**。
