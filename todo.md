# TODO

> 生成于 2026-10-11（无 todo 文件；来源：docs/mvp-issues.md 未完成 42 项全量搬入 + docs/master-plan.md 后续可选）。
> 2026-10-11 巡检复点：六条「夹具/表驱动单测」单测半全部核对（模板渲染补 7 例，批3-补39），手动半统一标注【需装机】留待用户；后续可选两条为范围决策不标注。
> 2026-10-12 装机清单 CI 代验转化（批3-补50）：daily-build 视觉取证新增场景 E（单实例+托盘驻留+Exit）、F（无网启动+Last update+自愈+零弹窗+单条通知）、G（调色即时生效/非法回退/重置/动效持久化/帧差分静止呼吸/两 tile 独立）、H（悬浮位置两轮还原+TOOLWINDOW+刷新不抢焦点+panel/floating 二选一）、J（静默安装/卸载）；安装包步骤前置到取证之前。下表「已由 CI 代验」均指此批场景，证据在 Actions `visual-evidence` 工件；断言失败=红构建。
> 2026-10-12 批3-补51：G 系色彩断言阈值按 38261911354 工件逐像素实测重标定（状态灯亚像素渲染漂移 10~44px，详见决策记录）；托盘菜单驱动改 AttachThreadInput 取前台（ForceForeground）。

## 决策记录

- **断网降级通知缓存命中路径永不开火**（2026-10-12 装机清单 CI 代验转化批3-补50，场景 F 首跑实证）：PublishStale 缓存命中时 severity 沿用缓存值（健康时=Success），NotificationEngine 入口 Success/Info 一律静默 → builtin.connection-degraded 只在「从无缓存首次失败」开火，真正的「有数据后断网」（规则名所指的降级）反而不通知。修复：缓存命中 stale 路径 severity 抬到 Max(cached, Warning)（与无缓存路径一致；tile 灰显由 ConnectionHealthy 驱动不受影响，恢复后 provider severity 自然回填）；补 WidgetHostTests 两例回归（Success 抬 Warning / Error 不动）。F5 断言相应移入断网窗口内 + F5b 恢复后记录恒 1。
- **moonshot.balance 数值位补映射**（2026-10-12 巡检点火，用户倾向「补一行映射」→ 拍板落地，批3-补49）：payload 只有 `available_balance`，不在 `WidgetValueHint` 优先级链 → 数值位跌 `Summary[..7]+"…"` 截断，数字全没（「Moonshot · ¥110.00」→「Moonshot…」）——正是 hint 链立项要消灭的「数不出值」失败模式。**口径**：`¥{value}` 固定 CNY 符号（开放平台只按 CNY 计价，provider Summarize 已硬编码 ¥，不引 deepseek 式币种开关）；`0.00` 顶格形态，宽预算与 deepseek 同款 `¥99999.99`。同轮 hint 链全量自查另修两处：qwen 配额用尽路径补 `value_text="已用尽"`（比照 mimo「无额度口」显式文本先例）、machine.git 补 `value_text`（Summary 计数段同源，兜底截断把 ↑↓/改 计数全截没）。
- **tile 状态灯像素断言阈值重标定（亚像素渲染漂移）**（2026-10-12 批3-补51，38261911354 工件逐像素取证）：8DIP 状态灯同一几何在不同窗口实例渲染于不同亚像素位置 → 匹配像素 10~44 px 漂移（D 场景五实例 10、G2 三轮 44、G1 品红两跑 44/24、G4 橙 24/24；tol45=tol60 计数相同，tol90 起误吞 icon 灰）。G4 连红两轮的 24 px 是半像素偏移的正常渲染（精确核 16 + 0.89 覆盖率均匀边缘环 8），**非 3be02b6 所诊「呼吸调光」**——呼吸只存在于 full 档（StartBreathing 入口 `Mode != Full` 即返回），reduced/off 根本不启动；该次误诊引入的 motion off 钉死无害（G7b 帧差分仍依赖），保留。GLM tile 无进度条（state.Progress 为空 bar 收起，G2 绿 bbox 8×8 实证），此前「状态灯+条带」「95% 填充外推 ~92px」均为误设。失效模式（palette 未用新色/渲染缺失）恒 0 px → 色彩存在性断言统一定标实测底（10）之下、0 之上 = 8（G1/G2/G4/G5/G7a 五处）。另证：PS 非前台时 BringToFront 对任意窗口静默失败（E2/E3 菜单仍不弹的根因），托盘菜单驱动改 AttachThreadInput 共享前台权（WinEnum.ForceForeground）。

