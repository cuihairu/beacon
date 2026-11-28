using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// L0 悬浮 tile（B-702，RFC §6.2.1）：StatusLight + 标签 + 数字/摘要，32 DIP 高。
/// 组合而非派生（WinUI 3 的 Border 是 sealed，CS0509）；输入事件由宿主挂到 Root；
/// 状态变化只更新自身视觉。
/// </summary>
internal sealed class PinTile
{
    private const int LightSizeDips = 8;
    private const int PoolSize = 16;  // 额度圆池直径（DIP）
    private const double PoolRadius = PoolSize / 2.0;

    // —— 底部线性进度条（2026-10-11 重设计，替代「进度条看不到」的 3px 旧版）——
    // 4 DIP 高 + 2 DIP 圆角（整条胶囊形）：3px 在 100% 缩放下细到难以辨认（用户实测「进度条看不到」），
    // 提到 4 DIP 并在两端圆角，track 35% 透明底轨 + fill 档位色，暗亮双主题都可辨。
    private const int BarHeightDips = 4;
    private const double BarRadiusDips = 2;
    // 档位阈值（≥80% warning / ≥98% error）下沉 Core ProgressTiers 单源（Phase 2），此处不再持有
    private const double BarMinFillDips = 2; // 极小进度保底可辨

    // —— 横灌注水桶（AQUA-001 横向改版，2026-11-17 用户令「我说的注水桶应该是横着的」「就是整个就是注水桶」）——
    // 整个 tile 本身=横放的水桶：水（档位配色）从左往右灌满全高桶身，水位前沿=竖直线（frontX=进度×桶宽），
    // 前沿双层正弦波（Core AquariumWave 折线轴交换）composition 纵向漂移出活水感。波幅按桶高度自适应
    // clamp(桶高/8, 3, 6) DIP（下限 3=CI 列采样波峰可证下限）、周期按桶宽自适应 clamp(桶宽/3, 28, 56)
    // DIP，漂移 2.2s/3.4s 一周期（AQUA-002 提速），另加 1 DIP 高光线 6s 间歇扫过；motion off 档全静止
    // （B-707 纪律，Core WavesAnimate 门）。
    private const double AquariumWaveSkirtDips = 4; // 波形裙边（没入水体侧，盖住与水体的接缝，trough 1.6 DIP 内无漏缝）

    /// <summary>tile 高（DIP）：max(尺寸档, 模板最小高)——带模板树时按模板防裁切放大
    /// （评审 P2 裁切：数字卡 48/圆环 34 等），无树恒等尺寸档（TileSizing 单源；常规 32 恒等）。</summary>
    public int TileHeightDips { get; private set; }

    /// <summary>进度百分比字号随 tile 高度分级（固定 12 会在任何 tile 尺寸变化后裁字——
    /// 5K/200% 裁字同族缺陷；DIP 恒定比例，高缩放下 XAML 按新 DPI 等比放大不裁）。</summary>
    private double ProgressFontDips => Math.Round(TileHeightDips * 0.375, 1);

