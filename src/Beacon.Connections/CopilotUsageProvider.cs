using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>copilot（GitHub Copilot）Widget 类型元数据（AI Usage 第八家）。</summary>
public static class CopilotWidgetDescriptors
{
    public const string UsageType = "copilot.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "GitHub Copilot 用量",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Default, // 月度配额窗口（quota_reset_date），默认档足够
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Copilot"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（已用 %）", Placeholder: "默认 80"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（已用 %）", Placeholder: "默认 95"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// copilot.usage（AI Usage 第八家）：GitHub Copilot 套餐用量——**档①官方口直连**（2026-10-09 实测 200）：
/// 官方编辑器扩展（VS Code Copilot）同款配额端点 `api.github.com/copilot_internal/user`——不在公开 REST
/// 文档面，但为 GitHub 官方客户端 API；PAT（需 copilot scope）+ Editor-Version / Copilot-Integration-Id
/// 两头（实测必带，api.githubcopilot.com 域同样两头才通）。返回 quota_snapshots 三槽：
/// premium_interactions（套餐含额，Pro 实测 1500/月）、chat / completions（unlimited），
/// 外加 copilot_plan 与 quota_reset_date（**额度重置日**，非订阅续费日——续费日本接口不返回，不编造）。
/// 级别取非无限槽位已用百分比过阈值（默认 80/95 可配）。
/// </summary>
public sealed class CopilotUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://api.github.com/copilot_internal/user";

    /// <summary>官方扩展同款请求头（实测缺失时端点不认）。</summary>
    internal const string EditorVersionHeader = "vscode/1.99.2";
    internal const string IntegrationIdHeader = "vscode-chat";

    private readonly HttpClient? _client;

    public CopilotUsageProvider() { }

