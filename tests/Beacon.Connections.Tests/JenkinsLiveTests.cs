using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// jenkins.build 真实 Jenkins 端到端（2026-11-06 用户令「开源的就自己搭」）：
/// docker jenkins/jenkins:lts-jdk17（Jenkins 2.541.3）实证 Provider 的端点/认证/解析与真实实例一致——
/// 连接 GET {base}/api/json、组件 GET {base}/job/{path}/api/json?tree=JobTree、admin API Token 走
/// Basic（真实 token 创建：POST /user/admin/descriptorByName/jenkins.security.ApiTokenProperty/generateNewToken，
/// 注意真实描述符是 jenkins.security.ApiTokenProperty/generateNewToken，非旧资料的
/// jenkins.api.ApiTokenProperty/createToken——本机 2.541.3 实测后者 404）、404→Offline、401→Unauthorized。
/// opt-in：BEACON_LIVE_JENKINS_URL（如 http://localhost:18087）未设 → 提前返回不发请求，离线 CI 恒绿
/// （同 GitHubPublicApiLiveTests 口径）；BEACON_LIVE_JENKINS_AUTH = user:apiToken 时走 token 认证并启用 401 用例；
/// BEACON_LIVE_JENKINS_JOB 组件路径（默认 beacon-e2e，需已成功构建过 → color=blue）。
/// </summary>
public sealed class JenkinsLiveTests
{
    private static string? Endpoint => Environment.GetEnvironmentVariable("BEACON_LIVE_JENKINS_URL");

    private static string? Auth => Environment.GetEnvironmentVariable("BEACON_LIVE_JENKINS_AUTH");

    private static string JobPath => Environment.GetEnvironmentVariable("BEACON_LIVE_JENKINS_JOB") ?? "beacon-e2e";

    private static bool LiveEnabled => Endpoint is not null;

    private static (ConnectionConfig Connection, ConnectionContext Context) Connection()
    {
        var useAuth = !string.IsNullOrWhiteSpace(Auth);
        var secrets = new SecretStoreStub();
        if (useAuth)
        {
            secrets.Secrets["conn:jenkins-live"] = Auth!;
        }
        return (new ConnectionConfig
        {
            Id = "jenkins-live",
            Type = "jenkins",
            Endpoint = Endpoint,
            CredentialRef = useAuth ? "conn:jenkins-live" : null,
        }, new ConnectionContext { Secrets = secrets });
    }

    private static WidgetConfig Widget(string jobPath) => new()
    {
        Id = "w-jenkins-live",
        Type = JenkinsWidgetDescriptors.BuildType,
        ConnectionId = "jenkins-live",
        Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["job_path"] = jobPath },
    };

    [Fact]
    public async Task ConnectionTest_RealJenkins_RootJsonHealthy()
    {
        if (!LiveEnabled) return; // opt-in：离线 CI 跳过

        var (connection, context) = Connection();
        var result = await new JenkinsConnectionProvider()
            .TestAsync(connection, context, CancellationToken.None);

        // 真实实例根视图 200 且是 JSON（带 token 或匿名读实例同口径）→ Healthy
        Assert.Equal(ConnectionHealthState.Healthy, result.Health);
    }

    [Fact]
    public async Task GetStateAsync_RealJob_TreeResponseMapsToState()
    {
        if (!LiveEnabled) return; // opt-in：离线 CI 跳过

        var (connection, context) = Connection();
        var state = await new JenkinsBuildProvider()
            .GetStateAsync(Widget(JobPath), connection, context, CancellationToken.None);

        Assert.NotNull(state);
        var color = state.Payload["color"]; // 真实响应 color 键 → 判级唯一输入
        Assert.False(string.IsNullOrWhiteSpace(color));
        var (lifecycle, severity, phase) = JenkinsBuildProvider.MapColor(color);
        Assert.Equal(lifecycle, state.Lifecycle); // 取数 → 解析 → 映射与真实 color 一致
        Assert.Equal(severity, state.Severity);
        Assert.Contains(phase, state.Summary); // 摘要带真实阶段词（如「成功」）
        Assert.StartsWith($"{Endpoint!.TrimEnd('/')}/job/", state.DetailUrl); // DetailUrl = 真实 job url
        Assert.Equal(JobPath, state.Payload["job_path"]);
        if (color == "blue") // 已成功构建的实例（本机 docker 夹具）：构建号/链接齐
        {
            Assert.Equal(LifecycleState.Success, state.Lifecycle);
            Assert.False(string.IsNullOrEmpty(state.Payload["build_number"]));
            Assert.StartsWith($"{Endpoint.TrimEnd('/')}/job/", state.Payload["build_url"]);
            Assert.Equal("0", state.Payload["building"]);
        }
    }

    [Fact]
    public async Task GetStateAsync_MissingJob_Real404Offline()
    {
        if (!LiveEnabled) return; // opt-in：离线 CI 跳过

        var (connection, context) = Connection();
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => new JenkinsBuildProvider()
            .GetStateAsync(Widget("beacon-e2e-no-such-job-404"), connection, context, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health); // 真实 404 → Offline（非冒充状态）
        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public async Task GetStateAsync_WrongApiToken_Real401Unauthorized()
    {
        if (!LiveEnabled || string.IsNullOrWhiteSpace(Auth)) return; // 需 token 实例才谈得上 401

        var user = Auth!.Split(':', 2)[0];
        var (connection, _) = Connection();
        var wrongCreds = new ConnectionConfig
        {
            Id = connection.Id,
            Type = connection.Type,
            Endpoint = connection.Endpoint,
            CredentialRef = "conn:jenkins-live-wrong",
        };
        var context = new ConnectionContext
        {
            Secrets = new SecretStoreStub
            {
                Secrets =
                {
                    ["conn:jenkins-live-wrong"] = $"{user}:0000000000000000000000000000000000",
                },
            },
        };

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => new JenkinsBuildProvider()
            .GetStateAsync(Widget(JobPath), wrongCreds, context, CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health); // 真实 401 → Unauthorized
        Assert.Contains("apiToken", exception.Message); // 提示走 user:apiToken
    }
}
