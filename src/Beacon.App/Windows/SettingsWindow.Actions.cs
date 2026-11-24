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
    // —— 页面：自定义动作（独占一页，UI/UX 审计：编辑器 8+ 字段与诊断/导入导出/通知规则混排一页时，
    // 动作表单占满首屏，把后半分组压出视口且无提示——一屏一模块） ——

    private UIElement BuildActionsPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("自定义动作"));
        page.Children.Add(BuildActionEditorSection());
        return page;
    }

    // —— 页面：高级（诊断 + 导入导出 + 通知规则） ——

    private UIElement BuildAdvancedPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("诊断"));
        page.Children.Add(Hint($"逐跳诊断日志：%APPDATA%\\Beacon\\logs\\beacon-*.log（每跳一行：一键添加→拉取→字段映射→渲染；组件不出数先看这里最新一份）。"));
        page.Children.Add(SectionTitle("导入导出"));
        page.Children.Add(BuildImportExportSection());
        page.Children.Add(SectionTitle("通知规则"));
        page.Children.Add(new TextBlock
        {
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            Text = DescribeRules(),
            TextWrapping = TextWrapping.Wrap,
        });
        page.Children.Add(new TextBlock
        {
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "规则经 config.json 的 notificationRules 自定义；留空使用内置默认。",
            TextWrapping = TextWrapping.Wrap,
        });
        return page;
    }

    // —— 高级页：自定义动作库（actions.json；详情窗 ACTIONS 区按 widgetType 过滤出现） ——

    private UIElement BuildActionEditorSection()
    {
        _actionTypeBox = new ComboBox { Header = "类型", Width = 220 };
        foreach (var (type, label) in new[]
        {
            ("http", "HTTP 请求（触发打包 / 部署）"),
            ("webhook", "Webhook（带签名 / 凭据）"),
            ("local.command", "本机命令"),
            ("open.url", "打开链接"),
        })
        {
            _actionTypeBox.Items.Add(new ComboBoxItem { Content = label, Tag = type });
        }
        _actionTypeBox.SelectedIndex = 0;
        _actionTypeBox.SelectionChanged += (_, _) => SyncActionFields();

        _actionNameBox = new TextBox { Header = "按钮名", Width = 160, PlaceholderText = "触发打包" };
        _actionConfirmBox = new ToggleSwitch { Header = "执行前确认", IsOn = true, OnContent = "", OffContent = "", Margin = new Thickness(0, 0, 8, 0) };
        _actionScopeBox = new TextBox
        {
            Header = "限定组件类型（可空 = 全部组件可见）",
            Width = 240,
            PlaceholderText = "http.status / github.actions.runs …",
        };

        _actionUrlBox = new TextBox { Header = "URL", Width = 300, PlaceholderText = "http://192.168.5.9:8080/build" };
        _actionMethodBox = new TextBox { Header = "Method（默认 GET）", Width = 160, PlaceholderText = "POST" };
        _actionBodyBox = new TextBox { Header = "Body（可空）", Width = 300, PlaceholderText = "{\"job\":\"release\"}" };
        _actionCommandBox = new TextBox { Header = "命令", Width = 300, PlaceholderText = "C:\\build\\trigger.cmd" };
        _actionArgsBox = new TextBox { Header = "参数（可空）", Width = 300, PlaceholderText = "--job release" };
        _actionWorkDirBox = new TextBox { Header = "工作目录（可空）", Width = 300 };

        var save = new Button { Content = "保存动作" };
        save.Click += (_, _) => SaveAction();

        _actionFeedback = new TextBlock { FontSize = DesignTokens.FontBody, TextWrapping = TextWrapping.Wrap };
        _actionList = new StackPanel { Spacing = 4 };

        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionTypeBox, _actionNameBox } });
        // UI/UX 审计：URL+Method、命令+参数各并一行——原 9 行纵向表单按类型显隐后可视 5 行，
        // 压缩到 6 行容器（可视 4 行），动作列表不再被推出首屏
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionUrlBox, _actionMethodBox } });
        form.Children.Add(_actionBodyBox);
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionCommandBox, _actionArgsBox } });
        form.Children.Add(_actionWorkDirBox);
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionScopeBox, _actionConfirmBox, save } });
        form.Children.Add(_actionFeedback);
        form.Children.Add(_actionList);
        SyncActionFields();
        RebuildActionList();
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = DesignTokens.RadiusCard,
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
    }

    /// <summary>参数字段按类型显隐：http=URL/Method/Body；webhook=URL/Body；local.command=命令/参数/目录；open.url=URL。</summary>
    private void SyncActionFields()
    {
        if (_actionTypeBox is null || _actionUrlBox is null)
        {
            return;
        }
        var type = TagOf(_actionTypeBox) ?? "http";
        SetVisible(_actionUrlBox, type is "http" or "webhook" or "open.url");
        SetVisible(_actionMethodBox, type == "http");
        SetVisible(_actionBodyBox, type is "http" or "webhook");
        SetVisible(_actionCommandBox, type == "local.command");
        SetVisible(_actionArgsBox, type == "local.command");
        SetVisible(_actionWorkDirBox, type == "local.command");
    }
    private void SaveAction()
    {
        var type = TagOf(_actionTypeBox) ?? "http";
        var name = _actionNameBox?.Text.Trim() ?? "";
        if (name.Length == 0)
        {
            Feedback(_actionFeedback, "✗ 请填写按钮名。", error: true);
            return;
        }
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var url = _actionUrlBox?.Text.Trim() ?? "";
        switch (type)
        {
            case "http" or "webhook" or "open.url" when url.Length == 0:
                Feedback(_actionFeedback, "✗ 请填写 URL。", error: true);
                return;
            case "http":
                parameters["url"] = url;
                parameters["method"] = _actionMethodBox?.Text.Trim() is { Length: > 0 } method ? method.ToUpperInvariant() : "GET";
                if (_actionBodyBox?.Text.Trim() is { Length: > 0 } body)
                {
                    parameters["body"] = body;
                }
                break;
            case "webhook":
                parameters["url"] = url;
                if (_actionBodyBox?.Text.Trim() is { Length: > 0 } hookBody)
                {
                    parameters["body"] = hookBody;
                }
                break;
            case "open.url":
                parameters["url"] = url;
                break;
            case "local.command":
                var command = _actionCommandBox?.Text.Trim() ?? "";
                if (command.Length == 0)
                {
                    Feedback(_actionFeedback, "✗ 请填写命令。", error: true);
                    return;
                }
                parameters["command"] = command;
                if (_actionArgsBox?.Text.Trim() is { Length: > 0 } args)
                {
                    parameters["args"] = args;
                }
                if (_actionWorkDirBox?.Text.Trim() is { Length: > 0 } workDir)
                {
                    parameters["workingDirectory"] = workDir;
                }
                break;
        }
        if (_actionScopeBox?.Text.Trim() is { Length: > 0 } scope)
        {
            parameters["widgetType"] = scope; // 详情窗按组件类型过滤（空=全部组件可见）
        }

        // 同名覆盖（再保存一次 = 编辑），Id 稳定可复现
        var id = $"action:{name.ToLowerInvariant().Replace(' ', '-')}";
        _runtime.Config.UpsertAction(new ActionConfig
        {
            Id = id,
            Type = type,
            Name = name,
            RequireConfirmation = _actionConfirmBox?.IsOn ?? true,
            Parameters = parameters,
        });
        _runtime.Config.SaveActions();
        SettingsApplied?.Invoke(); // 动作增删即时生效：悬浮 tile 下次打开详情窗即见按钮，不必等重启
        Feedback(_actionFeedback, $"✓ 动作「{name}」已保存——点悬浮 tile 打开详情窗即可看到按钮。", error: false);
        RebuildActionList();
    }

    /// <summary>动作库列表（高级页）：名称 · 类型 · 确认标记 · 作用域，行内删除即落盘。</summary>
    private void RebuildActionList()
    {
        if (_actionList is null)
        {
            return; // 页面未构建（其他页切走）
        }
        _actionList.Children.Clear();
        foreach (var action in _runtime.Config.Actions)
        {
            var scope = action.Parameters.TryGetValue("widgetType", out var widgetType) && widgetType.Length > 0
                ? $" · 仅 {widgetType}"
                : "";
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = $"{action.Name ?? action.Id} · {action.Type}{(action.RequireConfirmation ? " · 需确认" : "")}{scope}",
                FontSize = DesignTokens.FontBody,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var delete = new Button { Content = "删除", Padding = new Thickness(8, 2, 8, 2) };
            var captured = action;
            delete.Click += (_, _) =>
            {
                _runtime.Config.RemoveAction(captured.Id);
                _runtime.Config.SaveActions();
                SettingsApplied?.Invoke(); // 删除同保存口径：详情窗按钮即时消失
                RebuildActionList();
            };
            row.Children.Add(delete);
            _actionList.Children.Add(row);
        }
        if (_runtime.Config.Actions.Count == 0)
        {
            _actionList.Children.Add(Hint("尚无自定义动作。保存后出现在组件详情窗（点悬浮 tile）的 ACTIONS 区。"));
        }
    }

    // —— 页面：Provider 模块（GitHub / 智谱 GLM / 自定义 HTTP） ——

    private StackPanel? _sourceRecommend; // SET-UX-09：下一步推荐区（测试状态驱动重建）

    private UIElement BuildProviderPage(ModuleDef module)
    {
        var type = module.ConnectionType!;
        var connections = _runtime.Config.Connections.Where(c => c.Type == type).ToList();
        var widgets = _runtime.Config.Widgets
            .Where(w => _scopeDescriptors!.Any(d => d.Type == w.Type))
            .ToList();

        var page = PageShell();
        page.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                MakeModuleIcon(module),
                new StackPanel
                {
                    Spacing = 2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = module.DisplayName, FontSize = DesignTokens.FontDisplay, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock
                        {
                            Text = ProviderStatus(module, connections, widgets.Count),
                            FontSize = DesignTokens.FontCaptionEmphasis,
                            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            },
        });
        // SET-UX-09：页头直给能力一句话——用户不用先建连接才知道这页能读到什么
        page.Children.Add(Hint(ProviderReads(type)));

        page.Children.Add(SectionTitle("连接"));
        _connectionList = new StackPanel { Spacing = 6 };
        _connectionStateLabels.Clear(); // SET-UX-09：页面重建后旧 chip 引用作废
        foreach (var connection in connections)
        {
            _connectionList.Children.Add(MakeConnectionRow(connection));
        }
        if (connections.Count == 0)
        {
            _connectionList.Children.Add(Hint(ProviderEmptyHint(type)));
        }
        page.Children.Add(_connectionList);
        // SET-UX-09：下一步推荐区（行内测试点完就地重组；验证通过即出快速添加）
        _sourceRecommend = new StackPanel { Spacing = 8 };
        page.Children.Add(_sourceRecommend);
        RefreshSourceRecommend();
        page.Children.Add(BuildConnectionEditor(type));
        page.Children.Add(_connFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connFeedback, "conn-feedback"); // UIA 取证：测试反馈文本可硬断言

        page.Children.Add(SectionTitle("组件"));
        _widgetList = new StackPanel { Spacing = 6 };
        foreach (var widget in widgets)
        {
            _widgetList.Children.Add(MakeWidgetRow(widget));
        }
        if (widgets.Count == 0)
        {
            _widgetList.Children.Add(Hint("尚无组件——用下方向导添加，字段由组件类型决定。"));
        }
        page.Children.Add(_widgetList);
        page.Children.Add(BuildWidgetEditor());
        if (type == "bigmodel")
        {
            // 绑定流捷径（验收反馈：连上 GLM 却无组件可展示）——一条按钮从「连接就绪」直达「上板」
            var quick = new Button { Content = "一键添加 GLM 额度组件（示例）", HorizontalAlignment = HorizontalAlignment.Left };
            quick.Click += (_, _) => AddGlmQuotaExample();
            page.Children.Add(quick);
        }
        page.Children.Add(_widgetFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        // SET-UX-09：组件树编辑器收进折叠区（高级语义；编辑器无既有 UIA 锚，折叠不影响取证）
        page.Children.Add(new Expander
        {
            Header = "组件树（高级 · 声明式编辑）",
            Content = BuildComponentTreeEditor(),
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        return page;
    }

    /// <summary>SET-UX-09：类型能力一句话（这页能读到什么数据），页头下方直给。</summary>
    private static string ProviderReads(string type) => type switch
    {
        "github" => "能读到：Pull Requests 列表与检查状态、Actions 运行记录。",
        "jenkins" => "能读到：Jenkins 任务最近构建状态与进度。",
        "machine" => "能读到：本机 CPU/内存/网络、Git 仓库状态（零凭据零网络）。",
        "herald" => "能读到：Herald 事件台账、订阅源健康与投递失败。",
        "bigmodel" => "能读到：GLM Coding Plan 额度余量与 5h/周/月重置窗口。",
        "ark" => "能读到：火山方舟 Coding Plan Pro 额度余量。",
        "claude" => "能读到：本机 Claude Code 会话统计（零凭据）。",
        "kimi" => "能读到：Kimi For Coding 套餐余量（5h/周窗口）。",
        "moonshot" => "能读到：Moonshot 开放平台余额。",
        "deepseek" => "能读到：DeepSeek 开放平台余额。",
        "mimo" => "能读到：小米 MiMo 模型目录（官方用量口未开放）。",
        "codex" => "能读到：本机 OpenAI Codex 会话统计（零凭据）。",
        "copilot" => "能读到：Copilot 套餐配额与高级请求用量。",
        "opencode" => "能读到：OpenCode Console Budgets 月度用量。",
        "qwen" => "能读到：百炼 Token Plan 额度（需网关用量口）或模型目录。",
        "sensenova" => "能读到：商汤 Token Plan 积分池余量（5h/7d）。",
        "http" => "能读到：任意状态接口（状态词表/字段映射在组件向导里配）。",
        _ => "能读到：连接对应的数据。",
    };

    /// <summary>SET-UX-09：数据来源「下一步」推荐区，按会话测试状态重组。未验证→引导点测试；
    /// 失败→引导重试；通过/降级→快速添加（该类型首个免必填字段的组件 + 位置单选 + 上板）。
    /// 全部控件带 UIA 锚（source-*），场景 SH 断言链：chip 待验证 → 测试 → chip 通过 → 添加 → 去向。</summary>
    private void RefreshSourceRecommend()
    {
        if (_sourceRecommend is null)
        {
            return; // 非 Provider 页无推荐区
        }
        _sourceRecommend.Children.Clear();
        var type = _scopeType;
        if (type is null)
        {
            return;
        }
        var connections = _runtime.Config.Connections.Where(c => c.Type == type).ToList();
        if (connections.Count == 0)
        {
            _sourceRecommend.Children.Add(Hint("下一步：在下方表单添加连接，保存后点行内「测试」验证。"));
            return;
        }
        var untested = connections.Where(c => _connectionTestStates.GetValueOrDefault(c.Id).State is not "ok" and not "degraded" and not "failed")
            .Select(c => c.Id).ToList(); // 补集口径：显式 saved（保存落账）也算待验证，清单不空括号
        var failed = connections.Where(c => _connectionTestStates.GetValueOrDefault(c.Id).State == "failed").Select(c => c.Id).ToList();
        var usable = connections.Where(c => _connectionTestStates.GetValueOrDefault(c.Id).State is "ok" or "degraded").ToList();
        if (usable.Count == 0)
        {
            _sourceRecommend.Children.Add(Hint(failed.Count > 0
                ? $"下一步：连接 {string.Join("、", failed)} 测试未通过——检查凭据/端点后点行内「测试」重试。"
                : $"下一步：点连接行「测试」验证（{string.Join("、", untested)}），通过后可直接快速添加组件。"));
            return;
        }
        var descriptor = _scopeDescriptors?.FirstOrDefault(d => d.Fields.All(f => !f.Required));
        var app = _runtime.Config.App;
        var islandOn = StatusBarMode.Read(app) == StatusBarMode.Island;
        if (descriptor is null || (!descriptor.PinSupported && !islandOn))
        {
            _sourceRecommend.Children.Add(Hint("✓ 连接已验证——该来源的组件用下方组件向导添加（需必填字段或当前无可放位置）。"));
            return;
        }
        var connection = usable[0];
        _sourceRecommend.Children.Add(new TextBlock
        {
            Text = $"快速添加「{descriptor.DisplayName}」（连接 {connection.Id} 已验证）：",
            FontSize = DesignTokens.FontBody,
            TextWrapping = TextWrapping.Wrap,
        });
        var desktopRadio = new Microsoft.UI.Xaml.Controls.RadioButton
        {
            Content = descriptor.PinSupported ? "钉到桌面" : "钉到桌面（该组件不支持钉选）",
            IsEnabled = descriptor.PinSupported,
            IsChecked = descriptor.PinSupported,
            GroupName = "source-position",
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(desktopRadio, "source-pos-desktop");
        var islandRadio = new Microsoft.UI.Xaml.Controls.RadioButton
        {
            Content = islandOn ? "加入顶部悬浮岛" : "加入顶部悬浮岛（状态条当前未选悬浮岛）",
            IsEnabled = islandOn,
            GroupName = "source-position",
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(islandRadio, "source-pos-island");
        if (!descriptor.PinSupported)
        {
            islandRadio.IsChecked = islandOn;
        }
        _sourceRecommend.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { desktopRadio, islandRadio },
        });
        var addButton = new Button { Content = $"添加 {descriptor.DisplayName}", HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(addButton, "source-add-button");
        var recommendFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap };
        addButton.Click += (_, _) =>
        {
            addButton.IsEnabled = false; // SET-UX-09：重复提交不重复创建（继续添加走向导/组件中心）
            var toIsland = islandRadio.IsChecked == true;
            var id = $"{descriptor.Type}:{connection.Id}";
            for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
            {
                id = $"{descriptor.Type}:{connection.Id}-{suffix}";
            }
            var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (descriptor.Fields.Any(f => f.Key == "label"))
            {
                config["label"] = connection.Id; // 额度卡显示名用连接 Id（一键语义：改名走向导）
            }
            _runtime.Config.UpsertWidget(new WidgetConfig
            {
                Id = id,
                Type = descriptor.Type,
                ConnectionId = connection.Id,
                RefreshTier = descriptor.SuggestedTier,
                Pinned = !toIsland, // 桌面位=钉选上板；岛位走 IslandComponents 白名单（不占桌面）
                Config = config,
            });
            if (toIsland)
            {
                ComponentCenterCatalog.SetIslandOnboarded(_runtime.Config.App, id, onboarded: true);
                _runtime.Config.SaveApp();
            }
            _runtime.ReloadWidgets([id]); // 挂载即拉数（与一键添加同口径：一次开即到位）
            _runtime.Logger?.LogInformation("快速添加：组件 {Id}（连接 {ConnectionId}，{Placement}）。", id, connection.Id, toIsland ? "悬浮岛" : "桌面钉选");
            RebuildWidgets();
            Feedback(recommendFeedback, toIsland
                ? $"✓ 组件 {id} 已加入悬浮岛，正在拉取（几秒内出数）。"
                : $"✓ 组件 {id} 已钉到桌面，正在拉取（几秒内出数）。", error: false);
            PinsChanged?.Invoke();
            var gotoPlacement = new Button { Content = "查看去向（组件中心）", HorizontalAlignment = HorizontalAlignment.Left };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(gotoPlacement, "source-goto-placement");
            gotoPlacement.Click += (_, _) =>
            {
                OpenModule("components");
                FocusAnchor($"component-desktop-state-{id}"); // 直达该组件的桌面去向行（SET-UX-09 查看去向）
            };
            _sourceRecommend!.Children.Add(gotoPlacement);
        };
        _sourceRecommend.Children.Add(addButton);
        _sourceRecommend.Children.Add(recommendFeedback);
    }

    /// <summary>绑定流捷径：GLM 连接就绪后一键建 bigmodel.usage 组件——直接钉选上板（白板三连根因之一：
    /// 以前 Pinned=false 只建不钉，用户看到的是空板），并经 ReloadWidgets 立即拉取（一次开即到位）。</summary>
    private void AddGlmQuotaExample()
    {
        var connection = _runtime.Config.Connections.FirstOrDefault(c => c.Type == "bigmodel");
        if (connection is null)
        {
            Feedback(_widgetFeedback, "✗ 先在上方添加并测试一个智谱 GLM 连接（bigmodel 类型）。", error: true);
            return;
        }
        var id = "bigmodel.usage:GLM";
        for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
        {
            id = $"bigmodel.usage:GLM-{suffix}";
        }
        _runtime.Config.UpsertWidget(new WidgetConfig
        {
            Id = id,
            Type = BigModelWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            RefreshTier = RefreshTiers.Ci,
            Pinned = true, // 一键即上板——「添加了组件板面却空白」比「多一步钉选」更伤
            Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "GLM" },
        });
        _runtime.Logger?.LogInformation("一键示例：组件 {Id} 已创建并钉选（连接 {ConnectionId}，端点 {Endpoint}）。", id, connection.Id, connection.Endpoint);
        _runtime.ReloadWidgets([id]); // 挂载即拉数：不等组循环，事件到达自动重渲
        RebuildWidgets();
        Feedback(_widgetFeedback, $"✓ GLM 额度组件 {id} 已上板（连接 {connection.Id}）——正在拉取，几秒内出数；若持续空白看日志 %APPDATA%\\Beacon\\logs。", error: false);
        PinsChanged?.Invoke();
    }

    private static string ProviderStatus(ModuleDef module, List<ConnectionConfig> connections, int widgetCount)
    {
        if (connections.Count == 0)
        {
            return "未配置——填下方连接后即启用。";
        }
        var enabled = connections.Count(c => c.Enabled);
        var state = enabled == 0 ? "已停用" : enabled == connections.Count ? "运行中" : $"部分启用（{enabled}/{connections.Count}）";
        return $"{state} · {connections.Count} 连接 · {widgetCount} 组件";
    }

    private static string ProviderEmptyHint(string type) => type switch
    {
        "github" => "尚无连接——填一个 GitHub PAT（需要 repo / workflow 读权限）。",
        "jenkins" => "尚无连接——填 Jenkins 实例基址（如 https://jenkins.example.com）。匿名可读实例免凭据；受限实例填 user:apiToken（Manage Jenkins → Users → API Token）。",
        "bigmodel" => "尚无连接——填智谱 API Key（open.bigmodel.cn 控制台获取，监控接口自动带默认端点；Z.AI 填国际站 Endpoint）。",
        "ark" => "尚无连接——火山控制台「API 访问密钥」创建 AK/SK（ArkReadOnlyAccess 权限即可），Token 框一次粘贴 AccessKey:SecretKey；推理用 ARK_API_KEY 调不了额度口（管控面实测 400 拒绝）。",
        "claude" => "无需连接配置——直接读本机 ~/.claude/projects 会话记录；名称随意填（如 claude-local），Endpoint 可空。",
        "machine" => "无需连接配置——本机数据（资源/网络/Git 仓库），零凭据零网络；名称随意填（如 machine-local），Endpoint 留空。",
        "herald" => "尚无连接——填 Herald 基址（如 http://127.0.0.1:8080）；Herald 开启 auth.enabled 时录 API Key（config.yaml auth.api_keys），未开 auth 匿名可用。",
        "kimi" => "尚无连接——填 Kimi Code 控制台 Key（sk-kimi-*，与 Moonshot 开放平台不通用；用量接口自动带默认端点）。",
        "moonshot" => "尚无连接——填 Moonshot 开放平台 Key（platform.moonshot.cn → API Key 管理）。与 Kimi For Coding 套餐是两套账号体系（Key 不通用）；余额接口自动带默认端点。",
        "deepseek" => "尚无连接——填 DeepSeek 开放平台 API Key（platform.deepseek.com；余额接口自动带默认端点）。",
        "mimo" => "尚无连接——填小米开放平台 API Key（platform.xiaomimimo.com，实测推理域可用）。官方未开放用量接口（推理域 /usage 实测 404），卡片显示模型目录真数据；日后开放可在连接 Settings 填 usage_endpoint。",
        "codex" => "无需连接配置——直接读本机 ~/.codex/sessions 会话记录；名称随意填（如 codex-local），Endpoint 可空。官方用量口需 ChatGPT OAuth（不同凭据体系），卡片为本地统计口径。",
        "copilot" => "尚无连接——填 GitHub PAT（github.com/settings/tokens 创建并勾选 copilot scope，官方扩展同款配额端点自动带默认地址）。卡片直读套餐配额（Pro 1500 高级请求/月、聊天/补全无限）与额度重置日；续费信息在 github.com/settings/copilot 查看。",
        "opencode" => "尚无连接——填 OpenCode Key（oc_sk，opencode.ai Console Keys 创建；读 Budgets 需 All 权限，inference-only Key 读不了）。卡片直读官方 Console Budgets 用量（本月消费/月度上限/重置日）。",
        "qwen" => "尚无连接——填百炼 Token Plan 专属 Key（bailian.console.aliyun.com 我的订阅页创建，sk-sp- 前缀）。官方额度查询 REST 口未开放（专属 Key 实测只授权模型调用），卡片默认显示模型目录真数据；有网关/代理用量口可在下方「用量端点」填入（GET + Bearer，响应含 percent/reset 即出额度+重置日）。",
        "sensenova" => "尚无连接——填商汤控制台会话令牌（platform.sensenova.cn 登录后 F12 → 网络任一请求的 Authorization 头 Bearer 后段，是会话 JWT 不是 API Key；有有效期，过期卡面会提示重取）。积分池额度接口自动带默认端点。多账号 = 建多条连接各填一个令牌。",
        _ => "尚无连接——填局域网/内部接口地址（可空凭据）；状态词表/额度字段映射在组件向导里配。",
    };
}
