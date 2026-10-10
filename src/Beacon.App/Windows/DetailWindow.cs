using Beacon.App.Services;
using Beacon.Core.Models;
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
    private readonly WidgetState _state;
    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly TextBlock _resultText = new()
    {
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
    };

    public DetailWindow(WidgetState state, BeaconRuntime runtime, Action? closed = null)
    {
        _state = state;
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706

        Title = $"Beacon · {state.Summary}";
        Content = BuildContent();
        Closed += (_, _) => closed?.Invoke();
        Services.AppIcon.Apply(AppWindow, runtime.Logger); // 任务栏/Alt-Tab 图标（unpackaged 不会自动用 exe 图标）
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Spacing = 10, Padding = new Thickness(18, 16, 18, 16) };

        // 头部：severity 点 + 类型 + 摘要
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new Ellipse
        {
            Width = 10,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(_palette.SeverityColor(_state.Severity,
                widgetOverride: _runtime.Config.FindWidget(_state.WidgetId)?.ColorOverride)),
        });
        header.Children.Add(new TextBlock
        {
            Text = _state.Summary,
            FontSize = 16,
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
            FontSize = 12,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
        };
        root.Children.Add(meta);

        // Provider 附加数据（仓库/分支/时长/阶段/错误摘要…）
        if (_state.Payload.Count > 0)
        {
            root.Children.Add(SectionLabel("DETAILS"));
            var table = new StackPanel { Spacing = 3 };
            foreach (var pair in _state.Payload)
            {
                table.Children.Add(new TextBlock
                {
                    Text = $"{pair.Key}  {pair.Value}",
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 226, 232, 240)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
            root.Children.Add(table);
        }

        // Action 区（按能力显隐，B-603）
        var widget = _runtime.Config.FindWidget(_state.WidgetId);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        if (!string.IsNullOrEmpty(_state.DetailUrl))
        {
            actions.Children.Add(ActionButton("Open", () => RunActionAsync(new ActionConfig
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
            actions.Children.Add(ActionButton("Retry Failed Jobs", () => RetryAsync(widget, runId)));
            if (_state.Lifecycle is LifecycleState.Running or LifecycleState.Queued)
            {
                actions.Children.Add(ActionButton("Cancel Run", () => CancelAsync(widget, runId)));
            }
        }

        // 自定义动作库（actions.json，用户令 2026-10-10「局域网打包机就是典型可以触发的」）：
        // Parameters.widgetType 留空 = 全组件显示；填了则精确匹配当前组件类型
        foreach (var candidate in _runtime.Config.Actions)
        {
            if (candidate.Parameters.TryGetValue("widgetType", out var scope) && scope.Length > 0
                && !string.Equals(scope, _state.WidgetType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var custom = candidate;
            actions.Children.Add(ActionButton(custom.Name ?? custom.Id, () => RunCustomAsync(custom)));
        }
        if (actions.Children.Count > 0)
        {
            root.Children.Add(SectionLabel("ACTIONS"));
            root.Children.Add(actions);
        }

        root.Children.Add(_resultText);
        // UI/UX 审计：Auto 档滚动条不显眼，底部还有 ACTIONS/结果反馈这件事无提示——常驻 Visible
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
        Margin = new Thickness(0, 6, 0, 0),
    };

    private Button ActionButton(string label, Func<Task> run)
    {
        var button = new Button { Content = label, Padding = new Thickness(12, 6, 12, 6) };
        button.Click += (_, _) => _ = run();
        return button;
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
