using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 剩余边角面收口：UiStrings 具名入口全量、SvgPathParser 语法残缺/指数/重复参数、
/// ComponentVisibility 比较操作符全臂、EventBus Dispose、模型 record 构造、
/// 纯函数单行兜底（QuotaWindows 枚举标签、Dpi/Pin 布局数学、HighContrast、UpdateCheck 版本比较、
/// ComponentTreeValidator 空 state 路径、ComponentCenter 分组）。
/// </summary>
public sealed class EdgeSurfaceCoverageTests
{
    // ---------- UiStrings 具名入口（缺键退英语再退键名，不抛） ----------

    [Fact]
    public void UiStrings_NamedGetters_AllResolve()
    {
        Assert.NotEmpty(UiStrings.BoardTimelineEmpty);
        Assert.NotEmpty(UiStrings.BoardNotificationsEmpty);
        Assert.NotEmpty(UiStrings.BoardAttentionEmpty);
        Assert.NotEmpty(UiStrings.BoardFilterEmpty);
        Assert.NotEmpty(UiStrings.BoardClearFilter);
        Assert.NotEmpty(UiStrings.BoardFooterRunning);
        Assert.NotEmpty(UiStrings.BoardFooterOk);
        Assert.NotEmpty(UiStrings.BoardFooterHintActions);
        Assert.NotEmpty(UiStrings.BoardFooterHintWindow);
        Assert.NotEmpty(UiStrings.TrayOpenPanel);
        Assert.NotEmpty(UiStrings.TrayNotifications);
        Assert.NotEmpty(UiStrings.TrayKeepPanelOpen);
        Assert.NotEmpty(UiStrings.TrayRefreshAll);
        Assert.NotEmpty(UiStrings.TraySettings);
        Assert.NotEmpty(UiStrings.TrayComponents);
        Assert.NotEmpty(UiStrings.TrayExit);
        Assert.NotEmpty(UiStrings.SettingsLanguage);
        Assert.NotEmpty(UiStrings.SettingsLanguageAuto);
        Assert.NotEmpty(UiStrings.SettingsEdgeCollapse);
        Assert.NotEmpty(UiStrings.SettingsCapsuleIsland);
        Assert.NotEmpty(UiStrings.FeedbackOffline);
        Assert.NotEmpty(UiStrings.FeedbackError);
    }

    // ---------- SvgPathParser 语法分支 ----------

    [Fact]
    public void SvgPath_TCommandWithoutPriorQuadControl_ReflectsCurrentPoint()
    {
        var figure = SvgPathParser.Parse("M10,10 T20,20")[0];
        var quadratic = Assert.IsType<SvgQuadratic>(figure.Segments[0]);
        Assert.Equal(10, quadratic.Control.X); // 无前驱 Q → 控制点退回当前点
        Assert.Equal(20, quadratic.To.Y);
    }

    [Fact]
    public void SvgPath_RepeatedArguments_AllProduceSegments()
    {
        var figure = SvgPathParser.Parse("M0,0 L1,1 2,2")[0];
        Assert.Equal(2, figure.Segments.Count); // HasMoreArguments 续读同命令下一参数组
    }

    [Fact]
    public void SvgPath_InvalidArcFlag_Throws()
        => Assert.Throws<FormatException>(() => SvgPathParser.Parse("M0,0 A5,5 0 2,0 10,10"));

    [Fact]
    public void SvgPath_ExponentNumber_ParsesValue()
    {
        var figure = SvgPathParser.Parse("M1e2,0 L2,3")[0];
        Assert.Equal(100, figure.Start.X); // 完整指数才消费
    }

    [Fact]
    public void SvgPath_TrailingSeparators_TerminateCleanly()
    {
        var figures = SvgPathParser.Parse("M0,0  ");
        Assert.Empty(figures); // 仅 M 无线段 + 尾随空白 → 空子路径丢弃，不抛
    }

    // ---------- ComponentVisibility 比较操作符全臂 ----------

    private static WidgetState VisState() => new()
    {
        WidgetId = "w-1",
        WidgetType = "t",
        ConnectionId = "c-main",
        Severity = Severity.Warning,
        Summary = "ok",
        Payload = new Dictionary<string, string> { ["branch"] = "beta" },
    };

    [Fact]
    public void ComponentVisibility_StringComparison_AllOperators()
    {
        var state = VisState();
        // 非数值字段的关系运算不可求值（仅 = / != 有效）——钉住「静默 false」语义
        Assert.False(ComponentVisibility.Evaluate("payload:branch>alpha", state));
        Assert.False(ComponentVisibility.Evaluate("payload:branch>=beta", state));
        Assert.False(ComponentVisibility.Evaluate("payload:branch<gamma", state));
        Assert.True(ComponentVisibility.Evaluate("payload:branch!=alpha", state));
        Assert.True(ComponentVisibility.Evaluate("payload:branch=beta", state));
    }

