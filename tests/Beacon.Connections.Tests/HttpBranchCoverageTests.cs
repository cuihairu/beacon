using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// HTTP 面错误/边界分支收口：GitHubApiClient 状态映射与网络故障、HttpEndpoint 便捷口/超时/404、
/// HttpJsonPath 非标量、Jenkins 未知颜色与状态、GitHubActions 未知运行态/缺字段、
/// PullRequests 304-后空仓探针、MachineNet 真实网卡采样与点名缺失。
/// </summary>
public sealed class HttpBranchCoverageTests
{
    // ---------- GitHubApiClient ----------

    private static GitHubApiClient CreateClient(FakeHttpMessageHandler handler, string? token = "pat-test")
        => new("https://api.github.test/", token, handler);

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.Conflict, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    [InlineData((HttpStatusCode)418, ConnectionHealthState.Degraded)]
    public async Task GetAsync_StatusMappings_ThrowConnectionException(HttpStatusCode status, ConnectionHealthState expected)
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, "{}") };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/repos/o/r/pulls"));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task GetAsync_NetworkFailure_MapsToOffline()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => throw new HttpRequestException("connection refused"),
        };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/repos/o/r"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("GitHub unreachable", exception.Message);
    }

    [Fact]
    public async Task GetAsync_Timeout_MapsToOffline()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => throw new TaskCanceledException(),
        };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/repos/o/r"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task PostAsync_UnexpectedStatus_MapsToDegraded()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse((HttpStatusCode)418, "{}") };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => client.PostAsync("/repos/o/r/actions/runs/1/cancel"));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---------- HttpEndpoint ----------

    private static ConnectionConfig HttpConnection(string? endpoint) => new()
    {
        Id = "http-main",
        Type = "http",
        Endpoint = endpoint,
        CredentialRef = null,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public async Task FetchAsync_ConvenienceSharedClient_UnreachableMapsToOffline()
    {
        var context = new ConnectionContext { Secrets = new SecretStoreStub() };

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => HttpEndpoint.FetchAsync(HttpConnection("http://127.0.0.1:1/"), context, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("接口不可达", exception.Message);
    }

    [Fact]
    public async Task FetchAsync_HttpQuota_TimeoutMapsToOffline()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => throw new TaskCanceledException() };
        var provider = new HttpQuotaProvider(http);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(QuotaWidget(), HttpConnection("http://quota.test/"),
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("超时（15s）", exception.Message);
    }

    [Fact]
    public async Task FetchAsync_HttpQuota_NotFound404_MapsToOffline()
    {
        var (provider, _) = QuotaFaked(HttpStatusCode.NotFound, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(QuotaWidget(), HttpConnection("http://quota.test/"),
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("接口不存在 (404)", exception.Message);
    }

    private static WidgetConfig QuotaWidget() => new()
    {
        Id = "w-http",
        Type = "http.quota",
        ConnectionId = "http-main",
        Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static (HttpQuotaProvider Provider, FakeHttpMessageHandler Http) QuotaFaked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new HttpQuotaProvider(http), http);
    }

    // ---------- HttpJsonPath ----------

    [Fact]
    public void Select_ArrayIndexNotANumber_ReturnsNull()
        => Assert.Null(HttpJsonPath.Select(System.Text.Json.JsonDocument.Parse("""{"items":[1,2]}""").RootElement, "$.items[abc]"));

    [Fact]
    public void Select_BooleanValues_MapToStrings()
    {
        var root = System.Text.Json.JsonDocument.Parse("""{"a":true,"b":false,"c":null}""").RootElement;
        Assert.Equal("true", HttpJsonPath.Select(root, "$.a"));
        Assert.Equal("false", HttpJsonPath.Select(root, "$.b"));
        Assert.Null(HttpJsonPath.Select(root, "$.c"));
    }

    // ---------- Jenkins ----------

    private static ConnectionConfig JenkinsConnection(string? endpoint = "https://weekly.ci.jenkins.io") => new()
    {
        Id = "jenkins-main",
        Type = "jenkins",
        Endpoint = endpoint,
        CredentialRef = null,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig JenkinsWidget() => new()
    {
        Id = "w-jenkins",
        Type = "jenkins.build",
        ConnectionId = "jenkins-main",
        Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["job_path"] = "job/demo",
        },
    };

    private static (JenkinsBuildProvider Provider, FakeHttpMessageHandler Http) JenkinsFaked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new JenkinsBuildProvider(http), http);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "接口不存在|Job 不存在")]
    [InlineData(HttpStatusCode.InternalServerError, "接口异常")]
    public async Task Jenkins_Non2xxStatus_MapsToOffline(HttpStatusCode status, string expectedFragment)
    {
        var (provider, _) = JenkinsFaked(status, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(JenkinsWidget(), JenkinsConnection(),
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Matches(expectedFragment, exception.Message);
    }

    [Fact]
    public async Task Jenkins_Timeout_MapsToOffline()
    {
        var (provider, _) = JenkinsFaked(HttpStatusCode.OK, "{}");
        var http = new FakeHttpMessageHandler { Responder = _ => throw new TaskCanceledException() };
        var timeoutProvider = new JenkinsBuildProvider(http);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => timeoutProvider.GetStateAsync(JenkinsWidget(), JenkinsConnection(),
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("请求超时（15s）", exception.Message);
    }

    [Fact]
    public void MapColor_UnknownColor_IsUnknownWarning()
    {
        var (lifecycle, severity, phase) = JenkinsBuildProvider.MapColor("purple");
        Assert.Equal(LifecycleState.Unknown, lifecycle);
        Assert.Equal(Severity.Warning, severity);
        Assert.Equal("purple", phase);
    }

    // ---------- GitHubActions ----------

    [Fact]
    public void MapRun_UnknownStatus_IsUnknownWarning()
    {
        var (lifecycle, severity) = GitHubActionsProvider.MapRun("mystery-status", null, null);
        Assert.Equal(LifecycleState.Unknown, lifecycle);
        Assert.Equal(Severity.Warning, severity);
    }

    [Fact]
    public async Task GetStateAsync_MissingRequiredField_MapsToDegraded()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}") };
        GitHubClientCache.SetForTests("gh-main", new GitHubApiClient("https://api.github.test/", "pat", handler));
        try
        {
            var provider = new GitHubActionsProvider();
            var connection = new ConnectionConfig
            {
                Id = "gh-main",
                Type = "github",
                Endpoint = "https://api.github.test/",
            };
            var widget = new WidgetConfig
            {
                Id = "w-ci",
                Type = GitHubWidgetDescriptors.ActionsRunsType,
                ConnectionId = "gh-main",
                Config = new Dictionary<string, string>
                {
                    ["repo"] = "owner/repo",
                    ["workflow"] = "ci.yml",
                },
            };

            var exception = await Assert.ThrowsAsync<ConnectionException>(
                () => provider.GetStateAsync(widget, connection,
                    new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None));

            Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
            Assert.Contains("缺少字段", exception.Message);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    // ---------- GitHubPullRequests ----------

    [Fact]
    public async Task GetStateAsync_304AfterEmptyRepo_ProbeReturnsNull()
    {
        var requestCount = 0;
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => ++requestCount == 1
                ? new FakeHttpResponse(HttpStatusCode.OK, "[]")
                : new FakeHttpResponse(HttpStatusCode.NotModified),
        };
        GitHubClientCache.SetForTests("gh-main", new GitHubApiClient("https://api.github.test/", "pat", handler));
        try
        {
            var provider = new GitHubPullRequestsProvider();
            var connection = new ConnectionConfig
            {
                Id = "gh-main",
                Type = "github",
                Endpoint = "https://api.github.test/",
            };
            var widget = new WidgetConfig
            {
                Id = "w-pr",
                Type = GitHubWidgetDescriptors.PullRequestsType,
                ConnectionId = "gh-main",
                Config = new Dictionary<string, string>
                {
                    ["repo"] = "owner/repo",
                    ["warnOnRedCi"] = "true",
                },
            };

            var first = await provider.GetStateAsync(widget, connection,
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None);
            var second = await provider.GetStateAsync(widget, connection,
                new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None);

            Assert.NotNull(first); // 空仓库首次 = 正常 Success 状态
            Assert.Equal("0", first!.Payload["open_count"]);
            Assert.Null(second); // 304 + 上次就是空仓 → 无可续探，宿主沿用缓存
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    // ---------- MachineNet（真实网卡源） ----------

    [Fact]
    public void Sample_RealInterface_PopulatesSnapshot()
    {
        var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        Assert.NotEmpty(nics);
        var name = nics.FirstOrDefault(n => n.Name == "lo")?.Name ?? nics[0].Name;

        var sample = new NetworkInterfaceNetSource().Sample(name);

        Assert.Equal(name, sample.InterfaceName);
        Assert.True(sample.SentBytes >= 0);
        Assert.True(sample.ReceivedBytes >= 0);
    }

    [Fact]
    public void Sample_NamedInterfaceMissing_ThrowsDegraded()
    {
        var exception = Assert.Throws<ConnectionException>(
            () => new NetworkInterfaceNetSource().Sample("beacon-missing-nic-xyz"));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("找不到网卡", exception.Message);
    }
}
