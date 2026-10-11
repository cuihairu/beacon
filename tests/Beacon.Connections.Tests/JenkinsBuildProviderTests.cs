using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// jenkins.build（开源 Jenkins 标准 JSON API，master-plan §13 打包工具槽位）：
/// weekly.ci.jenkins.io 真实夹具（freestyle blue #1 / pipeline red #22 / test notbuilt）、
/// Folder 路径逐段转义、color→级别映射（_anime=构建中）、Basic 匿名两态、SPA 假阳性防御。
/// </summary>
public sealed class JenkinsBuildProviderTests
{
    // weekly.ci.jenkins.io/job/freestyle/api/json 真实抓取（裁到判级所需键；原文见 /tmp 夹具）
    private const string FreestyleJob = """
        {
          "_class": "hudson.model.FreeStyleProject",
          "displayName": "freestyle",
          "name": "freestyle",
          "url": "https://weekly.ci.jenkins.io/job/freestyle/",
          "color": "blue",
          "buildable": true,
          "lastBuild": { "_class": "hudson.model.FreeStyleBuild", "number": 1, "url": "https://weekly.ci.jenkins.io/job/freestyle/1/" },
          "healthReport": [
            { "description": "Tests: 0 tests failing out of a total of 12 tests.", "iconClassName": "icon-health-80plus", "score": 100 },
            { "description": "Build stability: No recent builds failed.", "score": 100 }
          ],
          "inQueue": false,
          "disabled": false
        }
        """;

    // jobs-tree.json 抓取的 pipeline job（color=red，lastBuild #22 FAILURE ts 1772187495495——注意 job 级无 result）
    private const string RedPipelineJob = """
        {
          "displayName": "pipeline",
          "name": "pipeline",
          "url": "https://weekly.ci.jenkins.io/job/pipeline/",
          "color": "red",
          "lastBuild": { "number": 22, "url": "https://weekly.ci.jenkins.io/job/pipeline/22/" },
          "healthReport": [ { "description": "Build stability: All recent builds failed.", "score": 0 } ],
          "disabled": false
        }
        """;

