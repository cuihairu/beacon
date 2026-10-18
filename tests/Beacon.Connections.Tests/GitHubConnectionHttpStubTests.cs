using System.Net;
using System.Net.Sockets;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// Item 1（2026-10-18 用户令「改用可造数据验证」）：连接测试成功/失败两条渲染路径，
/// 用**真实本地 HttpListener 桩**走完整 HTTP 栈（TCP → HttpClient → GitHubApiClient →
/// provider → ConnectionTestResult，驱动 SettingsWindow 的 ✓/✗ 反馈分支），而不是只注入
/// HttpMessageHandler。桩在 127.0.0.1 随机端口返回 200 与 401，无需真实 PAT、无网络依赖。
/// 超时→Offline 路径已由注入式 test（TestAsync_Timeout_MapsOffline）覆盖；此处不复用 15s
/// 真实 HttpClient 超时以保持套件快速（真实栈的 2xx/4xx 状态映射已由本文件证明）。
/// </summary>
public sealed class GitHubConnectionHttpStubTests
{
    private static ConnectionConfig Connection(int port) => new()
    {
        Id = "gh-stub",
        Type = "github",
        Endpoint = $"http://127.0.0.1:{port}/",
        CredentialRef = "github:stub",
    };

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>起一个真实 HttpListener，对单次 /rate_limit 请求回指定状态码，随后释放端口。</summary>
    private static async Task ServeOnceAsync(int port, HttpStatusCode status)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = (int)status;
            var payload = System.Text.Encoding.UTF8.GetBytes("{}");
            context.Response.ContentType = "application/json";
            // 长度前缀而非 chunked：客户端以 ResponseHeadersRead 读流，
            // 无 ContentLength64 的 chunked 收尾会在 CopyToAsync 抛「copying content to a stream」。
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
            context.Response.Close();
            listener.Stop();
            listener.Close();
        });
        await Task.Yield();
    }

    private static SecretStoreStub Secrets(string token) => new()
    {
        Secrets = { [$"github:stub"] = token },
    };

    [Fact]
    public async Task TestConnection_RealHttpStack_200_RendersHealthy()
    {
        var port = FreePort();
        await ServeOnceAsync(port, HttpStatusCode.OK).ConfigureAwait(false);
        var connection = Connection(port);

        var provider = new GitHubConnectionProvider();
        var result = await provider.TestAsync(
            connection, new ConnectionContext { Secrets = Secrets("pat") }, CancellationToken.None)
            .ConfigureAwait(false);

        // 成功渲染路径：Health=Healthy → SettingsWindow 反馈「✓ 连接正常」。
        Assert.Equal(ConnectionHealthState.Healthy, result.Health);
    }

    [Fact]
    public async Task TestConnection_RealHttpStack_401_RendersUnauthorized()
    {
        var port = FreePort();
        await ServeOnceAsync(port, HttpStatusCode.Unauthorized).ConfigureAwait(false);
        var connection = Connection(port);

        var provider = new GitHubConnectionProvider();
        var result = await provider.TestAsync(
            connection, new ConnectionContext { Secrets = Secrets("bad-token") }, CancellationToken.None)
            .ConfigureAwait(false);

        // 失败渲染路径：Health=Unauthorized → SettingsWindow 反馈「✗ 认证失败，检查 token」。
        Assert.Equal(ConnectionHealthState.Unauthorized, result.Health);
        Assert.Contains("401", result.Detail);
    }
}
