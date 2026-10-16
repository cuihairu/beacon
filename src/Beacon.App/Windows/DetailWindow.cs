using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Windows;

/// <summary>
/// L3 Detail Window（B-603，RFC §6.5）：单对象视图（状态/仓库/分支/时长/错误摘要）
/// + Action 区（Open / Retry / Cancel，按 Widget 能力显隐）。
/// 确认在窗内完成（ContentDialog）后以 RequireConfirmation=false 提交——
/// Runner 的确认安全默认（无 Handler 拒绝执行）仍适用于配置驱动的动作。
/// </summary>
internal sealed class DetailWindow : Window
{
    // DPI 修复单 2026-10-10：设计值一律 DIP，窗尺寸在 Loaded 后按内容实测（DpiLayoutMath 换算）。
    // 本窗此前从未 Resize——WinUI 默认窗尺寸是固定物理像素，200%/250% 下客户区装不下 DIP 内容 →
    // 5K 裁字的直接落点；150%（公司 4K）勉强容下所以「正常」。
    private const double DetailWidthDips = 520;
    private const double DetailMinHeightDips = 320;
    private const double DetailMaxHeightDips = 560;

    private readonly WidgetState _state;
    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly TextBlock _resultText = new()
    {
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
        FontSize = DesignTokens.FontBody,
        TextWrapping = TextWrapping.Wrap,
    };

    public DetailWindow(WidgetState state, BeaconRuntime runtime, Action? closed = null)
    {
        _state = state;
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706

        Title = $"Beacon · {state.Summary}";
        Content = BuildContent();
        ((FrameworkElement)Content).Loaded += OnContentLoaded; // 量内容需视觉树就绪（Activate 前触发，无闪跳）
        Closed += (_, _) => closed?.Invoke();
        Services.AppIcon.Apply(AppWindow, runtime.Logger); // 任务栏/Alt-Tab 图标（unpackaged 不会自动用 exe 图标）
    }