    public CopilotUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => CopilotWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var usage = await FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToState(widget, connection, usage);
    }

    /// <summary>套餐用量：GET copilot_internal/user（Bearer PAT + 官方扩展两头）。</summary>
    public async Task<CopilotUsage> FetchUsageAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var token = await RequireTokenAsync(connection, context).ConfigureAwait(false);
        var endpoint = connection.Endpoint?.Trim().Length > 0 ? connection.Endpoint!.Trim() : DefaultEndpoint;
        var body = await SendAsync(endpoint, token, cancellationToken).ConfigureAwait(false);
        return ParseUsage(body);
    }

    private async Task<string> SendAsync(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("Editor-Version", EditorVersionHeader);
        request.Headers.Add("Copilot-Integration-Id", IntegrationIdHeader);
        request.Headers.UserAgent.ParseAdd("Beacon");
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 401/403=PAT 无效或缺 copilot scope（Degraded 配置可修）；404=端点变动/无 Copilot 权益（Degraded）；
            // 5xx/其余=服务侧（Offline）
            throw new ConnectionException($"Copilot API {(int)response.StatusCode}：{Truncate(body)}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
    }

    /// <summary>
    /// 解析：copilot_plan / access_type_sku / quota_reset_date / quota_snapshots{chat,completions,premium_interactions}。
    /// quota_snapshots 缺失 → 结构异常 Degraded（接口变了要人查，不静默给 0）。
    /// </summary>
    public static CopilotUsage ParseUsage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("quota_snapshots", out var snapshots) || snapshots.ValueKind != JsonValueKind.Object)
        {
            throw new ConnectionException("Copilot 响应不含 quota_snapshots（接口结构可能变化）", ConnectionHealthState.Degraded);
        }

        var planRaw = root.TryGetProperty("copilot_plan", out var planElement) && planElement.ValueKind == JsonValueKind.String
            ? planElement.GetString() ?? ""
            : "";
        var sku = root.TryGetProperty("access_type_sku", out var skuElement) && skuElement.ValueKind == JsonValueKind.String
            ? skuElement.GetString() ?? ""
            : "";
        var resetDate = root.TryGetProperty("quota_reset_date", out var resetElement) && resetElement.ValueKind == JsonValueKind.String
            ? resetElement.GetString()
            : null;

        var slots = new List<CopilotQuotaSlot>();
        foreach (var property in snapshots.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            slots.Add(ParseSlot(property.Name, property.Value));
        }
        return new CopilotUsage(planRaw, sku, resetDate, slots);
    }

    private static CopilotQuotaSlot ParseSlot(string id, JsonElement element)
    {
        var unlimited = element.TryGetProperty("unlimited", out var unlimitedElement) && unlimitedElement.ValueKind == JsonValueKind.True;
        double percentRemaining = 100;
        if (!unlimited && element.TryGetProperty("percent_remaining", out var percentElement)
            && percentElement.ValueKind == JsonValueKind.Number)
        {
            percentRemaining = percentElement.GetDouble();
        }
        long? entitlement = element.TryGetProperty("entitlement", out var entitlementElement)
            && entitlementElement.ValueKind == JsonValueKind.Number
            ? (long)Math.Round(entitlementElement.GetDouble())
            : null;
        long? remaining = element.TryGetProperty("remaining", out var remainingElement)
            && remainingElement.ValueKind == JsonValueKind.Number
            ? (long)Math.Round(remainingElement.GetDouble())
            : null;
        return new CopilotQuotaSlot(id, unlimited, percentRemaining, entitlement, remaining);
    }

    internal static WidgetState ToState(WidgetConfig widget, ConnectionConfig connection, CopilotUsage usage)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "Copilot";
        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 80;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 95;

        var metered = usage.Slots.Where(slot => !slot.Unlimited).ToList();
        var worst = metered.Count > 0 ? metered.Max(slot => 100 - slot.PercentRemaining) : 0;
        var severity = worst >= errorPercent ? Severity.Error
            : worst >= warnPercent ? Severity.Warning
            : Severity.Success;

        var parts = new List<string> { PlanDisplay(usage.PlanRaw, usage.AccessTypeSku) };
        foreach (var slot in usage.Slots)
        {
            var name = SlotName(slot.Id);
            if (slot.Unlimited)
            {
                parts.Add($"{name} ∞");
            }
            else if (slot.Entitlement is long entitlement)
            {
                var used = Math.Max(0, entitlement - (slot.Remaining ?? 0));
                parts.Add($"{name} {used}/{entitlement}");
            }
            else
            {
                parts.Add($"{name} 剩 {slot.PercentRemaining:0.#}%");
            }
        }
        parts.Add(ResetDisplay(usage.ResetDate));

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["plan"] = usage.PlanRaw,
            ["access_type_sku"] = usage.AccessTypeSku,
            ["usage_source"] = "api", // 档①官方口直连（官方扩展同款配额端点）
        };
        if (usage.ResetDate is string reset)
        {
            payload["quota_reset_date"] = reset;
        }
        foreach (var slot in usage.Slots)
        {
            var prefix = slot.Id;
            payload[$"{prefix}_unlimited"] = slot.Unlimited ? "true" : "false";
            if (!slot.Unlimited)
            {
                payload[$"{prefix}_percent_remaining"] = slot.PercentRemaining.ToString("0.#", CultureInfo.InvariantCulture);
                if (slot.Entitlement is long entitlementValue)
                {
                    payload[$"{prefix}_entitlement"] = entitlementValue.ToString("0", CultureInfo.InvariantCulture);
                }
                if (slot.Remaining is long remainingValue)
                {
                    payload[$"{prefix}_remaining"] = remainingValue.ToString("0", CultureInfo.InvariantCulture);
                }
            }
        }

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = CopilotWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = LifecycleState.Success,
            Summary = $"{head} · {string.Join(" · ", parts)}",
            DetailUrl = "https://github.com/settings/copilot",
            Payload = payload,
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>套餐名映射（individual=Pro）；Pro 月付带官方定价 $10/月（access_type_sku=monthly_subscriber_quota 才标，
    /// 年付/其他套餐不标价）。plan 缺失/未知原样显示，不猜。</summary>
    internal static string PlanDisplay(string planRaw, string accessTypeSku)
    {
        return planRaw switch
        {
            "individual" when accessTypeSku == "monthly_subscriber_quota" => "Pro $10/月",
            "individual" => "Pro",
            "individual_next" => "Pro+",
            "business" => "Business",
            "enterprise" => "Enterprise",
            "free" => "Free",
            "" => "套餐未知",
            _ => planRaw,
        };
    }

    private static string SlotName(string id) => id switch
    {
        "chat" => "聊天",
        "completions" => "补全",
        "premium_interactions" => "高级",
        _ => id,
    };

    /// <summary>额度重置日（quota_reset_date ISO 日期 → MM-dd 重置；解析失败原样展示，不编）。</summary>
    internal static string ResetDisplay(string? resetDate)
    {
        if (resetDate is null)
        {
            return "重置日未知";
        }
        if (DateOnly.TryParseExact(resetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return $"{date:MM-dd} 重置";
        }
        return $"{resetDate} 重置";
    }

    private static async Task<string> RequireTokenAsync(ConnectionConfig connection, ConnectionContext context)
    {
        var credentialRef = connection.CredentialRef ?? "copilot:default";
        var token = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ConnectionException("缺少 GitHub PAT：github.com/settings/tokens 创建并勾选 copilot scope 后填连接 Token 框",
                ConnectionHealthState.Degraded);
        }
        return token.Trim();
    }

    private static string Truncate(string value) => value.Length <= 120 ? value : value[..120];
}

/// <summary>三槽配额快照之一（unlimited 槽位 percent/entitlement 无意义）。</summary>
public sealed record CopilotQuotaSlot(
    string Id,
    bool Unlimited,
    double PercentRemaining,
    long? Entitlement,
    long? Remaining);

/// <summary>Copilot 套餐用量（plan 原文 + 三槽快照 + 额度重置日）。</summary>
public sealed record CopilotUsage(
    string PlanRaw,
    string AccessTypeSku,
    string? ResetDate,
    IReadOnlyList<CopilotQuotaSlot> Slots);

/// <summary>copilot 连接：测试连通 = GET copilot_internal/user（同款两头）；PAT 无效 → Degraded（显式失败不静默）。</summary>
public sealed class CopilotConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public CopilotConnectionProvider() { }

    public CopilotConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "copilot";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new CopilotUsageProvider(_handler!).FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
