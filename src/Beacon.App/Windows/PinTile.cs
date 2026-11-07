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

        // 显示路径：bar=底部线性进度条（默认，2026-10-11 用户令翻转）；pool=圆池注水（旧特效，显式选择）。
        // 组件级 widgets.json config.progressStyle 可覆盖外观设置（向后兼容）；加载中=bar 空轨/空池环+「加载中」
        var style = widget.Config.GetValueOrDefault("progressStyle");
        if (string.IsNullOrEmpty(style))
        {
            style = _motion.ProgressStyle;
        }
        _barMode = string.Equals(style, "bar", StringComparison.OrdinalIgnoreCase);
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
            Visibility = _barMode ? Visibility.Collapsed : Visibility.Visible,
        };
        _waveHost.Children.Add(_poolFill);
        _waveHost.Children.Add(_poolRing);
        _progressText.Text = "加载中"; // 首态静态可读量：首个状态事件到达即被 NN% 覆盖
        _progressText.Visibility = Visibility.Visible;

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

        // 主行 + 底部进度条叠放（见 _barTrack/_barFill 注释）：单 Grid 三子，后加者在上层；
        // 主内容竖直居中（进度读数字号居 32 DIP，下部留白 ≥6 DIP），与 4 DIP 条互不碰撞。
        var container = new Grid();
        container.Children.Add(grid);
        container.Children.Add(_barTrack);
        container.Children.Add(_barFill);
        container.SizeChanged += (_, e) => ApplyBarGeometry(e.NewSize.Width); // 布局实测后重放比例
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

    /// <summary>进度两显示路径（2026-10-11 用户令重设计，默认翻转）：
    /// ① bar（默认）——底部 4 DIP 圆角线性条：track 35% 透明底轨 + fill 档位色
    ///   （≥80% warning、≥98% error 红——用户「满了就变成另一颜色」），推进平滑过渡；
    /// ② pool（可选，显式选择）——16DIP 圆池水位=进度（Path 圆弓形=水位线以下整块），满格变 severity 色；
    /// ③ percent——「NN%」静态文本常驻（动画+文字双通道，截图/肉眼硬口径，永远可读）。
    /// 无进度语义的类型全收（value 已带可读量）。</summary>
    private void UpdateProgress(WidgetState state, global::Windows.UI.Color color)
    {
        if (state.Progress is not { } progress)
        {
            _waveHost.Visibility = Visibility.Collapsed;
            _progressText.Visibility = Visibility.Collapsed;
            CollapseBar();
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
            // ① bar 默认路径：显式像素矩形（CI 已证渲染），档位配色，池收
            _waveHost.Visibility = Visibility.Collapsed;
            _barFill.Fill = new SolidColorBrush(BarFillColor(fraction, color));
            _barTrack.Visibility = Visibility.Visible;
            _barFill.Visibility = Visibility.Visible;
            _barFraction = fraction;
            ApplyBarGeometry(_root.ActualWidth - _root.Padding.Left - _root.Padding.Right);
        }
        else
        {
            // ② pool 可选路径（旧特效，显式选择）：水位=进度（≥2 DIP 保底可辨），Fill=severity 色
            // （满格 Error 红同口径）；bar 收
            CollapseBar();
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

    /// <summary>收起为细条（宿主 ApplyLayout 判定）：暂停全部动画循环省电；展开复位。</summary>
    public void SuspendMotion()
    {
        if (_suspended)
        {
            return;
        }
        _suspended = true;
        _motion.StopLoops(_light);
        _motion.StopLoops(_root);
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