    /// <summary>窗尺寸：客户区 = 宽 DIP 设计值 × DPI、高 = 内容自然高 clamp 到 [min,max]（超出走 ScrollViewer）。
    /// 与胶囊/快捷面板同口径：DIP 只在 Win32 边界经 DpiLayoutMath 换算一次。
    /// AppWindow.Resize 设的是窗口外框（含标题栏/边框），量出来的是客户区需求——必须补回非客户区差值，
    /// 否则 200% 下标题栏物理高度翻倍，客户区比内容矮一截、底部照旧裁字（2026-10-11 装机「没有修复」根因）。</summary>
    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = NativeMethods.GetDpiForWindow(hwnd) / 96.0;
        var content = (FrameworkElement)Content;
        content.Measure(new global::Windows.Foundation.Size(DetailWidthDips, double.PositiveInfinity));
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(hwnd);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32
        {
            Width = DpiLayoutMath.ToPx(DetailWidthDips, dpi) + chromeWidth,
            Height = DpiLayoutMath.ClampedHeightPx(content.DesiredSize.Height, DetailMinHeightDips, DetailMaxHeightDips, dpi) + chromeHeight,
        });
    }

    private UIElement BuildContent()
    {
        // Column 词汇件（Phase 5 行 2 收编调用面；值逐项等价于旧 new StackPanel { Spacing = 10 }）。
        // 间距走 DesignTokens.Spacing 密度收编口（Phase 5 行 5）：regular 恒等（G 系像素锚不动），compact ×0.75
        var density = _runtime.Config.App.Appearance.Density;
        var root = ComponentLayout.Column(DesignTokens.Spacing(10, density));
        root.Padding = new Thickness(18, 16, 18, 16);

        // 头部：severity 点 + 类型 + 摘要
        var header = ComponentLayout.Row(DesignTokens.Spacing(8, density));
        header.Children.Add(ComponentStatus.Indicator(new SolidColorBrush(_palette.SeverityColor(_state.Severity,
            widgetOverride: _runtime.Config.FindWidget(_state.WidgetId)?.ColorOverride))));
        header.Children.Add(new TextBlock
        {
            Text = _state.Summary,
            FontSize = DesignTokens.FontDisplay,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(header);

        var meta = new TextBlock
        {
            Text = $"{_state.WidgetType} · {_state.Severity}"
                   + (_state.Lifecycle is { } lifecycle ? $" · {lifecycle}" : "")
                   + (_state.IsStale ? " · stale" : "")
                   + $" · fetched {_state.FetchedAt.ToLocalTime():HH:mm:ss}",
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
        };
        root.Children.Add(meta);

        // Phase 5 统一附加提示面（WidgetFeedback 纯函数判定，Core 侧单测锚定）：错误/离线/空数据
        // 三态共用 ComponentFeedback 词汇件——此前同一条件各窗口各写一遍且格式不齐
        var feedback = WidgetFeedback.Resolve(_state);
        if (feedback.Kind is FeedbackKind.Offline)
        {
            root.Children.Add(ComponentFeedback.Offline($"{feedback.Headline} · {feedback.Caption}",
                new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
                new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184))));
        }
        else if (feedback.Kind is FeedbackKind.Error)
        {
            root.Children.Add(ComponentFeedback.Error($"{feedback.Headline} · {feedback.Caption}",
                new SolidColorBrush(_palette.SeverityColor(Severity.Error))));
        }

        // Provider 附加数据（仓库/分支/时长/阶段/错误摘要…）——表格件（Phase 5 行 1）：
        // 行物料走 WidgetFeedback.DetailRows 单源（跳过数值位提示键与空白值）
        var muted = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184));
        var foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 226, 232, 240));
        root.Children.Add(ComponentLayout.SectionLabel("DETAILS",
            new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139))));
        var detailRows = WidgetFeedback.DetailRows(_state.Payload);
        root.Children.Add(detailRows.Count == 0
            ? ComponentFeedback.Empty(UiStrings.FeedbackEmpty, muted) // 空数据明示（含只有数值位提示键的情形）
            : ComponentTable.Body(detailRows, muted, foreground));

        // 趋势 sparkline（Phase 5 行 1）：Provider 往 payload 写 TrendSeries.PayloadKey 时画线；
        // 解析不出（缺席/坏值/非有限）恒不画线——宁缺不画错线
        var trend = TrendSeries.Parse(_state.Payload);
        var trendSummary = TrendSeries.Summarize(trend);
        if (trendSummary is not null)
        {
            var direction = trendSummary.Direction switch
            {
                TrendDirection.Up => Severity.Success,
                TrendDirection.Down => Severity.Error,
                _ => Severity.Info,
            };
            var chartRow = ComponentLayout.Row(DesignTokens.Spacing(8, density));
            chartRow.Children.Add(ComponentChart.Sparkline(trend, 180, 32,
                new SolidColorBrush(_palette.SeverityColor(direction))));
            chartRow.Children.Add(ComponentText.Caption(ComponentChart.Caption(trendSummary), muted));
            root.Children.Add(ComponentLayout.SectionLabel("TREND",
                new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139))));
            root.Children.Add(chartRow);
        }

        // 该组件最近事件（Phase 5 行 1 时间线）：通知中心记录按 SourceWidgetId 过滤，
        // 展示序新在前 + 上限走 Core TimelineEntries；无记录整区不出（空态不占行）
        var entries = TimelineEntries.Of(
            _runtime.Notifications.Records
                .Where(record => record.SourceWidgetId == _state.WidgetId)
                .ToList());
        if (entries.Count > 0)
        {
            root.Children.Add(ComponentLayout.SectionLabel(UiStrings.BoardRecentEvents,
                new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139))));
            var timeline = ComponentLayout.Column(DesignTokens.Spacing(6, density));
            foreach (var entry in entries)
            {
                timeline.Children.Add(ComponentTimeline.Row(entry.Time, entry.Title,
                    new SolidColorBrush(_palette.SeverityColor(entry.Severity)), muted, foreground));
            }
            root.Children.Add(timeline);
        }

        // Action 区（按能力显隐，B-603）
        var widget = _runtime.Config.FindWidget(_state.WidgetId);
        var actions = ComponentLayout.Row(DesignTokens.Spacing(8, density));
        actions.Margin = new Thickness(0, 4, 0, 0);

        if (!string.IsNullOrEmpty(_state.DetailUrl))
        {
            actions.Children.Add(ComponentAction.Button("Open", () => RunActionAsync(new ActionConfig
            {
                Id = $"{_state.WidgetId}.open",
                Type = "open.url",
                RequireConfirmation = false, // 打开详情链接无需确认；全局无确认通道，默认 true 会被 Runner 保守拒绝
                Parameters = new Dictionary<string, string> { ["url"] = _state.DetailUrl! },
            })));
        }

        var runId = _state.Payload.GetValueOrDefault("run_id") ?? _state.Payload.GetValueOrDefault("runId");
        if (runId is not null && widget is not null)
        {
            actions.Children.Add(ComponentAction.Button("Retry Failed Jobs", () => RetryAsync(widget, runId)));
            if (_state.Lifecycle is LifecycleState.Running or LifecycleState.Queued)
            {
                actions.Children.Add(ComponentAction.Button("Cancel Run", () => CancelAsync(widget, runId)));
            }
        }

        // 自定义动作库（actions.json，用户令 2026-10-10「局域网打包机就是典型可以触发的」）
        // + DSL 声明动作（positioning §6 批3-补42：连接 dsl_path → actions，带 connection_id 限定）：
        // 显隐统一走 ActionVisibility——widgetType 支持逗号分隔多值，connection_id 限定所属连接
        foreach (var candidate in _runtime.Config.Actions.Concat(_runtime.DslActions))
        {
            if (!ActionVisibility.IsVisible(candidate.Parameters, _state.WidgetType, _state.ConnectionId))
            {
                continue;
            }
            var custom = candidate;
            actions.Children.Add(ComponentAction.Button(custom.Name ?? custom.Id, () => RunCustomAsync(custom)));
        }
        if (actions.Children.Count > 0)
        {
            root.Children.Add(ComponentLayout.SectionLabel("ACTIONS",
                new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139))));
            root.Children.Add(actions);
        }

        root.Children.Add(_resultText);
        // UI/UX 审计：Auto 档滚动条不显眼，底部还有 ACTIONS/结果反馈这件事无提示——常驻 Visible
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
    }

    private async Task RetryAsync(WidgetConfig widget, string runId)
    {
        if (!await ConfirmAsync($"重跑 {widget.Config.GetValueOrDefault("repo")} 的 run {runId} 失败 job？"))
        {
            return;
        }
        await RunActionAsync(new ActionConfig
        {
            Id = $"{_state.WidgetId}.rerun",
            Type = "gh.workflow_rerun",
            RequireConfirmation = false, // 确认已在窗内完成（ContentDialog）；全局无确认通道
            Parameters = CloneWidgetParams(widget),
        });
    }

    private async Task CancelAsync(WidgetConfig widget, string runId)
    {
        if (!await ConfirmAsync($"取消 {widget.Config.GetValueOrDefault("repo")} 的 run {runId}？"))
        {
            return;
        }
        await RunActionAsync(new ActionConfig
        {
            Id = $"{_state.WidgetId}.cancel",
            Type = "gh.workflow_cancel",
            RequireConfirmation = false, // 确认已在窗内完成（ContentDialog）；全局无确认通道
            Parameters = CloneWidgetParams(widget),
        });
    }

    /// <summary>动作库动作执行：确认（如需）在窗内 ContentDialog 完成，落 Runner 前摘除确认标记。</summary>
    private async Task RunCustomAsync(ActionConfig action)
    {
        if (action.RequireConfirmation
            && !await ConfirmAsync($"执行「{action.Name ?? action.Id}」（{action.Type}）？"))
        {
            return;
        }
        await RunActionAsync(new ActionConfig
        {
            Id = action.Id,
            Type = action.Type,
            Name = action.Name,
            RequireConfirmation = false, // 确认已在窗内完成
            Parameters = new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase),
        });
    }

    private static Dictionary<string, string> CloneWidgetParams(WidgetConfig widget)
        => new(widget.Config, StringComparer.OrdinalIgnoreCase); // repo/workflow/branch 直通执行器模板

    private async Task RunActionAsync(ActionConfig action)
    {
        _resultText.Text = "执行中…";
        try
        {
            var result = await _runtime.Actions.ExecuteAsync(action, new ActionExecutionContext { SourceState = _state });
            _resultText.Text = $"{(result.Success ? "✓" : "✗")} {result.Message}";
        }
        catch (Exception exception)
        {
            _resultText.Text = $"✗ {exception.Message}";
        }
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "确认操作",
            Content = message,
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
