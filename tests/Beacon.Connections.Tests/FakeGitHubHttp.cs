using System.Net;
using Beacon.Core.Abstractions;

namespace Beacon.Connections.Tests;

/// <summary>可脚本化的 HttpMessageHandler：按序出队响应，或按请求动态响应（ETag 分支）。</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<FakeHttpResponse> _responses = new();
    private readonly object _gate = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public Func<HttpRequestMessage, FakeHttpResponse>? Responder { get; set; }

    public void Enqueue(FakeHttpResponse response)
    {
        lock (_gate)
        {
            _responses.Enqueue(response);
        }
    }

    public void Enqueue(HttpStatusCode status, string? body = null, string? etag = null, params (string Name, string Value)[] headers)
        => Enqueue(new FakeHttpResponse(status, body, etag, headers));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        FakeHttpResponse scripted;
        lock (_gate)
        {
            scripted = Responder?.Invoke(request) ?? (_responses.Count > 0
                ? _responses.Dequeue()
                : throw new InvalidOperationException("No scripted response for " + request.RequestUri));
        }
        var response = new HttpResponseMessage(scripted.Status)
        {
            Content = new StringContent(scripted.Body ?? "", System.Text.Encoding.UTF8, "application/json"),
        };
        if (scripted.Etag is not null)
        {
            response.Headers.TryAddWithoutValidation("ETag", scripted.Etag);
        }
        foreach (var (name, value) in scripted.Headers ?? [])
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }
        return Task.FromResult(response);
    }
}

internal sealed record FakeHttpResponse(
    HttpStatusCode Status,
    string? Body = null,
    string? Etag = null,
    (string Name, string Value)[]? Headers = null);

internal sealed class SecretStoreStub : ISecretStore
{
    public Dictionary<string, string> Secrets { get; } = new();

    public Task<string?> GetAsync(string credentialRef) => Task.FromResult(Secrets.GetValueOrDefault(credentialRef));

    public Task SetAsync(string credentialRef, string secret)
    {
        Secrets[credentialRef] = secret;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string credentialRef) => Task.CompletedTask;
}

internal static class Fixtures
{
    public const string PullRequests = """
        [
          { "number": 11, "title": "feat: a", "draft": false, "requested_reviewers": [{ "login": "alice" }] },
          { "number": 12, "title": "fix: b", "draft": false, "requested_reviewers": [] },
          { "number": 13, "title": "wip: c", "draft": true, "requested_reviewers": [] }
        ]
        """;

    public const string ActionsFailedRun = """
        {
          "total_count": 1,
          "workflow_runs": [
            {
              "id": 987654,
              "status": "completed",
              "conclusion": "failure",
              "head_branch": "main",
              "html_url": "https://github.com/owner/repo/actions/runs/987654",
              "created_at": "2026-01-01T10:00:00Z",
              "run_started_at": "2026-01-01T10:00:00Z",
              "updated_at": "2026-01-01T10:03:12Z"
            }
          ]
        }
        """;
}