    // jobs-tree.json 抓取的 test job（从未构建：color=notbuilt、无 lastBuild 键）
    private const string NotBuiltJob = """
        {
          "displayName": "test",
          "name": "test",
          "url": "https://weekly.ci.jenkins.io/job/test/",
          "color": "notbuilt",
          "disabled": false
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = "https://weekly.ci.jenkins.io", string? credentialRef = null)
        => new()
        {
            Id = "jenkins-main",
            Type = "jenkins",
            Endpoint = endpoint,
            CredentialRef = credentialRef,
        };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-jenkins",
        Type = JenkinsWidgetDescriptors.BuildType,
        ConnectionId = "jenkins-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["job_path"] = "freestyle",
        },
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:jenkins"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (JenkinsBuildProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new JenkinsBuildProvider(http), http);
    }

    // ---- URL 拼装（Folder 逐段转义） ----

    [Theory]
    [InlineData("https://jenkins.example.com", "my-pipeline",
        "https://jenkins.example.com/job/my-pipeline/api/json?tree=")]
    [InlineData("https://jenkins.example.com/", "team/app/build",
        "https://jenkins.example.com/job/team/job/app/job/build/api/json?tree=")]
    [InlineData("https://jenkins.example.com/jenkins", "my job",
        "https://jenkins.example.com/jenkins/job/my%20job/api/json?tree=")]
    public void JobUrl_EscapesEachSegment(string endpoint, string jobPath, string expectedPrefix)
    {
        var url = JenkinsBuildProvider.JobUrl(endpoint, jobPath);

        Assert.StartsWith(expectedPrefix, url);
        Assert.Contains("name%2CdisplayName", url); // tree 参数已编码
    }

    [Fact]
    public void JobUrl_MissingEndpoint_ThrowsDegraded()
        => Assert.Equal(ConnectionHealthState.Degraded,
            Assert.Throws<ConnectionException>(() => JenkinsBuildProvider.JobUrl(null, "job")).Health);

    [Fact]
    public void JobUrl_BlankJobPath_ThrowsDegraded()
        => Assert.Equal(ConnectionHealthState.Degraded,
            Assert.Throws<ConnectionException>(() => JenkinsBuildProvider.JobUrl("https://jenkins.example.com", " / ")).Health);

    // ---- color → 级别映射（真实词汇表） ----

    [Theory]
    [InlineData("blue", LifecycleState.Success, Severity.Success, "成功")]
    [InlineData("blue_anime", LifecycleState.Running, Severity.Info, "运行中")]
    [InlineData("yellow", LifecycleState.Success, Severity.Warning, "不稳定")]
    [InlineData("red", LifecycleState.Failed, Severity.Error, "失败")]
    [InlineData("red_anime", LifecycleState.Running, Severity.Error, "运行中")]
    [InlineData("dark_red", LifecycleState.Failed, Severity.Error, "失败")]
    [InlineData("aborted", LifecycleState.Cancelled, Severity.Warning, "已中止")]
    [InlineData("disabled", LifecycleState.Skipped, Severity.Info, "已禁用")]
    [InlineData("gray", LifecycleState.Unknown, Severity.Info, "未构建")]
    [InlineData("notbuilt", LifecycleState.Unknown, Severity.Info, "未构建")]
    [InlineData(null, LifecycleState.Unknown, Severity.Warning, "未知")]
    public void MapColor_RealVocabulary(string? color, LifecycleState lifecycle, Severity severity, string phase)
    {
        var (actualLifecycle, actualSeverity, actualPhase) = JenkinsBuildProvider.MapColor(color);

        Assert.Equal(lifecycle, actualLifecycle);
        Assert.Equal(severity, actualSeverity);
        Assert.Equal(phase, actualPhase);
    }

    [Fact]
    public void IsBuilding_AnimeSuffix()
    {
        Assert.True(JenkinsBuildProvider.IsBuilding("blue_anime"));
        Assert.False(JenkinsBuildProvider.IsBuilding("blue"));
        Assert.False(JenkinsBuildProvider.IsBuilding(null));
    }

    // ---- 解析（真实夹具） ----

    [Fact]
    public void ParseJob_RealFreestyleFixture()
    {
        var job = JenkinsBuildProvider.ParseJob(FreestyleJob)!;

        Assert.Equal("freestyle", job.Name);
        Assert.Equal("blue", job.Color);
        Assert.Equal(1, job.LastBuildNumber);
        Assert.Equal("https://weekly.ci.jenkins.io/job/freestyle/1/", job.LastBuildUrl);
        Assert.Equal(100, job.HealthScore);
        Assert.False(job.Disabled);
    }

    [Fact]
    public void ParseJob_NonJenkinsPayload_ReturnsNull()
    {
        Assert.Null(JenkinsBuildProvider.ParseJob("""<html><body>SPA shell</body></html>"""));
        Assert.Null(JenkinsBuildProvider.ParseJob("""{"jobs":[]}""")); // 根视图无 color 键
        Assert.Null(JenkinsBuildProvider.ParseJob("not json"));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_BlueJob_SuccessWithBuildNumberAndHealth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, FreestyleJob);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("freestyle · #1 成功", state.Summary);
        Assert.Equal("1", state.Payload["build_number"]);
        Assert.Equal("https://weekly.ci.jenkins.io/job/freestyle/1/", state.Payload["build_url"]);
        Assert.Equal("0", state.Payload["building"]);
        Assert.Equal("100", state.Payload["health_score"]);
        Assert.Contains("12 tests", state.Payload["health_text"]);
        Assert.Equal("https://weekly.ci.jenkins.io/job/freestyle/", state.DetailUrl);
        Assert.Equal("https://weekly.ci.jenkins.io/job/freestyle/api/json?tree=name%2CdisplayName%2Ccolor%2Curl%2ClastBuild%5Bnumber%2Curl%5D%2ChealthReport%5Bscore%2Cdescription%5D",
            http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_RedJob_Failed()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, RedPipelineJob);

        var state = await provider.GetStateAsync(
            Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["job_path"] = "pipeline" }),
            Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal("pipeline · #22 失败", state.Summary); // job 级无 result，判级全靠 color
        Assert.Equal("0", state.Payload["health_score"]);
    }

    [Fact]
    public async Task GetStateAsync_NeverBuiltJob_InfoNotBuilt()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, NotBuiltJob);

        var state = await provider.GetStateAsync(
            Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["job_path"] = "test" }),
            Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Info, state!.Severity);
        Assert.Equal(LifecycleState.Unknown, state.Lifecycle);
        Assert.Equal("test · 未构建", state.Summary);
        Assert.Equal("", state.Payload["build_number"]);
    }

    [Fact]
    public async Task GetStateAsync_FolderJob_BuildsEscapedUrlAndUsesDisplayName()
    {
        const string folderJob = """{"displayName":"app · build","name":"build","color":"blue_anime","url":"https://jenkins.example.com/job/team/job/app/job/build/","lastBuild":{"number":7,"url":"https://jenkins.example.com/job/team/job/app/job/build/7/"}}""";
        var (provider, http) = Faked(HttpStatusCode.OK, folderJob);

        var state = await provider.GetStateAsync(
            Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["job_path"] = "team/app/build" }),
            Connection("https://jenkins.example.com"), Ctx(), CancellationToken.None);

        Assert.Equal("https://jenkins.example.com/job/team/job/app/job/build/api/json",
            http.Requests[0].RequestUri!.GetLeftPart(System.UriPartial.Path));
        Assert.Equal(LifecycleState.Running, state!.Lifecycle); // _anime=构建中
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal("app · build · #7 运行中", state.Summary); // displayName 优先
        Assert.Equal("1", state.Payload["building"]);
    }

    [Fact]
    public async Task GetStateAsync_CredentialPresent_SendsBasicAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, FreestyleJob);
        var connection = Connection(credentialRef: "conn:jenkins");

        await provider.GetStateAsync(Widget(), connection, Ctx("ci-user:117a1c4"), CancellationToken.None);

        Assert.Equal("Basic", http.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String("ci-user:117a1c4"u8.ToArray()), http.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetStateAsync_Unauthorized_HintsApiToken()
    {
        var (provider, _) = Faked(HttpStatusCode.Unauthorized, """"{"code":401}"""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health);
        Assert.Contains("apiToken", exception.Message);
    }

    [Fact]
    public async Task GetStateAsync_NonJenkins200_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """<html>reverse proxy shell</html>""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health); // 地址不对不冒充状态
    }

    // ---- 连接测试 ----

    [Fact]
    public async Task TestAsync_HealthyOnJenkinsRootJson_DegradedOnSpaShell()
    {
        var http = new FakeHttpMessageHandler();
        var provider = new JenkinsConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"_class":"hudson.model.Hudson","jobs":[],"nodeDescription":"master"}""");
        var healthy = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """<html>nginx default</html>""");
        var spaShell = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, """{"code":401}""");
        var unauthorized = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, healthy.Health);
        Assert.Equal(ConnectionHealthState.Degraded, spaShell.Health);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized.Health);
    }

    [Fact]
    public async Task TestAsync_MissingEndpoint_DegradedWithHint()
    {
        var provider = new JenkinsConnectionProvider();

        var result = await provider.TestAsync(Connection(endpoint: null), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("Endpoint", result.Detail);
    }
}
