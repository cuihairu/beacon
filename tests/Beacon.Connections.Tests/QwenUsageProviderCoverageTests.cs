using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

public sealed class QwenUsageProviderCoverageTests
{
    private const string ModelsBody = """
        {"data":[{"id":"qwen3-coder-plus"}]}
        """;

    private const string UsageBody = """
        {"percent":20.5,"credits_remaining":3,"total_credits":10,"period_end":1771000000}
        """;

    private static ConnectionConfig Connection(Dictionary<string, string>? settings = null) => new()
    {
        Id = "qwen-main",
        Type = "qwen",
        CredentialRef = "conn:qwen",
        Settings = settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget() => new()
    {
        Id = "w-qwen",
        Type = QwenWidgetDescriptors.UsageType,
        ConnectionId = "qwen-main",
    };

    private static ConnectionContext Context() => new()
    {
        Secrets = new SecretStoreStub { Secrets = { ["conn:qwen"] = "sk-sp-test" } },
    };

    private static QwenUsageProvider Provider(HttpStatusCode status, string body)
        => new(new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) });

    [Fact]
    public async Task GetStateAsync_CustomUsageEndpoint_ReturnsUsageState()
    {
        var connection = Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_endpoint"] = "http://x/qwen/usage",
        });

        var state = await Provider(HttpStatusCode.OK, UsageBody)
            .GetStateAsync(Widget(), connection, Context(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Equal("custom", state.Payload["usage_source"]);
        Assert.Equal("20.5", state.Payload["percent"]);
        Assert.Equal("3", state.Payload["remaining_credits"]);
    }

    [Fact]
    public async Task GetStateAsync_EmptyCatalog_SkipsQuotaProbe()
    {
        var state = await Provider(HttpStatusCode.OK, """{"data":[]}""")
            .GetStateAsync(Widget(), Connection(), Context(), CancellationToken.None);

        Assert.Equal(Severity.Info, state!.Severity);
        Assert.Equal("0", state.Payload["models"]);
        Assert.Equal("unavailable", state.Payload["usage_source"]);
    }

    [Fact]
    public async Task GetStateAsync_QuotaExhaustedWithoutReset_RendersPlaceholder()
    {
        var provider = new QwenUsageProvider(new FakeHttpMessageHandler
        {
            Responder = request =>
                request.RequestUri!.ToString().EndsWith("/models")
                ? new FakeHttpResponse(HttpStatusCode.OK, ModelsBody)
                : new FakeHttpResponse(HttpStatusCode.TooManyRequests,
                    """{"code":"Throttling.AllocationQuota","message":"plan quota exhausted"}""")
        });

        var state = await provider.GetStateAsync(Widget(), Connection(), Context(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
        Assert.Contains("重置 —", state.Summary);
        Assert.Equal("", state.Payload["reset_iso"]);
    }

    [Fact]
    public void ToExhaustedState_WithoutReset_RendersPlaceholder()
    {
        var state = QwenUsageProvider.ToExhaustedState(
            Widget(),
            Connection(),
            new QwenUsageProvider.QwenCatalog(1, ["qwen-1"]),
            new QwenUsageProvider.QwenQuotaExhausted(null));

        Assert.Contains("重置 —", state.Summary);
        Assert.Equal("", state.Payload["reset_iso"]);
    }

    [Fact]
    public void ParseUsage_MapsCreditsAndEpochReset()
    {
        var usage = QwenUsageProvider.ParseUsage(UsageBody)!;

        Assert.Equal(20.5, usage.PercentUsed);
        Assert.Equal(3, usage.RemainingCredits);
        Assert.Equal(10, usage.TotalCredits);
        Assert.Equal("1771000000", usage.Reset);
    }
}
