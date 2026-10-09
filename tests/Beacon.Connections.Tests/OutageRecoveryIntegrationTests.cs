using System.Net;
using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;

namespace Beacon.Connections.Tests;

/// <summary>
/// B-207 断网/恢复验收（socket 级，2026-10-10 补强）：真 HttpStatusProvider + 真 HttpClient + 真 WidgetHost
/// + 真 JsonConfigurationStore/JsonCacheStore，本地 HttpListener 停机/重启模拟断网/恢复。
/// 比 FakeHttpHandler 单测更强：走真实连接拒绝路径（HttpRequestException → Offline）与真实缓存落盘/水合。
/// 覆盖：断网 → Offline 健康 + 陈旧态重发布（失败原因直显）+ 旧缓存保留（Last update 不丢）；
/// 恢复 → 自动复位 Healthy + 新状态落盘；全程无未捕获异常（RefreshWidgetAsync 返回 false 而非抛出）。
/// </summary>
public sealed class OutageRecoveryIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "beacon-outage-" + Guid.NewGuid().ToString("N"));

    private sealed class StubServer : IDisposable
    {
        private HttpListener? _listener;
        private int? _port; // 一经分配不再变：停机/重启模拟断网/恢复，地址必须相同

        public string Url { get; private set; } = "";

        public int Start()
        {
            _listener = new HttpListener();
            if (_port is null)
            {
                _port = FreePort();
                Url = $"http://127.0.0.1:{_port}/status";
            }
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(() => ServeAsync(_listener));
            return _port.Value;
        }

        public void Stop()
        {
            try { _listener?.Stop(); } catch { /* 已停 */ }
            try { _listener?.Close(); } catch { /* 已关 */ }
            _listener = null; // 端口保留在 _port，重启仍绑同一地址
        }

        private static async Task ServeAsync(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch
                {
                    return; // 停机即退出循环
                }
                var payload = Encoding.UTF8.GetBytes("""{"status":"success","message":"打包完成","html_url":"https://build.local/1"}""");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload);
                context.Response.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose() => Stop();
    }

    private sealed class InMemorySecrets : ISecretStore
    {
        public Task<string?> GetAsync(string credentialRef) => Task.FromResult<string?>(null);
        public Task SetAsync(string credentialRef, string secret) => Task.CompletedTask;
        public Task DeleteAsync(string credentialRef) => Task.CompletedTask;
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception) + (exception is null ? "" : " | " + exception.Message));
    }

    private sealed class HttpResolver : IWidgetProviderResolver
    {
        private readonly IWidgetProvider _provider;
        public HttpResolver(IWidgetProvider provider) => _provider = provider;
        public IWidgetProvider? Resolve(string widgetType) => widgetType == "http.status" ? _provider : null;
    }

    private sealed class HostFixture : IDisposable
    {
        private readonly string _dir;

        public StubServer Server { get; } = new();
        public JsonConfigurationStore Config { get; }
        public JsonCacheStore Cache { get; }
        public EventBus Bus { get; } = new();
        public List<WidgetState> States { get; } = [];
        public CapturingLogger Logger { get; } = new();
        public List<ConnectionHealthChanged> Healths { get; } = [];
        public WidgetHost Host { get; }

        public HostFixture(string dir)
        {
            _dir = dir;
            Cache = new JsonCacheStore(dir);
            Config = new JsonConfigurationStore(dir);
            Config.LoadAll();
            Config.UpsertConnection(new ConnectionConfig
            {
                Id = "conn-http",
                Type = "http",
                Endpoint = "http://127.0.0.1:1/status", // 占位，测试前按真实端口改写
                CredentialRef = "http:build",
            });
            Config.UpsertWidget(new WidgetConfig
            {
                Id = "w-build",
                Type = "http.status",
                ConnectionId = "conn-http",
                Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["label"] = "打包机 A",
                    ["status_path"] = "$.status",
                    ["summary_path"] = "$.message",
                    ["url_path"] = "$.html_url",
                },
                RefreshTier = RefreshTiers.Ci,
            });
            Config.SaveConnections();
            Config.SaveWidgets();

            Bus.Subscribe<WidgetStateChanged>(e => States.Add(e.State));
            Bus.Subscribe<ConnectionHealthChanged>(Healths.Add);

            Host = new WidgetHost(
                Bus,
                SystemClock.Instance,
                new RefreshScheduler(),
                Cache,
                Config,
                new InMemorySecrets(),
                new HttpResolver(new HttpStatusProvider(new HttpClientHandler())),
                Logger);
        }

        public void PointConnectionAtServer()
        {
            var old = Config.Connections.Single();
            Config.UpsertConnection(new ConnectionConfig
            {
                Id = old.Id,
                Type = old.Type,
                Endpoint = Server.Url,
                CredentialRef = old.CredentialRef,
                Settings = old.Settings,
            });
        }

        public void Dispose()
        {
            Server.Stop();
            Host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task OutageThenRecovery_HealthAndStaleStateFollow()
    {
        using var fixture = new HostFixture(_dir);
        fixture.Server.Start();
        fixture.PointConnectionAtServer();

        // —— 在线：成功态 + 缓存落盘 + Healthy ——
        var ok = await fixture.Host.RefreshWidgetAsync("w-build");
        Assert.True(ok);
        var online = Assert.Single(fixture.States);
        Assert.Equal(Severity.Success, online.Severity);
        Assert.Contains("打包完成", online.Summary);
        Assert.False(online.IsStale);
        Assert.Equal(ConnectionHealthState.Healthy, Assert.Single(fixture.Healths).State);
        Assert.NotNull(fixture.Cache.LoadStates("conn-http")["w-build"]); // 成功态已落盘

        // —— 断网：Listener 停机 = 连接拒绝 ——
        fixture.Server.Stop();
        var failed = await fixture.Host.RefreshWidgetAsync("w-build");
        Assert.False(failed); // 不抛未捕获异常（B-207 验收点）
        Assert.Equal(ConnectionHealthState.Offline, fixture.Healths.Last().State);
        var stale = fixture.States.Last();
        Assert.True(stale.IsStale);
        Assert.False(stale.ConnectionHealthy);
        // 有缓存：旧数据原样保留（灰灯 + Last update 语义），失败原因进日志直显（不吃通用文案）
        Assert.Equal("打包机 A · 打包完成", stale.Summary);
        Assert.Equal(online.FetchedAt, stale.FetchedAt); // 同一份缓存状态，仅标志位置位
        Assert.Contains(fixture.Logger.Messages, m => m.Contains("不可达", StringComparison.Ordinal));
        // 旧缓存保留 = 「Last update」不丢（B-206/B-207 灰灯语义的本地对应）
        Assert.True(fixture.Cache.LoadStates("conn-http").TryGetValue("w-build", out _));

        // —— 恢复：Listener 重启，自动复位 ——
        fixture.Server.Start();
        var recovered = await fixture.Host.RefreshWidgetAsync("w-build");
        Assert.True(recovered, "recovery refresh failed; log:\n" + string.Join("\n", fixture.Logger.Messages));
        Assert.Equal(ConnectionHealthState.Healthy, fixture.Healths.Last().State);
        var fresh = fixture.States.Last();
        Assert.False(fresh.IsStale);
        Assert.True(fresh.ConnectionHealthy);
        Assert.Equal(Severity.Success, fresh.Severity);
        Assert.True(fresh.FetchedAt > stale.FetchedAt);
    }

    [Fact]
    public async Task OutageOnFirstFetch_LeavesNoCacheAndReportsOffline()
    {
        // 从未成功过就断网：无缓存可留，健康事件仍须发布 Offline（首错直显路径）
        using var fixture = new HostFixture(_dir);
        fixture.Server.Start(); // 先取得真实端口
        fixture.Server.Stop();  // 再停机 = 断网
        fixture.PointConnectionAtServer();

        var failed = await fixture.Host.RefreshWidgetAsync("w-build");
        Assert.False(failed);
        Assert.Equal(ConnectionHealthState.Offline, Assert.Single(fixture.Healths).State);
        var stale = Assert.Single(fixture.States);
        Assert.True(stale.IsStale);
        Assert.StartsWith("拉取失败", stale.Summary); // 无缓存：首错直显合成态
        Assert.Contains(fixture.Logger.Messages, m => m.Contains("不可达", StringComparison.Ordinal));
        Assert.False(fixture.Cache.LoadStates("conn-http").TryGetValue("w-build", out _));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 临时目录清理失败不影响断言 */ }
    }
}
