using System.Globalization;
using System.Text;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>jenkins（开源 CI 打包工具槽位，master-plan §13）：Jenkins 构建状态组件类型元数据。</summary>
public static class JenkinsWidgetDescriptors
{
    public const string BuildType = "jenkins.build";

    public static WidgetTypeDescriptor Build { get; } = new()
    {
        Type = BuildType,
        DisplayName = "Jenkins 构建状态",
        PinSupported = true, // 状态 tile：不出悬浮数值卡（数值/额度类才 FloatingOptIn，同 github.actions.runs 口径）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("job_path", "Job 路径", Required: true, Placeholder: "folder/my-pipeline（Folder 用 / 分隔）"),
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "构建"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Build];
}

/// <summary>
/// jenkins.build：开源 Jenkins 标准 JSON API（任意实例通用，非厂商专属）。
/// job 端点：{base}/job/{name}/api/json——Folder 路径逐段转义（weekly.ci.jenkins.io 真实夹具实证：
/// freestyle color=blue / history-coverage-model color=red / test color=notbuilt）。
/// color → 级别：blue→Success、yellow→Warning（不稳定）、red/dark_red→Error、aborted→Cancelled、
/// gray/notbuilt→未构建 Info、disabled→已禁用 Info；`_anime` 后缀=正在构建 → Lifecycle Running。
/// 注意 job 级 lastBuild **不含 result** 字段（真实夹具实证），一次请求按 color 判级，不追打第二击。
/// 连接：Endpoint=Jenkins 基址；凭据可空（匿名读），填 user:apiToken 走 Basic 认证。
/// </summary>
public sealed class JenkinsBuildProvider : IWidgetProvider
{
    /// <summary>tree 参数裁剪响应体（标准 API 支持；键名与真实夹具逐一对应）。</summary>
    public const string JobTree = "name,displayName,color,url,lastBuild[number,url],healthReport[score,description]";

    private readonly HttpClient? _client;

    public JenkinsBuildProvider() { }

    public JenkinsBuildProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => JenkinsWidgetDescriptors.Build;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var jobPath = GitHubPullRequestsProvider.RequireConfig(widget, "job_path");
        var url = JobUrl(connection.Endpoint, jobPath);
        var body = await FetchAsync(url, connection, context, cancellationToken, _client).ConfigureAwait(false);
        var job = ParseJob(body)
            ?? throw new ConnectionException("Jenkins 响应不含 job 数据（地址或 Job 路径不对）", ConnectionHealthState.Degraded);

        var (lifecycle, severity, phase) = MapColor(job.Color);
        var head = widget.Config.GetValueOrDefault("label") ?? job.DisplayName ?? job.Name ?? jobPath;
        var buildNumber = job.LastBuildNumber?.ToString("0", CultureInfo.InvariantCulture) ?? "";
        var summary = job.Disabled ? $"{head} · 已禁用"
            : buildNumber.Length == 0 ? $"{head} · 未构建"
            : $"{head} · #{buildNumber} {phase}";

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = lifecycle,
            Progress = null, // 无进度口径：正在构建按 Lifecycle 显示不确定进度
            Summary = summary,
            DetailUrl = job.Url ?? url[..url.IndexOf("/api/json", StringComparison.Ordinal)],
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["job"] = job.Name ?? jobPath,
                ["job_path"] = jobPath,
                ["color"] = job.Color ?? "",
                ["building"] = IsBuilding(job.Color) ? "1" : "0",
                ["build_number"] = buildNumber,
                ["build_url"] = job.LastBuildUrl ?? "",
                ["health_score"] = job.HealthScore?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["health_text"] = job.HealthDescription ?? "",
                ["phase"] = phase,
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>job 端点拼装（Folder 路径逐段 job/ 前缀 + 转义；空路径 Degraded 不发请求）。</summary>
    internal static string JobUrl(string? baseEndpoint, string jobPath)
    {
        var baseUri = (baseEndpoint?.Trim().Length > 0 ? baseEndpoint!.Trim() : "").TrimEnd('/');
        if (baseUri.Length == 0)
        {
            throw new ConnectionException("Jenkins 连接缺少 Endpoint（实例基址，如 https://jenkins.example.com）",
                ConnectionHealthState.Degraded);
        }
        var segments = new List<string>();
        foreach (var raw in jobPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            segments.Add($"job/{Uri.EscapeDataString(raw)}");
        }
        if (segments.Count == 0)
        {
            throw new ConnectionException("Jenkins 组件缺少 job_path。", ConnectionHealthState.Degraded);
        }
        return $"{baseUri}/{string.Join("/", segments)}/api/json?tree={Uri.EscapeDataString(JobTree)}";
    }

