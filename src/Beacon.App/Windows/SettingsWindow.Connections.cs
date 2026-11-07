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
    // —— 连接：列表 + 表单 + 测试（B-801；token 只进 DPAPI） ——

    /// <summary>连接表单。lockType 非空 = Provider 模块页（类型锁定，凭证/端点提示按类型定制）。</summary>
    private UIElement BuildConnectionEditor(string? lockType)
    {
        _connTypeBox = new ComboBox { Header = "类型", Width = 160, IsEnabled = lockType is null };
        IEnumerable<string> types = lockType is { } locked ? [locked] : _runtime.ConnectionProviders.Keys;
        foreach (var type in types)
        {
            _connTypeBox.Items.Add(type);
        }
        if (_connTypeBox.Items.Count > 0)
        {
            _connTypeBox.SelectedIndex = 0;
        }
        _connIdBox = new TextBox { Header = "名称（唯一 Id）", Width = 200, PlaceholderText = ConnectionIdHint(lockType) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connIdBox, "conn-id-box"); // UIA 取证：save-flow 驱动
        _connIdBox.Text = PrefillConnectionId(lockType); // 预填唯一 Id：不改即用，改了以用户为准（不再强制手填）
        _connTypeBox.SelectionChanged += (_, _) => PrefillConnectionIdIfUntouched();
        _connTypeBox.SelectionChanged += (_, _) => SyncUsageBoxVisibility();
        _connEndpointBox = new TextBox { Header = ConnectionEndpointHeader(lockType), Width = 280, PlaceholderText = ConnectionEndpointHint(lockType) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connEndpointBox, "conn-endpoint-box");
        _connUsageBox = new TextBox
        {
            Header = "用量端点（可空 = 显示模型目录，官方额度口未开放）",
            Width = 280,
            PlaceholderText = "https://…/usage（GET + Bearer；响应含 percent/reset 即出额度卡）",
            Visibility = Visibility.Collapsed,
        };
        _connTokenBox = new PasswordBox { Header = ConnectionTokenHeader(lockType), Width = 280 };
        _connConsoleBox = new PasswordBox
        {
            Header = "控制台 Cookie（可选，录了组件才显示套餐用量）",
            Width = 280,
            PlaceholderText = "浏览器 F12 → 应用 → Cookies → api-platform_ph 的值",
            Visibility = Visibility.Collapsed,
        };

        _connSaveButton = new Button { Content = "保存连接" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connSaveButton, "conn-save-button");
        _connSaveButton.Click += async (_, _) => await SaveConnectionAsync();
        var cancel = new Button { Content = "取消编辑" };
        cancel.Click += (_, _) => ResetConnectionEditor();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _connTypeBox, _connIdBox } });
        form.Children.Add(_connEndpointBox);
        form.Children.Add(_connUsageBox);
        form.Children.Add(_connTokenBox);
        form.Children.Add(_connConsoleBox);
        SyncUsageBoxVisibility();
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _connSaveButton, cancel } });
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = DesignTokens.RadiusCard,
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
    }

    private async Task SaveConnectionAsync()
    {
        var id = _connIdBox.Text.Trim();
        if (id.Length == 0)
        {
            Feedback(_connFeedback, "✗ 请填写连接名称。", error: true);
            return;
        }
        if (_editingConnectionId is null && _runtime.Config.Connections.Any(c => c.Id == id))
        {
            Feedback(_connFeedback, $"✗ 连接 {id} 已存在。", error: true);
            return;
        }

        var existing = _editingConnectionId is null ? null : _runtime.Config.Connections.FirstOrDefault(c => c.Id == _editingConnectionId);
        var credentialRef = existing?.CredentialRef;
        var token = _connTokenBox.Password;
        if (token.Length > 0)
        {
            credentialRef = $"conn:{id}";
            try
            {
                await _runtime.Secrets.SetAsync(credentialRef, token);
            }
            catch (Exception exception)
            {
                Feedback(_connFeedback, $"✗ 密钥写入失败：{exception.Message}", error: true);
                return;
            }
        }

        var connection = new ConnectionConfig
        {
            Id = id,
            Type = SelectedString(_connTypeBox) ?? "github",
            Endpoint = string.IsNullOrWhiteSpace(_connEndpointBox.Text) ? null : _connEndpointBox.Text.Trim(),
            CredentialRef = credentialRef,
            Enabled = existing?.Enabled ?? true, // 编辑保留启停状态
            Settings = MergeConnectionSettings(existing, _connUsageBox?.Text.Trim()), // 保留 auth_header 等高级项 + usage_endpoint
        };
        if (_connConsoleBox is { } console && console.Password.Trim().Length > 0)
        {
            // 控制台 Cookie 是登录态凭据：只进 DPAPI（ref 默认 mimo:console，与推理 Key 分开两格）
            try
            {
                await _runtime.Secrets.SetAsync(MiMoUsageProvider.DefaultConsoleCredentialRef, console.Password.Trim());
            }
            catch (Exception exception)
            {
                Feedback(_connFeedback, $"✗ 控制台 Cookie 写入失败：{exception.Message}", error: true);
                return;
            }
        }
        _runtime.Config.UpsertConnection(connection);
        ResetConnectionEditor();
        RebuildConnections();
        RefreshWidgetConnectionOptions(); // 保存连接后必须刷组件向导下拉——否则新建连接不出现在可选列表（用户实测「下拉空白」根因）
        Feedback(_connFeedback, $"✓ 连接 {id} 已保存。", error: false);
        SettingsApplied?.Invoke();
    }

    /// <summary>生成该类型的唯一连接 Id：默认名（ConnectionIdHint）+ 重名 -2/-3 递增；预填输入框，不改即用。</summary>
    private string PrefillConnectionId(string? lockType)
    {
        var seed = ConnectionIdHint(lockType ?? SelectedString(_connTypeBox) ?? "github");
        var id = seed;
        for (var suffix = 2; _runtime.Config.Connections.Any(c => c.Id == id); suffix++)
        {
            id = $"{seed}-{suffix}";
        }
        _lastPrefilledConnectionId = id;
        return id;
    }

    /// <summary>类型切换时仅当 Id 框未被用户改过（空或仍是上次预填值）才重预填，不覆盖用户输入。</summary>
    private void PrefillConnectionIdIfUntouched()
    {
        if (!_connIdBox.IsEnabled)
        {
            return; // 编辑已有连接（Id 锁定）
        }
        var current = _connIdBox.Text.Trim();
        if (current.Length == 0 || current == _lastPrefilledConnectionId)
        {
            _connIdBox.Text = PrefillConnectionId(null);
        }
    }

    private static string ConnectionIdHint(string? lockType) => lockType switch
    {
        "github" => "github-main",
        "jenkins" => "jenkins-main",
        "bigmodel" => "zhipu",
        "ark" => "ark-main",
        "claude" => "claude-local",
        "machine" => "machine-local",
        "herald" => "herald-local",
        "kimi" => "kimi",
        "moonshot" => "moonshot-main",
        "deepseek" => "deepseek",
        "mimo" => "mimo-main",
        "qwen" => "qwen-main",
        "sensenova" => "sensenova-main",
        "codex" => "codex-local",
        "copilot" => "copilot-main",
        "opencode" => "opencode-main",
        "http" => "ci-local",
        _ => "github-main",
    };

    private static string ConnectionEndpointHeader(string? lockType) => lockType switch
    {
        "bigmodel" => "Endpoint（可空 = 官方监控接口；Z.AI 填国际站地址）",
        "ark" => "Endpoint（可空 = 官方网关 open.volcengineapi.com，一般不用改）",
        "jenkins" => "Endpoint（必填 = Jenkins 实例基址，如 https://jenkins.example.com）",
        "claude" => "会话目录（可空 = 默认 ~/.claude/projects）",
        "machine" => "无需 Endpoint（本机数据，留空）",
        "herald" => "Endpoint（必填 = Herald 基址，如 http://127.0.0.1:8080）",
        "kimi" => "Endpoint（可空 = 官方用量接口）",
        "moonshot" => "Endpoint（可空 = 官方余额接口 api.moonshot.cn/v1/users/me/balance）",
        "deepseek" => "Endpoint（可空 = 官方余额接口）",
        "mimo" => "Endpoint（可空 = 推理域 token-plan-cn.xiaomimimo.com/v1）",
        "qwen" => "Endpoint（可空 = 百炼兼容口 token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1）",
        "sensenova" => "Endpoint（可空 = 官方积分池接口）",
        "codex" => "会话目录（可空 = 默认 ~/.codex/sessions）",
        "copilot" => "Endpoint（可空 = 官方配额端点 copilot_internal/user）",
        "opencode" => "Endpoint（可空 = 官方 Console Budgets 接口）",
        "http" => "Endpoint（必填，如局域网打包工具地址）",
        _ => "Endpoint（可空 = 官方 API）",
    };

    private static string ConnectionEndpointHint(string? lockType) => lockType switch
    {
        "bigmodel" => "https://open.bigmodel.cn/api/monitor/usage/quota/limit",
        "ark" => "https://open.volcengineapi.com/",
        "jenkins" => "https://jenkins.example.com",
        "claude" => "C:\\Users\\me\\.claude\\projects",
        "kimi" => "https://api.kimi.com/coding/v1/usages",
        "moonshot" => "https://api.moonshot.cn/v1/users/me/balance",
        "deepseek" => "https://api.deepseek.com/user/balance",
        "mimo" => "https://token-plan-cn.xiaomimimo.com/v1",
        "qwen" => "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1",
        "sensenova" => "https://platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage",
        "codex" => "C:\\Users\\me\\.codex\\sessions",
        "copilot" => "https://api.github.com/copilot_internal/user",
        "opencode" => "https://opencode.ai/console/api/v2/budgets/members",
        "http" => "http://192.168.1.10:8080/api/status",
        _ => "https://api.github.com",
    };

    private static string ConnectionTokenHeader(string? lockType) => lockType switch
    {
        "github" => "PAT（只写 DPAPI，JSON 仅存引用）",
        "jenkins" => "user:apiToken（可空 = 匿名读；Manage Jenkins → Users → API Token 创建，只写 DPAPI）",
        "bigmodel" => "API Key（只写 DPAPI；监控接口裸 Key 直传）",
        "ark" => "AccessKey:SecretKey（冒号分隔一次粘贴，只进 DPAPI；控制台「API 访问密钥」创建）",
        "claude" => "无需凭据（留空）",
        "machine" => "无需凭据（留空）",
        "herald" => "API Key（可空 = 匿名；Herald auth.enabled 开启时必填，只写 DPAPI）",
        "kimi" => "API Key（sk-kimi-*，只写 DPAPI）",
        "moonshot" => "API Key（开放平台 Key，只写 DPAPI；Bearer 直传）",
        "deepseek" => "API Key（只写 DPAPI）",
        "mimo" => "API Key（只写 DPAPI；Bearer 直传）",
        "qwen" => "API Key（sk-sp- 专属 Key，只写 DPAPI；Bearer 直传）",
        "sensenova" => "控制台 JWT（会话令牌，非 API Key；只写 DPAPI；过期卡面提示重取）",
        "codex" => "无需凭据（留空）",
        "copilot" => "PAT（需 copilot scope，只写 DPAPI）",
        "opencode" => "API Key（oc_sk，只写 DPAPI；读 Budgets 需 All 权限）",
        "http" => "API Key（可选，只写 DPAPI；默认 Bearer 头，可在 config.json 改）",
        _ => "Token（只写 DPAPI，JSON 仅存引用）",
    };

    private void ResetConnectionEditor()
    {
        _editingConnectionId = null;
        _connSaveButton.Content = "保存连接";
        _connIdBox.IsEnabled = true;
        _connIdBox.Text = PrefillConnectionId(null);
        _connEndpointBox.Text = "";
        if (_connUsageBox is { } usage)
        {
            usage.Text = "";
        }
        if (_connConsoleBox is { } console)
        {
            console.Password = "";
            console.PlaceholderText = "浏览器 F12 → 应用 → Cookies → api-platform_ph 的值";
        }
        _connTokenBox.Password = "";
        _connTokenBox.PlaceholderText = "";
    }

    /// <summary>连接表单辅助框显隐：用量端点框仅 qwen（官方额度口未开放）；控制台 Cookie 框仅 mimo（plan-manage 套餐用量）。</summary>
    private void SyncUsageBoxVisibility()
    {
        var type = SelectedString(_connTypeBox);
        if (_connUsageBox is { } box)
        {
            box.Visibility = type == "qwen" ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_connConsoleBox is { } console)
        {
            console.Visibility = type == "mimo" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>连接 Settings 落盘：usage_endpoint 随表单值增删，其余高级项（auth_header 等）原样保留。</summary>
    private static Dictionary<string, string> MergeConnectionSettings(ConnectionConfig? existing, string? usageEndpoint)
    {
        var settings = existing?.Settings is { } keep
            ? new Dictionary<string, string>(keep, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(usageEndpoint))
        {
            settings.Remove("usage_endpoint");
        }
        else
        {
            settings["usage_endpoint"] = usageEndpoint!;
        }
        return settings;
    }

    private void RebuildConnections()
    {
        if (_connectionList is null)
        {
            return; // 常规/外观/高级页无连接区
        }
        _connectionList.Children.Clear();
        foreach (var connection in _runtime.Config.Connections.Where(c => _scopeType is null || c.Type == _scopeType))
        {
            _connectionList.Children.Add(MakeConnectionRow(connection));
        }
        if (!_runtime.Config.Connections.Any(c => _scopeType is null || c.Type == _scopeType))
        {
            _connectionList.Children.Add(Hint(_scopeType is { } type ? ProviderEmptyHint(type) : "尚无连接。"));
        }
        SyncModuleToggles();
    }

    /// <summary>左目录的模块开关与连接实际启停对齐（行内开关改动后回写）。</summary>
    private void SyncModuleToggles()
    {
        if (_moduleToggles.Count == 0)
        {
            return;
        }
        _syncingToggles = true;
        try
        {
            foreach (var (type, toggle) in _moduleToggles)
            {
                toggle.IsOn = TypeEnabled(type);
            }
        }
        finally
        {
            _syncingToggles = false;
        }
    }

    private UIElement MakeConnectionRow(ConnectionConfig connection)
    {
        var label = new TextBlock
        {
            Text = $"{connection.Id}{(connection.CredentialRef is null ? " · 无凭据" : " · 凭据已存")}{(connection.Enabled ? "" : " · 已停用")}",
            FontSize = DesignTokens.FontSubtitle,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 单连接启停（Enabled 是 init-only：with 表达式替换后落库）
        var enabledToggle = new ToggleSwitch
        {
            IsOn = connection.Enabled,
            OnContent = "",
            OffContent = "",
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        // UIA 取证/CI 入口（行 193 订阅重建链）：与 pin-toggle 同口径（ToggleSwitch 自带 peer）
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(enabledToggle, $"conn-toggle-{connection.Id}");
        enabledToggle.Toggled += (_, _) =>
        {
            // Enabled init-only（class 非 record）：重建替换落库
            _runtime.Config.UpsertConnection(new ConnectionConfig
            {
                Id = connection.Id,
                Type = connection.Type,
                Endpoint = connection.Endpoint,
                CredentialRef = connection.CredentialRef,
                Enabled = enabledToggle.IsOn,
                Settings = connection.Settings,
            });
            if (!enabledToggle.IsOn)
            {
                // 停用即失活关联组件（不留旧灯），再重建列表
                _runtime.InvalidateWidgets(_runtime.Config.Widgets.Where(w => w.ConnectionId == connection.Id).Select(w => w.Id));
            }
            RebuildConnections();
            RebuildWidgets();
            RefreshWidgetConnectionOptions(); // 停用连接后下拉同步移除，启用后恢复
            PinsChanged?.Invoke(); // 2026-10-09 修:连接停用/启用同样要重建 L0(悬浮窗/宿主)——与模块开关同口径
        };

        var test = new Button { Content = "测试" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(test, $"test-{connection.Id}"); // UIA 取证：点测试截反馈
        test.Click += async (_, _) => await TestConnectionAsync(connection);
        var edit = new Button { Content = "编辑" };
        edit.Click += (_, _) =>
        {
            _editingConnectionId = connection.Id;
            _connSaveButton.Content = "更新连接";
            _connIdBox.Text = connection.Id;
            _connIdBox.IsEnabled = false;
            _connEndpointBox.Text = connection.Endpoint ?? "";
            if (_connTypeBox.SelectedItem as string != connection.Type)
            {
                var typeIndex = -1;
                for (var i = 0; i < _connTypeBox.Items.Count; i++)
                {
                    if (_connTypeBox.Items[i] is string typeName && typeName == connection.Type)
                    {
                        typeIndex = i;
                        break;
                    }
                }
                if (typeIndex >= 0)
                {
                    _connTypeBox.SelectedIndex = typeIndex; // 表单框头/用量框显隐随类型切换（编辑连接带出真实类型）
                }
            }
            if (_connUsageBox is { } usage)
            {
                usage.Text = connection.Settings.GetValueOrDefault("usage_endpoint") ?? "";
            }
            if (_connConsoleBox is { } console && connection.Type == "mimo")
            {
                console.Password = "";
                console.PlaceholderText = "已保存（留空保持不变）"; // cookie 在 DPAPI，读不回明文——只提示不变
            }
            _connTokenBox.Password = "";
            _connTokenBox.PlaceholderText = "已保存（留空保持不变）";
        };
        var delete = new Button { Content = "删除" };
        delete.Click += (_, _) =>
        {
            _runtime.InvalidateWidgets(_runtime.Config.Widgets.Where(w => w.ConnectionId == connection.Id).Select(w => w.Id));
            _runtime.Config.RemoveConnection(connection.Id);
            RebuildConnections();
            RebuildWidgets(); // ConnectionId 失联的组件如实展示
            RefreshWidgetConnectionOptions(); // 删除连接后下拉同步移除
            PinsChanged?.Invoke();
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { label, enabledToggle, test, edit, delete },
        };
    }

    private async Task TestConnectionAsync(ConnectionConfig connection)
    {
        if (!_runtime.ConnectionProviders.TryGetValue(connection.Type, out var provider))
        {
            Feedback(_connFeedback, $"✗ 连接类型 {connection.Type} 无 Provider。", error: true);
            return;
        }
        try
        {
            var result = await provider.TestAsync(connection, new ConnectionContext { Secrets = _runtime.Secrets }, CancellationToken.None);
            var health = result.Health;
            // Detail 原样透出：通用文案（「降级（限流等）」）吞掉真实原因（凭据格式/4xx 响应体/响应结构），
            // 用户无法判断「明明连上了却报降级」是哪一环——真实原因必须可见（2026-10-09 用户实测反馈）
            var detail = string.IsNullOrEmpty(result.Detail) ? "" : $"：{result.Detail}";
            Feedback(_connFeedback, health switch
            {
                ConnectionHealthState.Healthy => $"✓ {connection.Id} 连接正常——在下方组件向导选该连接，或用「一键添加」即可上板。",
                ConnectionHealthState.Degraded => $"△ {connection.Id} 降级{detail}——仍可添加组件上板。",
                ConnectionHealthState.Offline => $"✗ {connection.Id} 不可达{detail}。",
                ConnectionHealthState.Unauthorized => $"✗ {connection.Id} 认证失败{detail}，检查 token。",
                _ => $"✗ {connection.Id} 状态未知{detail}。",
            }, error: health is not ConnectionHealthState.Healthy and not ConnectionHealthState.Degraded);
        }
        catch (Exception exception)
        {
            Feedback(_connFeedback, $"✗ 测试失败：{exception.Message}", error: true);
        }
    }
}
