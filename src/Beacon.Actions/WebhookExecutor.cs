using System.Security.Cryptography;
using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Actions;

/// <summary>
/// webhook（任务书第二阶段动作 / 审计 F-07 收口）：POST URL/体模板 + HMAC-SHA256 签名头。
/// 签名密钥按 credentialRef（参数可覆盖，缺省取 SourceState 连接的 CredentialRef）从 ISecretStore
/// 读取，只进内存参与签名、不进日志不落 JSON（RFC §9.2）；要求签名但取不到密钥时 fail closed
/// （拒发裸请求，宁可不动手也不静默发未认证触发）。签名口径与 GitHub X-Hub-Signature-256 同构：
/// X-Beacon-Signature: sha256=&lt;HMAC-SHA256(body UTF8) 十六进制小写&gt;。
/// </summary>
public sealed class WebhookExecutor : IActionExecutor
{
    public const string DefaultType = "webhook";

    /// <summary>签名头名（接收端按此头 + 共享密钥验签）。</summary>
    public const string SignatureHeader = "X-Beacon-Signature";

    private readonly HttpClient http;

    public WebhookExecutor(HttpMessageHandler? handler = null)
        => http = new HttpClient(handler ?? new HttpClientHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan, // 超时由每次请求的 token 控制
        };

    public string ActionType => DefaultType;

    public async Task<ActionResult> ExecuteAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("url", out var urlTemplate) || string.IsNullOrWhiteSpace(urlTemplate))
        {
            return Fail(action.Id, "缺少参数 url。");
        }

        try
        {
            var vars = ActionVars.Merge(context);
            var url = TemplateRenderer.Render(urlTemplate, vars);
            var body = action.Parameters.TryGetValue("body", out var bodyTemplate) && bodyTemplate is not null
                ? TemplateRenderer.Render(bodyTemplate, vars)
                : string.Empty; // 无体 POST 合法；签名对空体照算

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(double.TryParse(
                action.Parameters.GetValueOrDefault("timeoutSeconds", "15"), out var seconds) ? seconds : 15));

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (var (key, value) in action.Parameters.Where(kv => kv.Key.StartsWith("header.", StringComparison.OrdinalIgnoreCase)))
            {
                request.Headers.TryAddWithoutValidation(key["header.".Length..], value);
            }

            if (!string.Equals(action.Parameters.GetValueOrDefault("sign"), "false", StringComparison.OrdinalIgnoreCase))
            {
                var credentialRef = action.Parameters.GetValueOrDefault("credentialRef")
                    ?? context.Connection?.CredentialRef;
                var secret = credentialRef is null || context.ConnectionContext?.Secrets is null
                    ? null
                    : await context.ConnectionContext.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
                if (string.IsNullOrEmpty(secret))
                {
                    return Fail(action.Id, credentialRef is null
                        ? "签名密钥未配置（credentialRef）"
                        : $"签名密钥未找到：{credentialRef}（明确不需要签名请设 sign=false）");
                }
                request.Headers.TryAddWithoutValidation(SignatureHeader, $"sha256={Sign(body, secret)}");
            }

            if (body.Length > 0)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await http.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (status is >= 200 and < 300)
            {
                return new ActionResult(action.Id, Success: true, Message: $"HTTP {status}", OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
            }
            return Fail(action.Id, $"HTTP {status}（非 2xx 视为失败）");
        }
        catch (ArgumentException exception)
        {
            return Fail(action.Id, exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(action.Id, "webhook 请求超时");
        }
        catch (OperationCanceledException)
        {
            return Fail(action.Id, "已取消");
        }
        catch (HttpRequestException exception)
        {
            return Fail(action.Id, $"webhook 请求失败：{exception.Message}");
        }
    }

    /// <summary>HMAC-SHA256(body UTF8, secret UTF8) → 十六进制小写（纯函数供单测）。</summary>
    internal static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
}