    internal static async Task<string> FetchAsync(
        string url,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken,
        HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // 凭据可空（匿名读实例）；填了按 user:apiToken 走 Basic
        if (!string.IsNullOrWhiteSpace(connection.CredentialRef))
        {
            var secret = (await context.Secrets.GetAsync(connection.CredentialRef).ConfigureAwait(false))?.Trim();
            if (!string.IsNullOrEmpty(secret))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)));
            }
        }
        using var response = await (client ?? HttpEndpoint.Shared).SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            throw status switch
            {
                401 or 403 => new ConnectionException($"Jenkins 认证失败 ({status})：填 user:apiToken（Manage Jenkins → Users → API Token）。",
                    ConnectionHealthState.Unauthorized),
                404 => new ConnectionException("Jenkins Job 不存在 (404)：检查 job_path（Folder 路径用 / 分隔）。",
                    ConnectionHealthState.Offline),
                _ => new ConnectionException($"Jenkins 接口异常 ({status})。", ConnectionHealthState.Offline),
            };
        }
        return body;
    }

    /// <summary>job JSON（真实夹具形状）：name/displayName/color/url + lastBuild{number,url} + healthReport[0]。</summary>
    internal sealed record JenkinsJob(
        string? Name,
        string? DisplayName,
        string? Color,
        string? Url,
        long? LastBuildNumber,
        string? LastBuildUrl,
        double? HealthScore,
        string? HealthDescription,
        bool Disabled);

    internal static JenkinsJob? ParseJob(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("color", out var colorElement))
            {
                return null; // 无 color 键：非 Jenkins job JSON（SPA 壳/错误页），不冒充状态
            }
            var name = TryString(root, "name");
            var lastBuild = root.TryGetProperty("lastBuild", out var lastBuildElement) && lastBuildElement.ValueKind == JsonValueKind.Object
                ? lastBuildElement
                : default;
            var health = root.TryGetProperty("healthReport", out var healthElement) && healthElement.ValueKind == JsonValueKind.Array
                && healthElement.GetArrayLength() > 0
                    ? healthElement[0]
                    : default;
            return new JenkinsJob(
                name,
                TryString(root, "displayName") ?? name,
                colorElement.ValueKind == JsonValueKind.String ? colorElement.GetString() : null,
                TryString(root, "url"),
                lastBuild.ValueKind == JsonValueKind.Object && lastBuild.TryGetProperty("number", out var numberElement) && numberElement.TryGetInt64(out var number)
                    ? number
                    : null,
                lastBuild.ValueKind == JsonValueKind.Object ? TryString(lastBuild, "url") : null,
                health.ValueKind == JsonValueKind.Object && health.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var score)
                    ? score
                    : null,
                health.ValueKind == JsonValueKind.Object ? TryString(health, "description") : null,
                root.TryGetProperty("disabled", out var disabledElement) && disabledElement.ValueKind == JsonValueKind.True);
        }
    }

    private static string? TryString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>正在构建：Jenkins color 带 _anime 后缀（blue_anime/red_anime 真实词汇表）。</summary>
    internal static bool IsBuilding(string? color) => color?.EndsWith("_anime", StringComparison.Ordinal) ?? false;

    /// <summary>color → （Lifecycle, Severity, 中文阶段词）。未知颜色退 Warning（宁报勿漏）。</summary>
    internal static (LifecycleState Lifecycle, Severity Severity, string Phase) MapColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return (LifecycleState.Unknown, Severity.Warning, "未知");
        }
        var building = IsBuilding(color);
        var core = building ? color[..^"_anime".Length] : color;
        return core switch
        {
            "blue" => building ? (LifecycleState.Running, Severity.Info, "运行中") : (LifecycleState.Success, Severity.Success, "成功"),
            "yellow" => building ? (LifecycleState.Running, Severity.Warning, "运行中") : (LifecycleState.Success, Severity.Warning, "不稳定"),
            "red" or "dark_red" => building ? (LifecycleState.Running, Severity.Error, "运行中") : (LifecycleState.Failed, Severity.Error, "失败"),
            "aborted" => building ? (LifecycleState.Running, Severity.Warning, "运行中") : (LifecycleState.Cancelled, Severity.Warning, "已中止"),
            "disabled" => (LifecycleState.Skipped, Severity.Info, "已禁用"),
            "gray" or "notbuilt" => (LifecycleState.Unknown, Severity.Info, "未构建"),
            _ => (LifecycleState.Unknown, Severity.Warning, core),
        };
    }
}

/// <summary>jenkins 连接：测试 = GET {base}/api/json（根视图 JSON），2xx 且是 JSON 才健康（防 SPA 假阳性）。</summary>
public sealed class JenkinsConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public JenkinsConnectionProvider() { }

    public JenkinsConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "jenkins";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var baseUri = connection.Endpoint?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUri))
            {
                return new ConnectionTestResult(ConnectionHealthState.Degraded, "缺少 Endpoint（Jenkins 实例基址，如 https://jenkins.example.com）");
            }
            var body = await JenkinsBuildProvider.FetchAsync($"{baseUri}/api/json", connection, context, cancellationToken, _client).ConfigureAwait(false);
            // 匿名可读实例回根视图 JSON（含 jobs/节点描述等）；200 但不是 Jenkins JSON → Degraded（地址填错不冒充连通）
            return body.TrimStart().StartsWith("{") ? ConnectionTestResult.Ok()
                : new ConnectionTestResult(ConnectionHealthState.Degraded, "响应不是 Jenkins JSON：检查 Endpoint 是否指向实例根地址");
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