## MVP 待验/未修项（原样搬自 docs/mvp-issues.md，共 42 项，完成即改回该文件勾选）
- [ ] 双击 exe 二次启动不重复进程，既有实例收到唤起（气泡提示）——CI 已代验进程半（场景 E1：二次启动进程数恒 1 + 副实例退出日志硬断言）；唤起气泡是 OS Toast 面不可脚本取证【需装机（半）】
- [x] 关闭所有窗口后进程驻留托盘，Exit 才退出【已由 CI 代验：场景 E2 设置窗 WM_CLOSE 后进程驻留 + E3 托盘菜单 Exit 后进程退出（菜单驱动失败时降级 warning 转装机，见 run 日志）】
- [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱【需装机：胶囊（L0）位置持久化与真实 DPI 拓扑切换需物理显示器；悬浮 tile 半已由场景 H 代验】
- [ ] 任意前台应用下热键生效；可在设置改键并持久化——CI 已代验应用侧链（场景 K1：启动日志无 RegisterHotKey 失败 = 注册成功；SendInput 和弦优先，环境不可达时 WM_HOTKEY 直驱同入口验证 → 面板窗出现硬断言）；真实键盘在任意第三方前台的 OS 级唤起（SendInput 键盘注入 CI 会话不可达，与鼠标手势同限制）与设置改键持久化需装机【需装机（半）】
- [ ] 开关即时生效（重启验证）；卸载清理——CI 已代验卸载半（场景 J：静默安装/卸载、程序文件清空、用户数据保留）；开机自启注册表与重启生效需装机【需装机（半）】
- [ ] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口【需装机：应用无故障注入开关，加注入点属代码变更另立项】
- [x] 无网启动：界面有数据且标注 Last update，随后自动刷新【已由 CI 代验：场景 F1-F4（断网启动有窗有缓存数据、Last update 标注 45s 硬断言、恢复后 90s 自愈百分比读数）】
- [ ] 真实 PAT 手动验证连接测试成功/失败路径【需装机：需真实 GitHub PAT（mock 链路已由场景 D 代验，真实凭据属用户侧）】
- [x] 夹具单测：计数/映射正确（GitHubPullRequestsProviderTests 18 例：计数/亮黄映射/红 CI 续探复用；2026-10-11 巡检复点）
- [ ] 真实仓库手动验证（PR 计数与亮黄随刷新走）【需装机：需真实 GitHub PAT 与真实仓库数据；API 层已被 18 例假 Handler 单测覆盖】
- [x] 状态映射表驱动单测（GitHubActionsProviderTests：MapRun_TableDriven TheoryData + 端到端共 9 例）
- [ ] 真实 workflow 手动验证（成功/失败/运行中）【需装机：需真实 PAT + 真实仓库 workflow 状态流；映射层已被表驱动单测覆盖】
- [x] 路由/确认/超时单测（ActionRunnerTests 9 例：按类型路由/确认三态/超时取消/异常仍发事件/用户取消）
- [ ] 失败在 UI 有可见反馈【需装机：动作失败事件链需真实触发一次失败动作；ActionRunner 事件半已被 9 例单测覆盖】
- [x] 模板渲染单测（TemplateRendererTests 11 例；2026-10-11 补齐 7 例：键字符集/同键重复/单趟不二次展开/字面花括号/缺键去重）
- [ ] 手动验证浏览器打开正确 URL【需装机：默认浏览器行为属用户环境；URL 拼接已被模板渲染单测覆盖】
- [x] API 封装单测（假 Handler）（GitHubApiClientTests 10 例：ETag/401/403 配额/404/5xx/限流头/Bearer + GitHubWorkflowExecutorTests 6 例）
- [ ] 真实仓库手动：Run→运行中→终态全程可在 Beacon 看到【需装机：需真实仓库 workflow 触发与真实 PAT】
- [x] 超时终止生效；输出落盘（LocalCommandExecutorTests：Timeout_KillsProcess_AndStillWritesLog、RunsRealCommand_CapturesOutput_WritesLogFile）
- [ ] 确认弹窗可关（按 Action 配置）【需装机：需触发本地动作执行流弹出确认框（确认三态已被单测覆盖）】
- [ ] 发送/点击/激活路由全链路手动通过；快捷方式缺失可自愈【需装机：SendInput 落到真实目标应用的体验判定（CI 合成点击在会话桌面不可靠，此前有降级先例）】
- [x] 规则引擎表驱动单测（含冷却）（NotificationEngineTests 17 例：阈值 TheoryData 表驱动/冷却抑制与到期放行/跨越重臂/停用与过滤）
- [ ] 手动：注入 Failed 状态收到且仅收到一条 Toast——CI 已代验记录半（场景 F5：断网降级通知恰 1 条，connection-degraded 冷却去重，notifications.json 硬断言）；Toast 呈现面是 OS UI 需装机目检【需装机（半）】
- [ ] 聚合状态变化 ≤1 个刷新周期内反映到托盘【需装机：托盘图标像素在通知区域不可脚本取证；同链路 tile 层已由场景 G7a 代验（38%→95% ≤75s 反映）】
- [ ] 断网显示 Offline+时间；计数与实际 Widget 状态一致——CI 已代验 tile 半（场景 F2：Last update 离线标注硬断言）；面板计数一致性需装机【需装机（半）】
- [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭——CI 已代验两半（场景 K2：断网 Stop mock 后热键开面板、缓存百分比读数在即无白屏；K3：Esc 关闭优先、键盘注入不可达时降级 warning 并以热键 toggle 硬断言面板可关）；Esc 键盘半与 <100ms 精确计时需装机【需装机（半）】
- [ ] 计数/事件与状态一致；右键 Pin 落库（本阶段无 UI 效果）——面板标题锚点已补（「Beacon 快速面板」，场景 K 用），计数一致性与右键 Pin 交互需装机【需装机：面板行点击/右键交互流未建场景】
- [ ] 从 L2 事件一键打开对应详情；[Retry] 全链路可用【需装机：面板行点击→详情窗交互流未建场景（锚点已具备，可后续转化）】
- [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面——CI 已代验刷新半（场景 H1：mock 38%→95% 翻档期间前台窗句柄不变硬断言）；点击 tile 打开详情属用户意图合法夺焦，空白点击穿透需装机【需装机（半）】
- [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab【已由 CI 代验：场景 G7a（GLM 95% 时 DeepSeek 420.5 不动）+ 场景 H（每 tile WS_EX_TOOLWINDOW 置位、WS_EX_APPWINDOW 清位硬断言）】
- [ ] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一——CI 已代验还原与二选一半（场景 H 两轮重启精确落位 120,120/260,220 ±4px + H5 panel 模式悬浮窗归零）；拖放手感需装机【需装机（半）】
- [ ] 两种入口（设置开关/右键 Pin）行为一致并持久化【需装机：右键 Pin 在 L2 面板内，无 UIA 锚点】
- [ ] 非准入类型在两处入口均不可钉【需装机：同上】
- [ ] 重启后位置/收起态还原；拔显示器再接回 tile 不丢——CI 已代验位置半（场景 H 两轮重启落位一致）；收起态与拔显示器物理事件需装机【需装机（半）】
- [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区【需装机：DPI/分辨率拓扑切换需物理或虚拟显示栈（CI runner 单一虚拟屏不可驱动）】
- [x] 断网全流程无任何弹窗；恢复后 tile 自愈【已由 CI 代验：场景 F3（断网期间 Beacon 进程名下 #32770 对话框数=0）+ F4（mock 恢复 90s 内百分比读数回归）】
- [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出【需装机：无 Critical 注入面——grep 实证所有 Provider 最高只产 Error，应用无状态注入钩子；需先补注入机制另立项】
- [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录【需装机：四条含真实网络环境与真人操作流】
- [ ] 设置改级别色 → L0 tile、胶囊、托盘图标同步变色——CI 已代验 tile 半（场景 G4：设置页 ValuePattern 改 #ff8800 → config 落盘 + tile 像素即时变橙）；胶囊与托盘图标像素不可脚本取证【需装机（半）】
- [x] 某 Widget 设 colorOverride → 仅该 tile 变色，优先级正确【已由 CI 代验：场景 G1（GLM #ff00ff 品红 ≥8px 硬断言，DeepSeek 零渗 ≤4px）】
- [x] 非法颜色值回退默认色不崩溃；重启后保持【已由 CI 代验：场景 G2 两轮启动（"sent:---" 非法值进程存活、品红 ≤4px、默认绿 ≥8px 硬断言）】
- [ ] Success→Error 状态变化有平滑变色过渡（reduced 档）【需装机：reduced 档 300ms 色彩渐变观感判定，本轮未建帧取证（可循 G7 帧差分模式转化）】
- [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调——CI 已代验三半（场景 G7b off 档三帧差分 ≤12px 全静止 + G7c full 档帧差分 32px 呼吸可见（38274507626 实证，轮询取帧对最大差分）+ G6 强度 1.5 落盘）；Critical 脉冲无注入面装机（同上条原因）【需装机（半）】
- [ ] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）——CI 已代验焦点半（场景 H1 前台不变硬断言）；发声与隐藏期动画循环需装机【需装机（半）】
- [ ] 导出文件全文无 token；导入+重录密钥后行为与原机一致——单测半已证（ImportExportTests：导出全文无 token）；导入全流程依赖 WinUI 文件选择器（CI 会话不稳定，此前有先例）需装机【需装机（半）】
- [ ] 全流程录屏/截图归档至 docs/；发现问题全部修复或开 Issue——CI 半已代验（本批 A-H/J 全场景截图归档至 visual-evidence 工件）；真人全流程录屏需装机【需装机（半）】
- [ ] 干净 Windows 11 虚拟机：下载→运行→完成 B-803 脚本【需装机：用户验收终局，CI 是构建证明不是验收替代】
- [x] 调色/动效改动即时预览并持久化，重启生效【已由 CI 代验：场景 G4（改色即时变橙+config 落盘）+ G6（档位/强度落盘）+ G6b（重启后 UIA IsSelected 恢复「关闭」选中态）】
- [x] 重置默认一键还原；off 档预览即全静止【已由 CI 代验：场景 G5（重置后 severityColors 清空 + tile 即时回默认绿）+ G7b（off 档三帧差分 ≤12px）】

## 后续可选（master-plan.md）
- [ ] 厂商私有打包工具 Provider（开源标准 API 的 Jenkins Provider 已落位 2026-10-11；私有厂商待取得真实 API/认证/状态字段再评估，不建常驻 Agent）
- [x] AI Usage Provider——条件门控项已满足并实质完成，关闭（2026-10-11 批3-补41 评估：13 种组件类型/11 家已落地，可靠性三档 + 两次形状漂移均被单文件隔离当日收口；详见 master-plan §13 注记与 positioning §8）

## MVP 验收标准（master-plan §14，需真实装机验证）
- [ ] Windows 登录后按用户设置启动【需装机：真实登录会话的启动链，CI 无登录桌面】
- [ ] 托盘与状态胶囊稳定运行——CI 已代验驻留半（场景 E2/E3 关窗驻留 + Exit 唯一退出）；长时稳定与胶囊观感需装机【需装机（半）】
- [ ] 全局快捷键正常打开悬浮面板【需装机：热键场景未建（同「任意前台热键」条）】
- [ ] 面板展示真实 GitHub 状态【需装机：需真实 PAT 与真实仓库】

验收：CI windows-latest 出截图或给安装包名 + SHA256 让用户自验；「改了未验」不算完成。