    /// <summary>圆池水位几何（池水=圆形池底部圆弓形，水位=进度）：满格整圆、空池不画。
    /// 用折线近似圆弧（14 段 LineSegment，弧上点三角函数显式算出）而非 ArcSegment——
    /// SweepDirection/IsLargeArc 方向歧义在 CI 上才会现形（本地 WinUI 不可编译），折线零歧义。
    /// Point 全限定 global::Windows.Foundation.Point：WinUI3 无 Microsoft.UI.Xaml.Point 投影
    /// （8b94469 CS0246 实证）。</summary>
    internal static Geometry PoolGeometry(double fraction)
    {
        if (fraction >= 0.999)
        {
            return new EllipseGeometry
            {
                Center = new global::Windows.Foundation.Point(PoolRadius, PoolRadius),
                RadiusX = PoolRadius,
                RadiusY = PoolRadius,
            };
        }
        if (fraction <= 0.001)
        {
            return null;
        }
        var waterY = PoolSize * (1.0 - fraction); // 水位线 y（DIP，向下为正）
        var offset = waterY - PoolRadius;          // 水位线相对圆心纵距
        var halfChord = Math.Sqrt(Math.Max(0.0, PoolRadius * PoolRadius - offset * offset));
        var edgeAngle = Math.Asin(Math.Clamp(offset / PoolRadius, -1, 1));
        var figure = new PathFigure
        {
            // 起点取右侧交点（θ=edgeAngle），沿底部圆弧扫到左侧交点（θ=π-edgeAngle），闭合线即水面
            StartPoint = new global::Windows.Foundation.Point(PoolRadius + halfChord, waterY),
            IsClosed = true,
        };
        const int segments = 14;
        for (var i = 1; i <= segments; i++)
        {
            var angle = edgeAngle + (Math.PI - 2.0 * edgeAngle) * i / segments;
            figure.Segments.Add(new LineSegment
            {
                Point = new global::Windows.Foundation.Point(
                    PoolRadius + PoolRadius * Math.Cos(angle),
                    PoolRadius + PoolRadius * Math.Sin(angle)),
            });
        }
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private WidgetConfig _widget; // 非只读（P3-01 RefreshConfig：配置替换语义下换新对象）
    private readonly UiPalette _palette;
    private readonly MotionEngine _motion;
    private WidgetState? _lastState;
    private readonly string? _connectionType; // 传统面品牌图标用（BuildTraditionalContent，非 ctor 局部）

    /// <summary>该面声明式实例（P3-01）：widgets.json 带 tile 组件树且结构合法时非空——数据槽位
    /// 走渲染器（外围灯/品牌图标/标签保留，评审 P0 槽位化替换）；非法树 Resolve 已回 null
    /// （传统渲染，诊断在加载侧），清空配置即恢复旧面。</summary>
    private WidgetInstance? _declarative;
    /// <summary>声明式数据槽位宿主（评审 P0 槽位化替换）：null=当前是传统六列面——
    /// RenderDeclarativeFace 据此分流（槽位在位换槽内容，不在位整面替换后由 RefreshConfig 重建）。</summary>
    private Grid? _declarativeHost;
    private Severity? _lastSeverity;
    private global::Windows.UI.Color? _lastFillColor; // 上次状态灯着色——变色过渡只在色变 tick 重放，同色 tick 直接填充不闪灯
    private bool _suspended; // 收起态暂停动画循环（B-707 验收：隐藏/收起无动画循环）

    private readonly Border _root = new()
    {
        Padding = new Thickness(10, 0, 10, 0), // Height 在构造器按尺寸档设（TileSizing.TileHeightDips 单源）
        // 实色主题底（对比度可判定）：此前 12% 透明深灰 scrim 叠在窗口/桌面上——
        // 浅主题 + 浅桌面底时近白 icon 必然发虚（2026-10-09 用户实测「根本看不清」根因）
        Background = new SolidColorBrush(ThemeColors.Surface()),
    };
    private readonly Ellipse _light = new()
    {
        Width = 8,
        Height = 8,
        Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)),
        StrokeThickness = 2,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _label = new()
    {
        Foreground = new SolidColorBrush(ThemeColors.Label()),
        FontSize = DesignTokens.FontBody,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _value = new()
    {
        Foreground = new SolidColorBrush(ThemeColors.Value()),
        FontSize = DesignTokens.FontCaption,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right,
    };
    // 额度数值卡进度条（http.quota/bigmodel.usage 等 Progress 语义）：tile 底部 4 DIP 线性条 + NN% 读数。
    // 2026-10-10 重构（用户实测「进度条看不到」，CI 截图证实前代 Star 列设计从未渲染出任何像素）：
    // 弃嵌套 Star 列 Grid，改两枚显式 Rectangle——贴底 margin 0、宽度由 container.SizeChanged 按实测像素
    // 设置，结构上不存在「布局余量被吃掉整条消失」路径；track=Value 色 35% 透明（双主题都可辨的底轨），
    // fill=档位色。比例缓存进 _barFraction，SizeChanged 到达即重放（首次 Update 时 ActualWidth 可能还是 0，不丢）。
    // 2026-10-11 重设计（用户令「现在的进度特效太丑了…不要现在这个圆圈的效果」）：3px→4 DIP + 2 DIP 圆角
    // （3px 在 100% 下细到难辨），颜色随档位（≥0.8 warning / ≥0.98 error 红），NN% 读数常驻双通道；
    // 推进过渡走 Storyboard+DoubleAnimation(Width)（intensity 接管时长，off 档瞬时），不再依赖圆池/涟漪效果。
    private readonly Rectangle _barTrack = new()
    {
        Height = BarHeightDips,
        VerticalAlignment = VerticalAlignment.Bottom,
        Fill = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
        RadiusX = BarRadiusDips,
        RadiusY = BarRadiusDips,
        Visibility = Visibility.Collapsed,
    };
    private readonly Rectangle _barFill = new()
    {
        Height = BarHeightDips,
        VerticalAlignment = VerticalAlignment.Bottom,
        HorizontalAlignment = HorizontalAlignment.Left,
        RadiusX = BarRadiusDips,
        RadiusY = BarRadiusDips,
        Visibility = Visibility.Collapsed,
    };
    private Storyboard? _barStoryboard; // 字段持有：每次推进先 Stop 上段（HoldEnd 动画值会压住本地新值）
    private double _barFraction;

    // —— 额度显示（2026-10-11 用户令「进度特效太丑了…不要现在这个圆圈的效果」重设计）——
    // 两显示路径并存，默认 bar（旧配置显式 "pool" 保持不动）：
    // ① bar（默认）=底部 4 DIP 圆角线性条（track+fill，档位配色）——推进过渡，不再依赖任何圆/波纹效果
    // ② pool（可选）=16 DIP 圆池，水位=进度（圆环内底部填充），满格变 severity 色
    // ③ _progressText「NN%」常驻静态可读量——动画+文字双通道，截图和一眼扫都能拿到状态（用户硬口径）。
    // 池边描边与百分比统一 ThemeColors.Label()（主题令牌对比度最高的前景色，暗亮双主题均 ≥4.5:1）。
    // 三者无字段初始化器、构建主体在 BuildTraditionalContent 赋值——readonly 须去（方法内赋 readonly 是 CS0191）
    private Ellipse _poolRing;
    private global::Microsoft.UI.Xaml.Shapes.Path _poolFill;
    private Grid _waveHost;
    private readonly TextBlock _progressText = new()
    {
        Foreground = new SolidColorBrush(ThemeColors.Label()),
        // FontSize 在构造器按尺寸档设（ProgressFontDips = tile 高×0.375，字段初始化器不能引实例属性）
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };
    private bool _barMode; // 非只读：构建主体抽到 BuildTraditionalContent（方法内不得赋 readonly，CS0191）

    // —— 水族箱件（AQUA-001）——与 pool 件同纪律：无字段初始化器、构建主体 BuildTraditionalContent 赋值
    //（方法内赋 readonly 是 CS0191）；模式恒构建、可见性在 UpdateProgress 按模式与有无进度门。
    private Grid _aquariumHost;
    private Rectangle _aquariumTrack;
    private Rectangle _aquariumWater;
    private global::Microsoft.UI.Xaml.Shapes.Path _waveBack;
    private global::Microsoft.UI.Xaml.Shapes.Path _waveFront;
    private Rectangle _waveGlint; // AQUA-002：1 DIP 水面高光线（6s 间歇扫过，composition 内插）
    private double _frontPeriod = AquariumWave.FrontPeriodDips; // 自适应周期工作值（ApplyAquariumGeometry 按桶宽重算）
    private double _frontAmp = AquariumWave.FrontAmplitudeDips; // 自适应振幅工作值（按桶高重算；StartWaves 漂移行程与之同源）
    private double _glintTravel; // 高光扫过行程=桶高（ApplyAquariumGeometry 实测赋值；0=未测量不启动）
    private Storyboard? _aquariumStoryboard; // 字段持有：水位推进先 Stop 上段（HoldEnd 动画值压本地新值，bar 同口径）
    private double _aquariumFraction;
    private bool _aquariumMode; // 非只读同 _barMode（构建主体赋值）
    private bool _aquariumWavesRunning; // composition 波纹在跑标记（Stop 幂等）

    /// <summary>宿主面板挂载与输入挂接的根元素。</summary>
    public Border Root => _root;

    public string WidgetId { get; }

    /// <summary>状态查询（P3-03 依赖路由）：宿主把最新状态表（快照口径）交给面——声明式面的
    /// 附加源绑定按 widgetId 从这里取，不触发数据请求；null = 只用自身状态（P3-01 切片语义）。</summary>
    public Func<string, WidgetState?>? StateLookup { get; set; }

    /// <summary>本 tile 推算宽（DIP）：所有类型同一路径逐列字宽预算（TileSizing——数值列按各类型最坏
    /// ValueHint 文本、machine 值行按所选展示项构造），面板列宽取钉选 tile 的最大值，值行恒不裁
    /// （2026-10-12 用户令二修：真字宽预算扩到全部 tile）。</summary>
    public int TileWidthDips { get; }

    public PinTile(WidgetConfig widget, UiPalette palette, MotionEngine motion, string? connectionType = null)
    {
        WidgetId = widget.Id;
        TileWidthDips = TileSizing.TileWidthDips(widget);
        _widget = widget;
        _palette = palette;
        _motion = motion;
        _connectionType = connectionType;

        // 声明式 tile（P3-01）：带合法 tile 树时数据槽位走渲染器，外围（灯/品牌图标/标签）保留
        // （评审 P0 槽位化替换：换样式不丢「这是哪个源」的上下文）；非法树 Resolve 已回 null（传统渲染）。
        _declarative = WidgetInstanceResolver.Resolve(widget, "tile");
        // 高度 = max(尺寸档, 模板最小高)（评审 P2 裁切）：数字卡 48/圆环 34 等按需放大，无树恒等尺寸档
        TileHeightDips = EffectiveTileHeight(widget, _declarative);
        _root.Height = TileHeightDips;
        _progressText.FontSize = ProgressFontDips; // 随 tile 高度分级（高×0.375），尺寸档变化后不裁字
        if (_declarative is not null)
        {
            BuildSlottedContent();
            return;
        }
        BuildTraditionalContent();
    }

    /// <summary>tile 有效高（评审 P2 裁切）：声明式树经 DisplayTemplates.MinTileHeightDipsOf 反解
    /// 模板身份取最小高（自定义树 0 不放大），与尺寸档取大——「模板要的高」和「用户选的档」都满足。</summary>
    private static int EffectiveTileHeight(WidgetConfig widget, WidgetInstance? declarative)
    {
        var tier = TileSizing.TileHeightDips(widget); // 尺寸档（Config["sizeTier"]，常规 32 恒等）
        var templateMin = (int)Math.Ceiling(DisplayTemplates.MinTileHeightDipsOf(declarative?.Root));
        return Math.Max(tier, templateMin);
    }

    /// <summary>声明式槽位面构建（评审 P0 槽位化替换）：外围三列（灯/品牌图标/标签——与传统面同源
    /// 同位，「这是哪个源」的上下文换样式不丢）+ 数据槽位（Star 列，模板树渲染进 _declarativeHost）。
    /// 池/条/进度文本等传统专属件不建（模板树自带进度表现）；灯照常随状态驱动（Update 声明式分支）。</summary>
    private void BuildSlottedContent()
    {
        var widget = _widget;
        _label.Text = LabelOf(widget);
        var icon = BrandIconFactory.TryCreate(_connectionType, 16, ThemeColors.Label(), iconOverride: widget.Config.GetValueOrDefault("icon"), strokeWidth: 1)
            ?? new FontIcon
            {
                Glyph = "\uE774", // Segoe Fluent World：通用数据源兜底（与 BuildTraditionalContent 同口径）
                FontSize = DesignTokens.FontTitle,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(ThemeColors.Label()),
            };
        _declarativeHost = new Grid();
        // 四列 = 灯/图标/标签/数据槽位（Auto×3 + Star）。2026-11-08 真机探针（数字卡模板 tile）实证：
        // 此前只给三列定义（Auto/Auto/Star）却 SetColumn(3)——WinUI Grid 列序超出定义数时钳到最后一列，
        // 数据槽位与标签同格叠印（大数字直接压在标签字上）；传统面六列定义齐全无此错。
        var grid = ComponentLayout.Columns(DesignTokens.Spacing(6, _palette.Density), GridLength.Auto, GridLength.Auto,
            GridLength.Auto, new GridLength(1, GridUnitType.Star));
        Grid.SetColumn(_light, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(_label, 2);
        Grid.SetColumn(_declarativeHost, 3);
        grid.Children.Add(_light);
        grid.Children.Add(icon);
        grid.Children.Add(_label);
        grid.Children.Add(_declarativeHost);
        _root.Child = grid;
        if (_declarative is { } declarative)
        {
            // 首帧自身状态单源渲染（_lastState 此时尚空）：StateLookup 还没挂（对象初始化器在构造后
            // 执行），带源渲染会把未到齐的附加源误报缺失态——首帧后宿主重放状态即换带源渲染（P3-03）。
            RenderDeclarativeFace(declarative);
        }
    }

    /// <summary>传统六列 tile 构建（灯/品牌图标/标签/值/池或条/进度文本）——原构造主体，
    /// 声明式切换（RefreshConfig）与恢复共用。</summary>
    private void BuildTraditionalContent()
    {
        var widget = _widget;
        _label.Text = LabelOf(widget);
        _value.Text = "加载中…"; // 空白即 bug：首态也要有信息（首个状态事件到达即被覆盖）

        // 数据源品牌图标（用户令：悬浮框必须带对应 icon，不然分不清哪个悬浮框是哪个源）：
        // 组件级自选图标（widget.Config["icon"]）优先 → BrandIcons 连接品牌命中 → 通用 globe 字形兜底
        // 前景主题感知（浅底深字/深底亮字，≥4.5:1）——近白 icon 在浅底上不可辨（用户实测「根本看不清」）
        // 2026-10-10 修「icon 太浅看不清」：14→16 DIP + 同色 1px 描边增重（细线几何覆盖率 7-22% 是根因，非填色）；
        // 兜底字形同步提到 Label() 前景 + 14px，与品牌图同档可读
        var icon = BrandIconFactory.TryCreate(_connectionType, 16, ThemeColors.Label(), iconOverride: widget.Config.GetValueOrDefault("icon"), strokeWidth: 1)
            ?? new FontIcon
            {
                Glyph = "\uE774", // Segoe Fluent World：通用数据源兜底
                FontSize = DesignTokens.FontTitle,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(ThemeColors.Label()),
            };

        // 显示路径：bar=底部线性进度条（默认，2026-10-11 用户令翻转）；pool=圆池注水（旧特效，显式选择）；
        // aquarium=整行水族箱（AQUA-001，2026-11-15 用户令，显式选择）。
        // 组件级 widgets.json config.progressStyle 可覆盖外观设置（向后兼容）；组件级值与外观值同过
        // Core ProgressStyles 三值解析（非法一律落 bar）。加载中=bar 空轨/空池环+「加载中」（水族箱加载中不显带）
        var style = widget.Config.GetValueOrDefault("progressStyle");
        if (string.IsNullOrEmpty(style))
        {
            style = _motion.ProgressStyle;
        }
        style = ProgressStyles.Resolve(style);
        _barMode = string.Equals(style, ProgressStyles.Bar, StringComparison.Ordinal);
        _aquariumMode = string.Equals(style, ProgressStyles.Aquarium, StringComparison.Ordinal);
        var labelBrush = new SolidColorBrush(ThemeColors.Label());
        _poolRing = new Ellipse { Width = PoolSize, Height = PoolSize, Stroke = labelBrush, StrokeThickness = 1.5 };
        // 池水填充：Path 圆弓形（水位线以下整块）。UIElement.Clip 在 WinUI/UWP 只接受 RectangleGeometry——
        // 赋 EllipseGeometry 编译失败（8b94469 实证），故走 Path.Data 圆弓形，无 Clip 依赖
        _poolFill = new global::Microsoft.UI.Xaml.Shapes.Path
        {
            Width = PoolSize,
            Height = PoolSize,
            Fill = labelBrush,
        };
        _waveHost = new Grid
        {
            Width = PoolSize,
            Height = PoolSize,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = _barMode || _aquariumMode ? Visibility.Collapsed : Visibility.Visible,
        };
        _waveHost.Children.Add(_poolFill);
        _waveHost.Children.Add(_poolRing);
        _progressText.Text = "加载中"; // 首态静态可读量：首个状态事件到达即被 NN% 覆盖
        _progressText.Visibility = Visibility.Visible;

        // 注水桶层（AQUA-001 横向版）：全高桶身，叠层自底向上=桶壁（35% Value 冷色，空桶身内壁）
        // → 水体（档位色，左锚全高，Width=已灌宽）→ 后波（45% 深度感）→ 前波（实色）。
        // Host 裁切用 RectangleGeometry（WinUI Clip 只收矩形，8b94469 同口径）——波路径比前沿长出一个
        // 漂移周期、领先沿宽出半幅，必须裁掉桶身外行程。构建恒在（与 pool 件同口径），可见性按模式门。
        _aquariumTrack = new Rectangle
        {
            Fill = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
            Visibility = Visibility.Collapsed,
        };
        _aquariumWater = new Rectangle
        {
            Width = 0, // 空桶：首个进度事件按 fraction 落前沿
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
        };
        // 波层（pool Path 同款已证形态：显式宽高 + 全非负几何 + 零变换——负 Y 几何/无 Height 的 Path
        // 在 CI 实证整件不渲染，43729489131）。位置烘进几何（前沿随 fraction 重建即到）；漂移只走
        // composition Offset.Y（off 档不启动=静止；CI 会话不可证时波形静止在场，观感转装机目检）。
        _waveBack = new global::Microsoft.UI.Xaml.Shapes.Path
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Opacity = 0.45,
            Visibility = Visibility.Collapsed,
        };
        _waveFront = new global::Microsoft.UI.Xaml.Shapes.Path
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        // AQUA-002 水面高光：1 DIP 横亮线骑在水面上，随波纵向扫过、6s 间歇（其余时间 Opacity 0 隐没）。
        // 宽度=水体已灌宽（ApplyAquariumGeometry 同步），白色低透明不抢进度色。构建期 Collapsed 同波层。
        _waveGlint = new Rectangle
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = new SolidColorBrush(SeverityPalette.Rgb(150, 255, 255, 255)),
            Visibility = Visibility.Collapsed,
        };
        _aquariumHost = new Grid
        {
            IsHitTestVisible = false, // 纯装饰层：不吃输入（tile 点击面归主行）
            Visibility = Visibility.Collapsed,
        };
        _aquariumHost.Children.Add(_aquariumTrack);
        _aquariumHost.Children.Add(_aquariumWater);
        _aquariumHost.Children.Add(_waveBack);
        _aquariumHost.Children.Add(_waveFront);
        _aquariumHost.Children.Add(_waveGlint); // 最上层：高光压波面

        var grid = ComponentLayout.Columns(DesignTokens.Spacing(6, _palette.Density), GridLength.Auto, GridLength.Auto,
            new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto);
        Grid.SetColumn(_light, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(_label, 2);
        Grid.SetColumn(_value, 3);
        Grid.SetColumn(_waveHost, 4);
        Grid.SetColumn(_progressText, 5);
        grid.Children.Add(_light);
        grid.Children.Add(icon);
        grid.Children.Add(_label);
        grid.Children.Add(_value);
        grid.Children.Add(_waveHost);
        grid.Children.Add(_progressText);

        // 主行 + 注水桶层 + 底部进度条叠放：单 Grid 多子，后加者在上层——注水桶先加（最底层，铺满
        // 全高桶身），文字行压其上（横灌后高进度水面漫过文字区，文字在上层仍可读——漫水可读性装机
        // 亲验面）；bar 两矩形最后加（贴底 4 DIP 不与文字碰）。
        var container = new Grid();
        container.Children.Add(_aquariumHost);
        container.Children.Add(grid);
        container.Children.Add(_barTrack);
        container.Children.Add(_barFill);
        container.SizeChanged += (_, e) =>
        {
            ApplyBarGeometry(e.NewSize.Width); // 布局实测后重放比例
            ApplyAquariumGeometry(e.NewSize.Width, e.NewSize.Height); // 桶身几何同步重放（AQUA-001）
        };
        _root.Child = container;
    }

    /// <summary>配置对象刷新（P3-01）：UpsertWidget 是替换语义，宿主 ReloadTiles 把新 WidgetConfig
    /// 交给已存 tile——树变更下一帧即重渲生效；无树→有树、有树→无树（清空配置回传统面）都在此收口。
    /// 声明式在位时高度随模板重算（评审 P2 裁切：换更高模板即放大行高，不用等重建）。</summary>
    public void RefreshConfig(WidgetConfig widget)
    {
        _widget = widget;
        var declarative = WidgetInstanceResolver.Resolve(widget, "tile");
        if (declarative is null && _declarative is null)
        {
            return; // 双方都无声明树：传统面无动作（传统元素已建）
        }
        _declarative = declarative;
        if (declarative is not null)
        {
            var height = EffectiveTileHeight(widget, declarative);
            if (_declarativeHost is null || height != TileHeightDips)
            {
                // 传统→声明式首切（槽位未建），或模板变了导致目标高变了：重建槽位面（外围随新标签/图标一起挂）
                TileHeightDips = height;
                _root.Height = TileHeightDips;
                BuildSlottedContent();
            }
            else
            {
                RenderDeclarativeFace(declarative); // 同高换树/换参数：只换数据槽内容
            }
        }
        else
        {
            // 清空配置恢复旧面：重建传统六列（含 SizeChanged 几何回调重挂），高度回尺寸档
            _declarativeHost = null;
            TileHeightDips = EffectiveTileHeight(widget, null);
            _root.Height = TileHeightDips;
            BuildTraditionalContent();
        }
    }

    /// <summary>
    /// 状态变化只更新本 tile（B-701）；离线/错误降级（B-704，RFC §6.2.6）：
    /// ConnectionHealthy=false → 灰空心灯 + Last update HH:mm，绝不弹异常；恢复后健康事件自动复位。
    /// 着色走 UiPalette（B-706：健康灯应用 Widget 的 colorOverride；offline 是连接健康派生态，
    /// 恒走 appearance.SeverityColors["offline"]，不吃 Widget 覆盖——灰要始终可辨）。
    /// 动效走 MotionEngine（B-707）：变色过渡/Critical 脉冲（reduced 默认）、
    /// full 档呼吸 + 变化闪烁；收起态 SuspendMotion 暂停循环。
    /// 声明式面（P3-01）走整树重渲分支：动效与灯组语义由绑定档位色承担，tooltip 口径同传统。
    /// </summary>
    public void Update(WidgetState state)
    {
        _lastState = state;
        if (_declarative is { } declarative)
        {
            // 声明式分支（P3-01/P3-03）：数据槽整树重渲（≤256 节点、无跨帧局部状态；附加源经
            // StateLookup 取宿主快照）+ 沿用传统面的陈旧/离线 tooltip 口径；动效（闪烁/脉冲/呼吸）
            // 暂不作用于声明式树，色彩语义由绑定档位色承担。
            // 外围灯照常随状态驱动（评审 P0 槽位化替换：外围保留且活——含 colorOverride 与离线灰空心）
            if (state.ConnectionHealthy)
            {
                _light.Fill = new SolidColorBrush(_palette.SeverityColor(state.Severity, widgetOverride: _widget.ColorOverride));
                _light.Stroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            else
            {
                _light.Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                _light.Stroke = new SolidColorBrush(_palette.SeverityColor(state.Severity, offline: true)); // Offline 灰
            }
            RenderDeclarativeFace(declarative);
            ToolTipService.SetToolTip(_root,
                !state.ConnectionHealthy
                    ? UiStrings.FeedbackOffline + " · " + WidgetFeedback.LastUpdate(state.FetchedAt, withSeconds: true)
                    : state.IsStale ? WidgetFeedback.LastUpdate(state.FetchedAt, withSeconds: true) : null);
            return;
        }
        if (state.ConnectionHealthy)
        {
            var changed = _lastSeverity is { } previous && previous != state.Severity;
            _lastSeverity = state.Severity;

            if (!_suspended)
            {
                ApplyLoops(state.Severity);
            }

            var color = _palette.SeverityColor(state.Severity, widgetOverride: _widget.ColorOverride);
            // 变色过渡只属「色变」：同色 tick 直接填充——此前每 tick 都重放 0→1 淡入，
            // reduced 档下状态灯每个刷新周期凭空灭一次再渐亮（违反「换色后亮度渐入」本意）。
            var colorChanged = !_lastFillColor.HasValue || !_lastFillColor.Value.Equals(color);
            _lastFillColor = color;
            var breathingOwnsOpacity = !_suspended
                && _motion.Mode == MotionMode.Full && state.Severity != Severity.Critical;
            if (_suspended || breathingOwnsOpacity || !colorChanged)
            {
                _light.Fill = new SolidColorBrush(color); // 呼吸循环正占 Opacity / 色未变，直接换色不打断循环
            }
            else
            {
                _motion.TransitionFill(_light, color); // ① 变色过渡（off 档内部退化为瞬时）
            }

            _light.Stroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _value.Text = ValueOf(state);
            UpdateProgress(state, color);
            ToolTipService.SetToolTip(_root,
                state.IsStale ? WidgetFeedback.LastUpdate(state.FetchedAt, withSeconds: true) : null);

            if (changed && !_suspended)
            {
                _motion.Flash(_root); // ② 提醒闪烁（full 档内部自闸）
            }
        }
        else
        {
            _lastSeverity = null; // 降级后恢复视为新状态（闪烁重新可触发）
            _lastFillColor = null; // 灰空心灯→实色灯的恢复算色变，恢复时同样走过渡渐入
            _light.Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _light.Stroke = new SolidColorBrush(_palette.SeverityColor(state.Severity, offline: true)); // Offline 灰
            _value.Text = WidgetFeedback.LastUpdate(state.FetchedAt);
            _waveHost.Visibility = Visibility.Collapsed;
            _progressText.Visibility = Visibility.Collapsed;
            CollapseBar(); // 先停推进动画再收条（HoldEnd 值压本地值）
            CollapseAquarium(); // 离线降级同收水族箱带（波纹停、水位清）
            ToolTipService.SetToolTip(_root,
                UiStrings.FeedbackOffline + " · " + WidgetFeedback.LastUpdate(state.FetchedAt, withSeconds: true));
            _motion.StopLoops(_light);
            _motion.StopLoops(_root);
        }
    }

    /// <summary>声明式面渲染统一入口（P3-03）：附加源经 ResolveSources 从 StateLookup 取宿主快照；
    /// 未接 lookup 回自身状态单源（P3-01 切片语义不变）。缺席源由渲染器出缺失态，不冒充空值。
    /// 槽位化替换（评审 P0）：只换数据槽内容（_declarativeHost），外围灯/图标/标签不动；
    /// 宿主不在位（防御：构建顺序异常）退整面替换。</summary>
    private void RenderDeclarativeFace(WidgetInstance declarative)
    {
        var lookup = StateLookup;
        var sources = WidgetInstanceResolver.ResolveSources(declarative, lookup
            ?? (id => string.Equals(id, declarative.Id, StringComparison.Ordinal) ? _lastState : null));
        var rendered = ComponentSurfaceRenderer.Render(declarative, sources, _palette);
        if (_declarativeHost is { } host)
        {
            host.Children.Clear();
            host.Children.Add(rendered);
        }
        else
        {
            _root.Child = rendered;
        }
    }

    /// <summary>该面是否消费此 widget 的状态（P3-03 依赖路由）：传统面只认自身；声明式面按
    /// ComponentSourceIndex 全集（自身 + Sources 声明的附加源）——无关源变化不触达本面。</summary>
    public bool ConsumesWidget(string widgetId)
        => _declarative is { } instance
            ? ComponentSourceIndex.ConsumedWidgetIds(instance).Contains(widgetId)
            : string.Equals(WidgetId, widgetId, StringComparison.Ordinal);

    /// <summary>被消费源变化而自身状态未变（P3-03 依赖路由）：只重渲声明式面；传统面无附加源语义，无动作。</summary>
    public void RefreshSources()
    {
        if (_declarative is { } declarative)
        {
            RenderDeclarativeFace(declarative);
        }
    }

    /// <summary>③/④ 循环落位：Critical 脉冲（reduced 起）优先，full 档非 Critical 走呼吸灯。</summary>
    private void ApplyLoops(Severity severity)
    {
        _motion.StopLoops(_light);
        if (severity == Severity.Critical)
        {
            _motion.StartPulse(_light, LightSizeDips);
        }
        else if (_motion.Mode == MotionMode.Full)
        {
            _motion.StartBreathing(_light);
        }
    }

    /// <summary>进度三显示路径（2026-10-11 用户令重设计 bar 默认；AQUA-001 2026-11-15 增第三档、
    /// 2026-11-17 用户令横向改版「整个就是注水桶」）：
    /// ① bar（默认）——底部 4 DIP 圆角线性条：track 35% 透明底轨 + fill 档位色
    ///   （≥80% warning、≥98% error 红——用户「满了就变成另一颜色」），推进平滑过渡；
    /// ② pool（可选，显式选择）——16DIP 圆池水位=进度（Path 圆弓形=水位线以下整块），满格变 severity 色；
    /// ③ aquarium（可选，显式选择）——整个 tile=横放水桶：水从左往右灌满全高桶身，水位前沿=竖直线
    ///   （frontX=进度×桶宽）、前沿双层正弦波活水（composition 纵向漂移，off 档静止），档位配色同 bar；
    /// ④ percent——「NN%」静态文本常驻（动画+文字双通道，截图/肉眼硬口径，永远可读）。
    /// 无进度语义的类型全收（value 已带可读量）。</summary>
    private void UpdateProgress(WidgetState state, global::Windows.UI.Color color)
    {
        if (state.Progress is not { } progress)
        {
            _waveHost.Visibility = Visibility.Collapsed;
            _progressText.Visibility = Visibility.Collapsed;
            CollapseBar();
            CollapseAquarium();
            return;
        }

        var fraction = Math.Clamp(progress, 0, 1);
        _progressText.Text = $"{Math.Round(fraction * 100)}%";
        // 多窗口额度（bigmodel/ark 三窗全拼）：数值文本已含主位窗口百分比，读数重复占宽，收起
        // （2026-10-12 用户令；单窗形态维持常驻，与 tile 宽预算同口径——TileSizing 不预算该列）
        _progressText.Visibility = WidgetValueHint.SuppressProgressReadout(state)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (_barMode)
        {
            // ① bar 默认路径：显式像素矩形（CI 已证渲染），档位配色，池/箱收
            _waveHost.Visibility = Visibility.Collapsed;
            CollapseAquarium();
            _barFill.Fill = new SolidColorBrush(BarFillColor(fraction, color));
            _barTrack.Visibility = Visibility.Visible;
            _barFill.Visibility = Visibility.Visible;
            _barFraction = fraction;
            ApplyBarGeometry(_root.ActualWidth - _root.Padding.Left - _root.Padding.Right);
        }
        else if (_aquariumMode)
        {
            // ③ aquarium 路径（AQUA-001 横向版）：桶身显形，水从左往右灌（frontX=进度×桶宽，
            // 档位配色 BarFillColor 同 bar 合流），波纹纵向漂移（off 档 StartWaves 内部直接不启动=静止）；池收
            CollapseBar();
            _waveHost.Visibility = Visibility.Collapsed;
            var fill = BarFillColor(fraction, color);
            _aquariumWater.Fill = new SolidColorBrush(fill);
            _waveFront.Fill = new SolidColorBrush(fill);
            _waveBack.Fill = new SolidColorBrush(fill);
            _aquariumTrack.Visibility = Visibility.Visible;
            // 水体与双层波逐件翻开（构建期 Collapsed，只翻 host/track=桶里只剩桶壁——run 43720846043
            // 三态带像素逐行采样实证：12 行全 (62,72,91) 零水体。host 收时子件随隐，显形须逐件翻）
            _aquariumWater.Visibility = Visibility.Visible;
            _waveBack.Visibility = Visibility.Visible;
            _waveFront.Visibility = Visibility.Visible;
            _waveGlint.Visibility = Visibility.Visible;
            _aquariumHost.Visibility = Visibility.Visible;
            _aquariumFraction = fraction;
            ApplyAquariumGeometry(
                _root.ActualWidth - _root.Padding.Left - _root.Padding.Right,
                _root.ActualHeight - _root.Padding.Top - _root.Padding.Bottom);
            StartWaves();
        }
        else
        {
            // ② pool 可选路径（旧特效，显式选择）：水位=进度（≥2 DIP 保底可辨），Fill=severity 色
            // （满格 Error 红同口径）；bar/箱收
            CollapseBar();
            CollapseAquarium();
            _waveHost.Visibility = Visibility.Visible;
            _poolFill.Data = PoolGeometry(fraction);
            _poolFill.Fill = new SolidColorBrush(color);
        }
    }

    /// <summary>收掉 bar 路径（无进度语义/切池）：先停推进动画（HoldEnd 值压本地值），再收矩形。</summary>
    private void CollapseBar()
    {
        _barFraction = 0;
        _barStoryboard?.Stop();
        _barTrack.Visibility = Visibility.Collapsed;
        _barFill.Visibility = Visibility.Collapsed;
    }

    /// <summary>收掉注水桶路径（无进度/离线/切其他样式）：停前沿推进与波纹漂移，整层收起。</summary>
    private void CollapseAquarium()
    {
        _aquariumFraction = 0;
        _aquariumStoryboard?.Stop();
        StopWaves();
        if (_aquariumHost is not null)
        {
            _aquariumHost.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>波纹漂移起停（AQUA-001 横向版，漂移轴=纵向沿前沿流动）：off 档 StartRipple 内部直接
    /// 不启动（静止）；运行标记幂等，SuspendMotion（收起态省电）与 CollapseAquarium 走同一 Stop。</summary>
    private void StartWaves()
    {
        if (_aquariumWavesRunning)
        {
            return;
        }
        _motion.StartRipple(_waveFront, _frontPeriod, forward: false, vertical: true); // 行程=一个自适应周期（无缝循环）
        _motion.StartRipple(_waveBack, AquariumWave.BackPeriodDips, forward: true, vertical: true); // 反向+异周期=视差活水
        _motion.StartGlint(_waveGlint, _glintTravel); // AQUA-002：高光随波轴间歇扫过（off 档内部不启动）
        _aquariumWavesRunning = true;
    }

    private void StopWaves()
    {
        if (!_aquariumWavesRunning)
        {
            return;
        }
        _motion.StopRipple(_waveFront, vertical: true);
        _motion.StopRipple(_waveBack, vertical: true);
        _motion.StopGlint(_waveGlint);
        _aquariumWavesRunning = false;
    }

    /// <summary>注水桶几何（AQUA-001 横向版，bar 同口径防抖）：桶身=容器实测全高全宽，水从左往右灌
    /// ——已灌宽=fraction×桶宽（≥2 DIP 保底可辨，Storyboard 平滑过渡 off 档瞬时），水位前沿=竖直线
    /// frontX=已灌宽。波层=竖直前沿条：路径局部 X=位移（+A+skirt 平移进非负域），局部 Y=前沿纵向
    /// 位置（Core Crest 以跨度=桶高+漂移周期采样，轴交换在 VerticalWaveFigure）；Margin=
    /// (frontX−A−skirt, 0)——领先沿绝对位=frontX+位移恰绕前沿线振荡（43738359562 教训同源：定位烘进
    /// 几何会把整条波淹没进同色水体）。Host 裁到桶身矩形——波层纵向行程与横向领先幅全裁
    /// （RectangleGeometry 唯一合法 Clip）。</summary>
    private void ApplyAquariumGeometry(double innerWidth, double innerHeight)
    {
        if (_aquariumHost is null || _aquariumHost.Visibility != Visibility.Visible
            || double.IsNaN(innerWidth) || innerWidth < 8 || double.IsNaN(innerHeight) || innerHeight < 8)
        {
            return;
        }

        var tankWidth = Math.Floor(innerWidth);
        var tankHeight = Math.Floor(innerHeight);
        _aquariumHost.Clip = new RectangleGeometry
        {
            Rect = new global::Windows.Foundation.Rect(0, 0, tankWidth, tankHeight),
        };
        _aquariumTrack.Width = tankWidth;
        _aquariumTrack.Height = tankHeight;
        _aquariumWater.Height = tankHeight; // 水体全高（推进走 Width，高度不参与动画）
        var fillWidth = _aquariumFraction <= 0.001 ? 0 : Math.Max(BarMinFillDips, Math.Floor(tankWidth * _aquariumFraction));
        var frontX = fillWidth; // 水位前沿（竖直线，与水体右缘同一钳位）

        _aquariumStoryboard?.Stop(); // 上段还在 HoldEnd：不先停会压住本地新值（bar 同坑）
        var from = _aquariumWater.Width;
        _aquariumWater.Width = fillWidth; // 本地值=目标（动画结束 HoldEnd 与其重合，无跳变）
        var duration = _motion.BarTransitionDuration;
        if (duration > TimeSpan.Zero && double.IsFinite(from) && from != fillWidth)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = fillWidth,
                Duration = new Duration(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, _aquariumWater);
            Storyboard.SetTargetProperty(animation, "Width");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            _aquariumStoryboard = storyboard;
            storyboard.Begin();
        }

        // 波形重建（点数 ~40，重建廉价；前沿/桶高随 SizeChanged/fraction 重放同到）：Core 周期对齐
        // 采样（末点落周期整倍数且 ≥跨度=桶高+周期）→ 纵向平移一周期波形自重合=无缝循环。几何局部
        // X=位移+A+skirt 全非负（pool「显式宽高+非负几何」同口径），前沿定位走 Margin=frontX−A−skirt
        // ——领先沿绝对位=frontX+位移恰绕前沿线振荡。漂移=composition Offset.Y 平移 [0,−P]（前层）/
        // [−P,0]（后层反向）全程覆盖前沿纵向。
        // AQUA-002 自适应：波幅 clamp(桶高/8, 3, 6)、周期 clamp(桶宽/3, 28, 56)——桶度量驱动波感，
        // 下限 3 守 CI 列采样可证性。工作值落字段：StartWaves 漂移行程必须与本几何同周期（无缝循环）。
        _frontAmp = AquariumWave.AdaptiveAmplitudeDips(tankHeight);
        _frontPeriod = AquariumWave.AdaptivePeriodDips(tankWidth);
        _glintTravel = tankHeight;
        _waveGlint.Width = Math.Max(fillWidth, 0); // 高光骑在已灌水面上（空桶宽 0=不可见）
        var frontCrest = AquariumWave.Crest(
            tankHeight + _frontPeriod, _frontPeriod, _frontAmp, phaseTurns: 0);
        _waveFront.Width = 2 * _frontAmp + AquariumWaveSkirtDips;
        _waveFront.Height = frontCrest[^1].X; // 轴交换：crest 的 X（前沿纵向位置）落路径局部 Y 域
        _waveFront.Margin = new Thickness(frontX - _frontAmp - AquariumWaveSkirtDips, 0, 0, 0);
        _waveFront.Data = VerticalWaveFigure(frontCrest, _frontAmp);
        var backCrest = AquariumWave.Crest(
            tankHeight + AquariumWave.BackPeriodDips, AquariumWave.BackPeriodDips, AquariumWave.BackAmplitudeDips, AquariumWave.BackPhaseTurns);
        _waveBack.Width = 2 * AquariumWave.BackAmplitudeDips + AquariumWaveSkirtDips;
        _waveBack.Height = backCrest[^1].X;
        _waveBack.Margin = new Thickness(frontX - AquariumWave.BackAmplitudeDips - AquariumWaveSkirtDips, 0, 0, 0);
        _waveBack.Data = VerticalWaveFigure(backCrest, AquariumWave.BackAmplitudeDips);
    }

    /// <summary>竖直前沿波形 PathGeometry（Core AquariumWave 折线轴交换 → XAML 几何，PoolGeometry 同
    /// 纪律：折线零歧义）。crest 的 X=前沿纵向位置 → 路径局部 Y；crest 的 Y=位移 → 局部 X+amplitudeDips
    /// +skirt 平移进非负域（负 X 几何配 Pool 已证形态风险不做）。右缘=正弦前沿（领先沿），左缘=前沿线
    /// 左 skirt 深度直线（没入水体侧，盖住与水体的接缝）；纵向两端落在漂移行程带外（Clip 裁掉）。</summary>
    private static Geometry VerticalWaveFigure((double X, double Y)[] crest, double amplitudeDips)
    {
        double LocalX(double displacement) => displacement + amplitudeDips + AquariumWaveSkirtDips;
        var figure = new PathFigure
        {
            StartPoint = new global::Windows.Foundation.Point(LocalX(crest[0].Y), crest[0].X),
            IsClosed = true,
        };
        for (var i = 1; i < crest.Length; i++)
        {
            figure.Segments.Add(new LineSegment { Point = new global::Windows.Foundation.Point(LocalX(crest[i].Y), crest[i].X) });
        }
        var last = crest[^1];
        var first = crest[0];
        // 裙边：前沿线左移 skirt 深度（水体侧）沿纵向两端直落——填充域盖住 crest 与水体的接缝
        figure.Segments.Add(new LineSegment { Point = new global::Windows.Foundation.Point(0, last.X) });
        figure.Segments.Add(new LineSegment { Point = new global::Windows.Foundation.Point(0, first.X) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>进度条档位配色：≥98% 满格 error 红、≥80% 接近满 warning、其余用组件 severity 色
    /// （正常档保持与状态灯一致； quota 组件 severity 本就随进度分档，两套口径在这里合流）。</summary>
    private global::Windows.UI.Color BarFillColor(double fraction, global::Windows.UI.Color normalColor)
        => ProgressTiers.Level(fraction) is { } tier ? _palette.SeverityColor(tier) : normalColor; // 档位单一来源 Core ProgressTiers（Phase 2）

    /// <summary>按容器实测宽设条几何：track 全宽、fill=比例宽（≥2 DIP 保底可辨）；
    /// 未布局（宽 0）或已隐藏时跳过——SizeChanged 会带着实测宽再进来。
    /// 进度推进用 Storyboard+DoubleAnimation(Width) 平滑过渡（时长随动效档 intensity，off 档瞬时直设；
    /// WinUI 无 LayoutTransaction，Storyboard 动 Width 是唯一不改视觉树的布局动画口）。
    /// 首次显示（旧宽非有限值，如未布局的 NaN）直接落位，不播从 0 涨的假动画。</summary>
    private void ApplyBarGeometry(double innerWidth)
    {
        if (_barTrack.Visibility != Visibility.Visible || double.IsNaN(innerWidth) || innerWidth < 8)
        {
            return;
        }

        var trackWidth = Math.Floor(innerWidth);
        var fillWidth = Math.Max(BarMinFillDips, Math.Floor(trackWidth * _barFraction));
        if (_barTrack.Width == trackWidth && _barFill.Width == fillWidth)
        {
            return; // 目标没变不重排（重放/SizeChanged 抖动防抖）
        }
        _barTrack.Width = trackWidth;
        _barStoryboard?.Stop(); // 上段还在 HoldEnd：不先停，动画值会压住下面的本地新值
        var from = _barFill.Width;
        _barFill.Width = fillWidth; // 本地值=目标（动画结束后 HoldEnd 值与其重合，无跳变）
        var duration = _motion.BarTransitionDuration;
        if (duration > TimeSpan.Zero && double.IsFinite(from) && from != fillWidth)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = fillWidth,
                Duration = new Duration(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, _barFill);
            Storyboard.SetTargetProperty(animation, "Width");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            _barStoryboard = storyboard;
            storyboard.Begin();
        }
    }

    /// <summary>收起为细条（宿主 ApplyLayout 判定）：暂停全部动画循环省电（含水族箱波纹，AQUA-001）；展开复位。</summary>
    public void SuspendMotion()
    {
        if (_suspended)
        {
            return;
        }
        _suspended = true;
        _motion.StopLoops(_light);
        _motion.StopLoops(_root);
        StopWaves(); // composition 漂移同样在收起态停（波形定格不消失）
    }

    public void ResumeMotion()
    {
        if (!_suspended)
        {
            return;
        }
        _suspended = false;
        if (_lastState is { ConnectionHealthy: true } state)
        {
            var color = _palette.SeverityColor(state.Severity, widgetOverride: _widget.ColorOverride);
            ApplyLoops(state.Severity);
            UpdateProgress(state, color); // 池水/bar 按最近进度重放
        }
    }

    /// <summary>⑤ 滑入：tile 出现在未收起的展开态时调用（full 档）。</summary>
    public void PlaySlideIn() => _motion.SlideIn(_root);

    /// <summary>tile 标签：优先用户配的显示名（label），再取自然名（GitHub repo/owner），
    /// 否则取连接类型短名（WidgetTypeNames）。旧兜底取类型尾段——ark.usage tile 显示成「usage」，
    /// 字段名冒充了显示名（2026-10-09 用户实测悬浮框名字错误）；未知前缀才退尾段。</summary>
    private static string LabelOf(WidgetConfig widget)
        => ComponentCenterCatalog.ItemLabel(widget); // 标签口径 Core 单源（组件中心条目同名）

    /// <summary>数值位短文本：映射规则在 Core（WidgetValueHint，Linux 可单测），此处仅委托。</summary>
    private static string ValueOf(WidgetState state) => WidgetValueHint.Of(state);
}
