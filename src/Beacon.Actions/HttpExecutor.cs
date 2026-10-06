using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Services;
using Beacon.Core.Models;

namespace Beacon.Actions;

/// <summary>
/// http（B-405）：方法/URL/头/体模板执行，非 2xx 视为失败并回报状态码。
/// handler 可注入（单测假 Handler / 本地 stub）。
/// </summary>
public sealed class HttpExecutor : IActionExecutor
{
    public static readonly string DefaultType = "http";

    private static readonly string[] KnownBodyParams = ["url", "method", "body", "timeoutSeconds"];

    private readonly HttpClient http;

    public HttpExecutor(HttpMessageHandler? handler = null)
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

        string url;
        try
        {
            var vars = ActionVars.Merge(context);
            url = TemplateRenderer.Render(urlTemplate, vars);
            action.Parameters.TryGetValue("body", out var bodyTemplate);
            var body = bodyTemplate is null ? null : TemplateRenderer.Render(bodyTemplate, vars);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(double.TryParse(
                action.Parameters.GetValueOrDefault("timeoutSeconds", "15"), out var seconds) ? seconds : 15));
            using var request = new HttpRequestMessage(
                new HttpMethod(action.Parameters.GetValueOrDefault("method", "GET").ToUpperInvariant()),
                url);
            foreach (var (key, value) in action.Parameters.Where(kv => kv.Key.StartsWith("header.", StringComparison.OrdinalIgnoreCase)))
            {
                request.Headers.TryAddWithoutValidation(key["header.".Length..], value);
            }
            if (body is not null)
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
            return Fail(action.Id, "HTTP 请求超时");
        }
        catch (OperationCanceledException)
        {
            return Fail(action.Id, "已取消");
        }
        catch (HttpRequestException exception)
        {
            return Fail(action.Id, $"HTTP 请求失败：{exception.Message}");
        }
    }

    private static ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
}