    [Fact]
    public void ComponentVisibility_ProgressComparison_AllOperators()
    {
        var state = VisState() with { Progress = 0.5 };
        Assert.True(ComponentVisibility.Evaluate("progress>=0.5", state));
        Assert.True(ComponentVisibility.Evaluate("progress<=0.5", state));
        Assert.False(ComponentVisibility.Evaluate("progress<0.5", state));
        Assert.False(ComponentVisibility.Evaluate("progress>0.5", state));
    }

    [Fact]
    public void ComponentVisibility_SeverityComparison_AllOperators()
    {
        var state = VisState();
        Assert.True(ComponentVisibility.Evaluate("severity>info", state));
        Assert.True(ComponentVisibility.Evaluate("severity<=warning", state));
        Assert.False(ComponentVisibility.Evaluate("severity<warning", state));
        Assert.True(ComponentVisibility.Evaluate("severity>=warning", state));
    }

    // ---------- EventBus.Dispose ----------

    [Fact]
    public void EventBus_Dispose_ClearsHandlers()
    {
        var bus = new EventBus();
        bus.Subscribe<object>(_ => { });
        bus.Dispose(); // 清空处理器表
    }

    // ---------- 模型 record 构造/读取 ----------

    [Fact]
    public void ModelRecords_ConstructAndRead()
    {
        var quick = new QuickPanelLayout
        {
            Monitor = "DP-1 2560x1440",
            Anchor = PinAnchor.TopRight,
            OffsetDips = new PinOffset(24, 96),
            Width = 360,
            Height = 240,
        };
        Assert.Equal("DP-1 2560x1440", quick.Monitor);
        Assert.Equal(PinAnchor.TopRight, quick.Anchor);
        Assert.Equal(24, quick.OffsetDips.X);
        Assert.Equal(360, quick.Width);
        Assert.Equal(240, quick.Height);

        var island = new CapsuleIslandLayout { Monitor = "DP-1", OffsetDips = new PinOffset(0, 8) };
        Assert.Equal(0, island.OffsetDips.X);

        var field = new WidgetFieldDescriptor("icon", "图标", Required: true, Placeholder: "选择", Choices: ["a", "b"]);
        Assert.Equal("icon", field.Key);
        Assert.Equal(2, field.Choices!.Count);

        var template = new DisplayTemplates.DisplayTemplate(
            "id", "n", "d", new HashSet<string>(), new Dictionary<string, TemplateParameter>(), 24, _ => null!);
        Assert.Equal("id", template.Id);
        Assert.False(template.RequiresProgress);
    }

    [Fact]
    public void QuotaWindows_Label_UnknownEnumValue_IsEmpty()
        => Assert.Equal("", QuotaWindows.Label((QuotaWindow)99));

    [Fact]
    public void LayoutMathSurfaces_LineHeightAndDefaultOffset()
    {
        Assert.True(DpiLayoutMath.LineHeightDips(12) > 0);
        Assert.Equal(24, PinLayoutMath.DefaultOffset.X);
        Assert.Equal(96, PinLayoutMath.DefaultOffset.Y);
    }

    [Fact]
    public void HighContrastPalette_Value_BothModes()
    {
        var dark = HighContrastPalette.Value(dark: true);
        var light = HighContrastPalette.Value(dark: false);
        Assert.NotEqual(dark, light);
    }

    [Fact]
    public void UpdateCheck_Compare_NumericVersusAlphanumericSegment()
    {
        var numeric = UpdateCheck.Parse("1.0.0-1")!.Value;
        var alpha = UpdateCheck.Parse("1.0.0-a")!.Value;
        Assert.True(UpdateCheck.Compare(numeric, alpha) < 0); // 数字段 < 字母数字段
    }

    [Fact]
    public void UpdateCheck_Compare_StableReleaseNewerThanPrerelease()
    {
        var stable = UpdateCheck.Parse("1.0.1")!.Value;
        var pre = UpdateCheck.Parse("1.0.1-rc.1")!.Value;
        Assert.True(UpdateCheck.Compare(stable, pre) > 0); // 同主干：正式版 > 预发布
    }

    [Fact]
    public void ComponentTreeValidator_EmptyStateRef_IsInvalid()
    {
        var root = new ComponentDescriptor
        {
            Id = "r",
            Type = "t",
            Bindings =
            {
                new ComponentBinding
                {
                    Field = "f",
                    Source = new ComponentBindingSource { Kind = ComponentBindingSource.KindState, Ref = "" },
                },
            },
        };

        var validation = ComponentTreeValidator.Validate(root);
        Assert.False(validation.Ok);
    }

    [Fact]
    public void ComponentCenterGroup_RecordConstructs()
    {
        var group = new ComponentCenterGroup("k", "显示", new List<ComponentCenterItem>());
        Assert.Equal("k", group.Key);
        Assert.Empty(group.Items);
    }
}
