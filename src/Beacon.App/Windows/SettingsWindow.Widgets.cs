using System.Globalization;
using System.Reflection;
using System.Threading;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;

namespace Beacon.App.Windows;

internal sealed partial class SettingsWindow
{
    // —— 组件：列表 + 向导（描述符驱动动态字段） ——

    private UIElement BuildWidgetEditor()
    {
        _widgetTypeBox = new ComboBox { Header = "类型", Width = 220 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_widgetTypeBox, "widget-type-box"); // UIA 取证：类型下拉非空断言（小米类型空回归）
        foreach (var descriptor in _scopeDescriptors ?? AllWidgetDescriptors())
        {
            _widgetTypeBox.Items.Add(new ComboBoxItem { Content = descriptor.DisplayName, Tag = descriptor.Type });
        }
        if (_widgetTypeBox.Items.Count > 0)
        {
            _widgetTypeBox.SelectedIndex = 0;
        }
        _widgetTypeBox.SelectionChanged += (_, _) => RebuildWidgetFields();
        _widgetConnectionBox = new ComboBox { Header = "连接", Width = 200 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_widgetConnectionBox, "widget-connection-box"); // UIA 取证：展开截可选项
        _widgetTierBox = new ComboBox { Header = "刷新档", Width = 180 };
        // UI/UX 审计：下拉里是原始英文 tag（pr/ci/…），含义与频率要用户猜——中文标签带刷新秒数，Tag 仍存档位名
        foreach (var (tier, label) in new[]
        {
            (RefreshTiers.Pr, "pr · 60 秒（PR 轮询）"),
            (RefreshTiers.Ci, "ci · 15 秒（CI 检查）"),
            (RefreshTiers.Machine, "machine · 15 秒（机器状态）"),
            (RefreshTiers.Agent, "agent · 10 秒（Agent 会话）"),
            (RefreshTiers.Workflow, "workflow · 10 秒（工作流）"),
            (RefreshTiers.Static, "static · 手动刷新"),
            (RefreshTiers.Default, "default · 60 秒（通用默认）"),
        })
        {
            _widgetTierBox.Items.Add(new ComboBoxItem { Content = label, Tag = tier });
        }
        // 检测间隔覆盖（拍板：频率要可设）——不选=按刷新档默认；秒数落 WidgetConfig.RefreshIntervalSeconds
        _widgetIntervalBox = new ComboBox { Header = "检测间隔（可选）", Width = 150 };
        foreach (var (tag, label) in new[] { ("", "跟随刷新档"), ("30", "30 秒"), ("60", "1 分钟"), ("120", "2 分钟"), ("300", "5 分钟"), ("900", "15 分钟"), ("3600", "1 小时") })
        {
            var item = new ComboBoxItem { Content = label, Tag = tag };
            _widgetIntervalBox.Items.Add(item);
        }
        _widgetIntervalBox.SelectedIndex = 0;

        _widgetFields = new StackPanel { Spacing = 8 };
        _widgetColorBox = new TextBox { Header = "颜色覆盖（可选 #RRGGBB，作用于状态灯）", PlaceholderText = "#3fb950", Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        // 验收反馈：向导保存即上板——默认勾选钉到桌面，避免「加了组件板面却空白」（同白板三连根因收口）
        _widgetPinBox = new ToggleSwitch { Header = "添加到桌面（钉选）", IsOn = true };

        var save = new Button { Content = "添加组件" };
        save.Click += (_, _) => SaveWidget();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _widgetTypeBox, _widgetConnectionBox, _widgetTierBox, _widgetIntervalBox } });
        form.Children.Add(_widgetFields);
        form.Children.Add(_widgetColorBox);
        form.Children.Add(_widgetPinBox);
        form.Children.Add(save);
        var root = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = DesignTokens.RadiusCard,
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
        RebuildWidgetFields();
        RefreshWidgetConnectionOptions();
        return root;
    }

    private void RebuildWidgetFields()
    {
        _widgetFields.Children.Clear();
        var descriptor = SelectedWidgetDescriptor();
        if (descriptor is null)
        {
            return;
        }
        foreach (var field in descriptor.Fields)
        {
            if (field.Choices is { } choices)
            {
                // 选择字段（icon 自选，用户令 2026-10-10）：下拉而非自由文本；首项=默认（按连接品牌）
                var picker = new ComboBox
                {
                    Header = field.DisplayName,
                    Tag = field.Key,
                    Width = 280,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                picker.Items.Add(new ComboBoxItem { Content = field.Placeholder ?? "默认", Tag = "" });
                foreach (var choice in choices)
                {
                    picker.Items.Add(new ComboBoxItem { Content = choice, Tag = choice });
                }
                picker.SelectedIndex = 0;
                _widgetFields.Children.Add(picker);
                continue;
            }
            _widgetFields.Children.Add(new TextBox
            {
                Header = field.DisplayName + (field.Required ? "（必填）" : ""),
                PlaceholderText = field.Placeholder ?? "",
                Tag = field.Key,
                Width = 280,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        _widgetColorBox.Text = "";
        var tier = descriptor.SuggestedTier;
        var tierIndex = IndexOfTag(_widgetTierBox, tier);
        _widgetTierBox.SelectedIndex = tierIndex >= 0 ? tierIndex : _widgetTierBox.Items.Count - 1;
    }

    private void RefreshWidgetConnectionOptions()
    {
        // 常规/外观/高级页无组件编辑器（_widgetConnectionBox 未构建）——导入等路径会调到，空守卫防 NRE
        if (_widgetConnectionBox is null)
        {
            return;
        }
        var previous = TagOf(_widgetConnectionBox);
        _widgetConnectionBox.Items.Clear();
        foreach (var connection in _runtime.Config.Connections.Where(c => _scopeType is null || c.Type == _scopeType))
        {
            _widgetConnectionBox.Items.Add(new ComboBoxItem { Content = connection.Id, Tag = connection.Id });
        }
        if (_widgetConnectionBox.Items.Count > 0)
        {
            _widgetConnectionBox.SelectedIndex = Math.Max(0, IndexOfTag(_widgetConnectionBox, previous ?? _runtime.Config.Connections[0].Id));
        }
    }

    private void SaveWidget()
    {
        var descriptor = SelectedWidgetDescriptor();
        if (descriptor is null)
        {
            Feedback(_widgetFeedback, "✗ 请选择组件类型。", error: true);
            return;
        }
        if (_widgetConnectionBox.SelectedItem is null)
        {
            Feedback(_widgetFeedback, "✗ 请先添加一个连接。", error: true);
            return;
        }

        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in _widgetFields.Children.OfType<TextBox>())
        {
            var key = child.Tag?.ToString() ?? "";
            if (child.Text.Trim().Length > 0)
            {
                config[key] = child.Text.Trim();
            }
        }
        foreach (var child in _widgetFields.Children.OfType<ComboBox>())
        {
            if (TagOf(child) is { Length: > 0 } choice) // 选择字段：未选（默认项）不落盘
            {
                config[child.Tag?.ToString() ?? ""] = choice;
            }
        }
        foreach (var field in descriptor.Fields.Where(f => f.Required))
        {
            if (!config.ContainsKey(field.Key))
            {
                Feedback(_widgetFeedback, $"✗ {field.DisplayName} 为必填。", error: true);
                return;
            }
        }
        string? colorOverride = null;
        var colorText = _widgetColorBox.Text.Trim();
        if (colorText.Length > 0)
        {
            var normalized = colorText.StartsWith('#') ? colorText : "#" + colorText;
            if (!PaletteResolver.TryParseHex(normalized, out _))
            {
                Feedback(_widgetFeedback, "✗ 颜色覆盖格式无效（#RRGGBB 或 #AARRGGBB）。", error: true);
                return;
            }
            colorOverride = normalized;
        }

        // 组件 Id 种子按类型取自然键：GitHub 用 repo，HTTP/额度用 label，缺省退类型尾段
        var seed = config.GetValueOrDefault("repo") ?? config.GetValueOrDefault("label") ?? descriptor.Type.Split('.')[^1];
        var id = $"{descriptor.Type}:{seed}";
        for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
        {
            id = $"{descriptor.Type}:{seed}-{suffix}";
        }

        var widget = new WidgetConfig
        {
            Id = id,
            Type = descriptor.Type,
            ConnectionId = TagOf(_widgetConnectionBox) ?? "",
            Config = config,
            RefreshTier = TagOf(_widgetTierBox) ?? descriptor.SuggestedTier,
            RefreshIntervalSeconds = TagOf(_widgetIntervalBox) is { } tag && int.TryParse(tag, out var seconds) && seconds > 0
                ? seconds
                : null,
            Pinned = _widgetPinBox.IsOn,
            ColorOverride = colorOverride,
        };
        _runtime.Config.UpsertWidget(widget);
        _runtime.ReloadWidgets([widget.Id]); // 新组件即建即拉（调度器重建+首拉，白板收口）
        RebuildWidgetFields(); // 清空已提交的字段输入（含颜色覆盖）
        RebuildWidgets();
        RefreshWidgetConnectionOptions();
        Feedback(_widgetFeedback, widget.Pinned
            ? $"✓ 组件 {id} 已添加并钉到桌面，正在拉取（几秒内出数）。"
            : $"✓ 组件 {id} 已添加（未钉选，可在上方组件行开启「钉」上板）。", error: false);
        PinsChanged?.Invoke();
    }

    private WidgetTypeDescriptor? SelectedWidgetDescriptor()
        => TagOf(_widgetTypeBox) is { } type
            ? AllWidgetDescriptors().FirstOrDefault(d => d.Type == type)
            : null;

    private void RebuildWidgets()
    {
        if (_widgetList is null)
        {
            return; // 常规/外观/高级页无组件区
        }
        _widgetList.Children.Clear();
        foreach (var widget in _runtime.Config.Widgets.Where(w => _scopeDescriptors is null || _scopeDescriptors.Any(d => d.Type == w.Type)))
        {
            _widgetList.Children.Add(MakeWidgetRow(widget));
        }
        if (!_runtime.Config.Widgets.Any(w => _scopeDescriptors is null || _scopeDescriptors.Any(d => d.Type == w.Type)))
        {
            _widgetList.Children.Add(Hint("尚无组件——用下方向导添加，字段由组件类型决定。"));
        }
    }
    private UIElement MakeWidgetRow(WidgetConfig widget)
    {
        var descriptor = AllWidgetDescriptors().FirstOrDefault(d => d.Type == widget.Type);
        var missingConnection = _runtime.Config.Connections.All(c => c.Id != widget.ConnectionId);
        var label = new TextBlock
        {
            Text = $"{widget.Id} · {widget.RefreshTier}"
                   + (widget.RefreshIntervalSeconds is { } seconds ? $" · {seconds}s" : "")
                   + (widget.Pinned ? " · 已钉" : "")
                   + (widget.ColorOverride is { } color ? $" · 覆盖 {color}" : "")
                   + (missingConnection ? " · 连接缺失" : ""),
            FontSize = DesignTokens.FontSubtitle,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (descriptor is { PinSupported: true })
        {
            var pin = new ToggleSwitch { OnContent = "钉", OffContent = "钉", IsOn = widget.Pinned, Margin = new Thickness(0, 0, 0, 0) };
            // UIA 取证/CI 入口（B-702 两入口一致性取证）：ToggleSwitch 自带 peer，AutomationId 即可达——
            // 与 appearance-color-success 同口径（Control 不需要 Border 那样的 SetName 补 peer）。
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pin, $"pin-toggle-{widget.Id}");
            pin.Toggled += (_, _) =>
            {
                widget.Pinned = pin.IsOn;
                _runtime.Config.UpsertWidget(widget);
                _runtime.ReloadWidgets(widget.Pinned ? [widget.Id] : null); // 开钉=重建注册+立即拉数（用户实测：以前首开不出数，关/开几次才显）
                PinsChanged?.Invoke();
            };
            row.Children.Add(pin);
        }

        var delete = new Button { Content = "删除" };
        // UIA 取证/CI 入口（行 193 订阅重建链）：删除走注销链（Invalidate+Remove+Reload 防幽灵刷新）
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(delete, $"widget-delete-{widget.Id}");
        delete.Click += (_, _) =>
        {
            _runtime.InvalidateWidgets([widget.Id]); // 面板/聚合器丢弃残留状态
            _runtime.Config.RemoveWidget(widget.Id);
            _runtime.ReloadWidgets(); // 调度器同步移除（防幽灵刷新）
            if (_componentEditTarget?.Id == widget.Id)
            {
                _componentEditTarget = null; // 防已删组件经编辑器 upsert 复活
                _componentEditHeader.Text = "组件实例面（点组件行「组件树」载入）";
            }
            RebuildWidgets();
            PinsChanged?.Invoke();
        };
        // 组件树编辑入口（Phase 5 行 4）：载入该组件的声明式树到下方 JSON 编辑器
        var treeButton = new Button { Content = "组件树" };
        treeButton.Click += (_, _) => OpenComponentTreeEditor(widget);
        // 展示样式入口（2026-10-18 外部评审 P0）：定向打开组件中心，自动带入该组件真实状态
        var styleButton = new Button { Content = "展示样式" };
        // UIA 取证/CI 入口（评审场景 AE）：每行样式入口稳定锚（与 widget-delete-{id} 同排布）
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(styleButton, $"style-button-{widget.Id}");
        styleButton.Click += (_, _) => OpenComponentStyleCenter(widget);
        row.Children.Add(label);
        row.Children.Add(styleButton);
        row.Children.Add(treeButton);
        row.Children.Add(delete);
        return row;
    }

    // —— 通知规则与通用小件 ——

    private string DescribeRules()
    {
        var rules = _runtime.Config.App.NotificationRules.Count > 0
            ? _runtime.Config.App.NotificationRules
            : DefaultNotificationRules.All;
        if (rules.Count == 0)
        {
            return "（无规则）";
        }
        return string.Join("\n", rules.Select(rule =>
            $"· {(rule.WidgetType ?? "*")} ≥{rule.SeverityAtLeast}{(rule.SeverityAtMost is { } atMost ? $" ≤{atMost}" : "")}"
            + $"{(rule.Toast ? " · Toast" : "")}{(rule.Sound ? " · 声音" : "")}"
            + (rule.Cooldown is { } cooldown ? $" · 冷却 {cooldown.TotalMinutes:F0}min" : " · 每阈值一次")));
    }
}
