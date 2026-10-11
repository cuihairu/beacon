using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// herald.incidents / herald.feed（统一订阅与投递中枢，2026-10-11 用户拍板先做这两张卡）：
/// 事件台账解析与状态词（open 未决亮黄 / critical 亮红 / 回看口径恒 Info）、RSS 解析（最新在前/pubDate）、
/// 未读基线（首刷只记基线，新增才计未读）、feed 类目 slug 校验、私密 token 从 SecretStore 取且不落 payload、
/// 连接测试（status 端点 2xx+JSON 才算通、反代 HTML 壳不冒充、401 给 API Key 指引）。
/// </summary>
public sealed class HeraldProviderTests
{
    private const string IncidentsBody = """
        {"code":0,"message":"ok","data":{"incidents":[
          {"id":"inc-1","rule_id":"r1","alert_id":"a1","title":"磁盘不足","level":"critical","opened_at":"2026-10-11T07:25:02Z","channels":["feishu"]},
          {"id":"inc-2","rule_id":"r1","alert_id":"a2","level":"warning","opened_at":"2026-10-11T06:00:00Z"}
        ],"count":2}}
        """;

    private const string RssTwoItems = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel><title>herald</title><link>https://herald.local</link><description>hub</description><lastBuildDate>Tue, 11 Oct 2026 07:25:02 +0000</lastBuildDate>
        <item><title>deploy ok</title><link>https://herald.local</link><description>pipeline green</description><guid isPermaLink="false">n-1</guid><pubDate>Tue, 11 Oct 2026 07:25:02 +0000</pubDate></item>
        <item><title>alert resolved</title><link>https://herald.local</link><description>disk back</description><guid isPermaLink="false">n-0</guid><pubDate>Tue, 11 Oct 2026 06:00:00 +0000</pubDate></item>
        </channel></rss>
        """;

    // 同源新到一条（最新在前，n-2 插最前）——未读基线第二刷用
    private const string RssThreeItems = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel><title>herald</title><link>https://herald.local</link><description>hub</description><lastBuildDate>Tue, 11 Oct 2026 08:00:00 +0000</lastBuildDate>
        <item><title>new arrival</title><link>https://herald.local</link><description>d</description><guid isPermaLink="false">n-2</guid><pubDate>Tue, 11 Oct 2026 08:00:00 +0000</pubDate></item>
        <item><title>deploy ok</title><link>https://herald.local</link><description>pipeline green</description><guid isPermaLink="false">n-1</guid><pubDate>Tue, 11 Oct 2026 07:25:02 +0000</pubDate></item>
        <item><title>alert resolved</title><link>https://herald.local</link><description>disk back</description><guid isPermaLink="false">n-0</guid><pubDate>Tue, 11 Oct 2026 06:00:00 +0000</pubDate></item>
        </channel></rss>
        """;

    private static ConnectionConfig Connection(string? endpoint = "http://127.0.0.1:8080", string? credentialRef = null) => new()
    {
        Id = "herald-main",
        Type = "herald",
        Endpoint = endpoint,
        CredentialRef = credentialRef,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig IncidentsWidget(string status = "") => new()
    {
        Id = "w-incidents",
        Type = HeraldWidgetDescriptors.IncidentsType,
        ConnectionId = "herald-main",
        Config = status.Length > 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["status"] = status }
            : [],
    };

    private static WidgetConfig FeedWidget(string category = "", string tokenRef = "", string label = "")
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (category.Length > 0)
        {
            config["feed_category"] = category;
        }
        if (tokenRef.Length > 0)
        {
            config["feed_token_ref"] = tokenRef;
        }
        if (label.Length > 0)
        {
            config["label"] = label;
        }
        return new WidgetConfig { Id = "w-feed", Type = HeraldWidgetDescriptors.FeedType, ConnectionId = "herald-main", Config = config };
    }

    private static ConnectionContext Ctx(string? apiKey = null, string? feedToken = null)
    {
        var stub = new SecretStoreStub();
        if (apiKey is not null)
        {
            stub.Secrets["conn:herald"] = apiKey;
        }
        if (feedToken is not null)
        {
            stub.Secrets["herald:feed"] = feedToken;
        }
        return new ConnectionContext { Secrets = stub };
    }

    // —— 事件台账解析（纯函数） ——

    [Fact]
    public void ParseIncidents_FieldsAndFallbacks()
    {
        var incidents = HeraldIncidentsProvider.ParseIncidents(IncidentsBody);
        Assert.Equal(2, incidents.Count);
        Assert.Equal("磁盘不足", incidents[0].Title);
        Assert.Equal("critical", incidents[0].Level);
        Assert.Equal("inc-1", incidents[0].Id);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 7, 25, 2, TimeSpan.Zero), incidents[0].OpenedAt);
        Assert.Equal("inc-2", incidents[1].Title); // 无 title 以 id 兜底（不冒充内容）
    }

    [Fact]
    public void ParseIncidents_BadShape_Throws()
    {
        var exception = Assert.Throws<ConnectionException>(() => HeraldIncidentsProvider.ParseIncidents("""{"code":0}"""));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // —— 事件卡映射（假 handler） ——

    [Fact]
    public async Task Incidents_ZeroOpen_Info()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"code":0,"data":{"incidents":[],"count":0}}""") };
        var state = await new HeraldIncidentsProvider(http).GetStateAsync(IncidentsWidget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Info, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("Herald 事件 · 无未决", state.Summary);
        Assert.Equal("0", state.Payload["count"]);
    }

    [Fact]
    public async Task Incidents_OpenWarning_CriticalError()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, IncidentsBody) };
        var state = await new HeraldIncidentsProvider(http).GetStateAsync(IncidentsWidget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Error, state!.Severity); // 未决里有 critical → 红
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal("Herald 事件 · 未决 2", state.Summary);
        Assert.Equal("1", state.Payload["critical"]);
        Assert.Equal("磁盘不足", state.Payload["latest_title"]);
        Assert.Equal("2026-10-11 07:25", state.Payload["latest_opened_at"]);

        const string noCritical = """{"code":0,"data":{"incidents":[{"id":"i","title":"t","level":"warning"}],"count":1}}""";
        var http2 = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, noCritical) };
        var warning = await new HeraldIncidentsProvider(http2).GetStateAsync(IncidentsWidget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, warning!.Severity); // 未决但无 critical → 黄
        Assert.Equal(LifecycleState.Running, warning.Lifecycle);
    }

    [Fact]
    public async Task Incidents_NonOpenFilter_Info()
    {
        const string acked = """{"code":0,"data":{"incidents":[{"id":"i","title":"t","level":"critical"}],"count":1}}""";
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, acked) };
        var state = await new HeraldIncidentsProvider(http).GetStateAsync(IncidentsWidget("acked"), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Info, state!.Severity); // 回看口径（已认领）不告警，critical 也不升色
        Assert.Equal("Herald 事件 · 已认领 1", state.Summary);
        Assert.Equal("acked", state.Payload["status"]);
    }

    [Fact]
    public async Task Incidents_InvalidFilter_FailLoud()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, IncidentsBody) };
        await Assert.ThrowsAsync<ConnectionException>(() =>
            new HeraldIncidentsProvider(http).GetStateAsync(IncidentsWidget("bogus"), Connection(), Ctx(), CancellationToken.None));
    }

    [Fact]
    public async Task Incidents_MissingEndpoint_FailLoud()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, IncidentsBody) };
        await Assert.ThrowsAsync<ConnectionException>(() =>
            new HeraldIncidentsProvider(http).GetStateAsync(IncidentsWidget(), Connection(endpoint: null), Ctx(), CancellationToken.None));
    }

    [Fact]
    public async Task Incidents_ApiKeyHeader()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, IncidentsBody) };
        await new HeraldIncidentsProvider(http).GetStateAsync(
            IncidentsWidget(), Connection(credentialRef: "conn:herald"), Ctx(apiKey: "hk-123"), CancellationToken.None);
        Assert.True(http.Requests[0].Headers.TryGetValues("X-API-Key", out var values));
        Assert.Equal("hk-123", Assert.Single(values));
    }

    // —— RSS 解析（纯函数） ——

    [Fact]
    public void ParseRss_NewestFirst_ParsesPubDate()
    {
        var items = HeraldFeedProvider.ParseRss(RssTwoItems);
        Assert.Equal(2, items.Count);
        Assert.Equal("deploy ok", items[0].Title); // Herald 两口都最新在前，首条即最新
        Assert.Equal("n-1", items[0].Id);
        Assert.Equal("pipeline green", items[0].Description);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 7, 25, 2, TimeSpan.Zero), items[0].PublishedAt);
    }

    [Fact]
    public void ParseRfc1123Z_NumericAndGmtZones()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 7, 25, 2, TimeSpan.Zero),
            HeraldFeedProvider.ParseRfc1123Z("Tue, 11 Oct 2026 07:25:02 +0000")); // Go RFC1123Z：时区无冒号
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 7, 25, 2, TimeSpan.Zero),
            HeraldFeedProvider.ParseRfc1123Z("Tue, 11 Oct 2026 07:25:02 GMT"));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 15, 25, 2, TimeSpan.Zero),
            HeraldFeedProvider.ParseRfc1123Z("Tue, 11 Oct 2026 07:25:02 -0800")); // 归一到 UTC
        Assert.Null(HeraldFeedProvider.ParseRfc1123Z("yesterday"));
        Assert.Null(HeraldFeedProvider.ParseRfc1123Z(null));
    }

    [Fact]
    public void ParseRss_GarbageAndWrongRoot_Throws()
    {
        Assert.Throws<ConnectionException>(() => HeraldFeedProvider.ParseRss("</not-xml"));
        var exception = Assert.Throws<ConnectionException>(() => HeraldFeedProvider.ParseRss("<html><body>login</body></html>"));
        Assert.Contains("channel", exception.Message); // 反代 HTML 壳按 Degraded，不冒充空源
    }

    // —— 订阅卡：未读基线 / slug 校验 / 私密 token ——

    [Fact]
    public async Task Feed_FirstRefresh_NoUnread_SecondRefresh_CountsNew()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        var provider = new HeraldFeedProvider(http);

        var first = await provider.GetStateAsync(FeedWidget("dev"), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("0", first!.Payload["unread"]); // 首刷只记基线：装完第一刷就炸一片不叫通知
        Assert.Contains("2 条", first.Summary);
        Assert.Contains("deploy ok", first.Summary);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssThreeItems); // 同实例换源：新增 n-2
        var second = await provider.GetStateAsync(FeedWidget("dev"), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("1", second!.Payload["unread"]);
        Assert.Contains("1 条新", second.Summary);
        Assert.Contains("new arrival", second.Summary); // 最新条标题跟到新来的一条
    }

    [Fact]
    public async Task Feed_EmptySource()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "<rss version=\"2.0\"><channel><title>t</title></channel></rss>") };
        var state = await new HeraldFeedProvider(http).GetStateAsync(FeedWidget("dev"), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("dev · 空源", state!.Summary);
        Assert.Equal(Severity.Info, state.Severity);
    }

    [Fact]
    public async Task Feed_LabelOverridesHead()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        var state = await new HeraldFeedProvider(http).GetStateAsync(FeedWidget("dev", label: "组内播报"), Connection(), Ctx(), CancellationToken.None);
        Assert.StartsWith("组内播报 · ", state!.Summary);
    }

    [Fact]
    public async Task Feed_InvalidCategorySlug_AndNothingConfigured_FailLoud()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        var provider = new HeraldFeedProvider(http);
        await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(FeedWidget("bad cat"), Connection(), Ctx(), CancellationToken.None));
        await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(FeedWidget(), Connection(), Ctx(), CancellationToken.None)); // 类目与令牌都没配
    }

    [Fact]
    public async Task Feed_PrivateToken_FromSecretStore_NotInPayload()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        var state = await new HeraldFeedProvider(http).GetStateAsync(
            FeedWidget(tokenRef: "herald:feed"), Connection(), Ctx(feedToken: "tok-abc123"), CancellationToken.None);
        var requested = http.Requests[0].RequestUri!.PathAndQuery;
        Assert.Contains("/feeds/private/tok-abc123.xml", requested); // token 即凭据：URL 携带但不落配置/payload
        Assert.DoesNotContain("tok-abc123", string.Concat(state!.Payload.Values));
        Assert.StartsWith("Herald 订阅 · ", state.Summary);
    }

    [Fact]
    public async Task Feed_CategoryPreferred_InvalidToken_FailLoud()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        await new HeraldFeedProvider(http).GetStateAsync(
            FeedWidget("dev", tokenRef: "herald:feed"), Connection(), Ctx(feedToken: "tok"), CancellationToken.None);
        Assert.Contains("/feeds/dev.xml", http.Requests[0].RequestUri!.PathAndQuery); // 类目优先：不发私密口

        var slugHttp = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, RssTwoItems) };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            new HeraldFeedProvider(slugHttp).GetStateAsync(
                FeedWidget(tokenRef: "herald:feed"), Connection(), Ctx(feedToken: "not a slug!"), CancellationToken.None));
        Assert.Contains("令牌", exception.Message);
    }

    // —— 投递失败卡（批3-补35 暂缓项，2026-10-11 续批落地） ——

    private const string DeliveriesBody = """
        {"code":0,"message":"ok","data":{"total":2,"offset":0,"limit":50,"logs":[
          {"id":"log-1","provider":"feishu","payload_kind":"notify","level":"error","status":"failed","error":"dial tcp timeout","created_at":"2026-10-11T08:00:00.123456789Z","duration":3000},
          {"id":"log-2","provider":"webhook","payload_kind":"notify","status":"failed","error":"","created_at":"2026-10-11T07:00:00Z"}
        ]}}
        """;

    [Fact]
    public void ParseDeliveries_FieldsAndFallbacks()
    {
        var (total, logs) = HeraldDeliveriesProvider.ParseDeliveries(DeliveriesBody);
        Assert.Equal(2, total); // total 是过滤后全窗口计数（logs 数组可能被 limit 截断）
        Assert.Equal("log-1", logs[0].Id);
        Assert.Equal("feishu", logs[0].Provider);
        Assert.Equal("dial tcp timeout", logs[0].Error);
        // Go time.Time 带纳秒（.123456789Z）：.NET tick 精度 100ns，四舍五入到 1234568 ticks
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero).AddTicks(1_234_568), logs[0].CreatedAt);
        Assert.Equal("", logs[1].Error); // 无 error 兜底空串
    }

    [Fact]
    public void ParseDeliveries_BadShape_Throws()
    {
        var exception = Assert.Throws<ConnectionException>(() => HeraldDeliveriesProvider.ParseDeliveries("""{"code":0}"""));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task Deliveries_ZeroFailed_Info()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"code":0,"data":{"total":0,"logs":[]}}""") };
        var state = await new HeraldDeliveriesProvider(http).GetStateAsync(DeliveriesWidget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Info, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("Herald 投递 · 无失败", state.Summary);
        Assert.Equal("0", state.Payload["failed"]);
    }

    [Fact]
    public async Task Deliveries_Failed_WarningPayloadCarries()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, DeliveriesBody) };
        var state = await new HeraldDeliveriesProvider(http).GetStateAsync(DeliveriesWidget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, state!.Severity); // 失败即黄：该送达的没送达，要人管
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("Herald 投递 · 失败 2 · feishu", state.Summary);
        Assert.Equal("2", state.Payload["failed"]);
        Assert.Equal("feishu", state.Payload["latest_provider"]);
        Assert.Equal("dial tcp timeout", state.Payload["latest_error"]);
        Assert.Equal("2026-10-11 08:00", state.Payload["latest_created_at"]);
    }

    [Fact]
    public async Task Deliveries_ProviderFilter_InUrl()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"code":0,"data":{"total":0,"logs":[]}}""") };
        await new HeraldDeliveriesProvider(http).GetStateAsync(
            DeliveriesWidget(provider: "fei shu"), Connection(), Ctx(), CancellationToken.None);
        var query = http.Requests[0].RequestUri!.PathAndQuery;
        Assert.EndsWith("/api/v1/logs?status=failed&limit=200&provider=fei%20shu", query); // 提供方筛选 + 转义
    }

    [Fact]
    public async Task Deliveries_MissingEndpoint_FailLoud()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, DeliveriesBody) };
        await Assert.ThrowsAsync<ConnectionException>(() =>
            new HeraldDeliveriesProvider(http).GetStateAsync(DeliveriesWidget(), Connection(endpoint: null), Ctx(), CancellationToken.None));
    }

    private static WidgetConfig DeliveriesWidget(string provider = "") => new()
    {
        Id = "w-deliveries",
        Type = HeraldWidgetDescriptors.DeliveriesType,
        ConnectionId = "herald-main",
        Config = provider.Length > 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["provider"] = provider }
            : [],
    };

    // —— 连接测试 ——

    [Fact]
    public async Task ConnectionTest_Paths()
    {
        var ok = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"code":0,"data":{"status":"running"}}""") };
        var okResult = await new HeraldConnectionProvider(ok).TestAsync(Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(ConnectionHealthState.Healthy, okResult.Health);

        var html = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "<html>login</html>") };
        var htmlResult = await new HeraldConnectionProvider(html).TestAsync(Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(ConnectionHealthState.Degraded, htmlResult.Health); // 反代 HTML 壳不冒充连通

        var missing = await new HeraldConnectionProvider().TestAsync(Connection(endpoint: null), Ctx(), CancellationToken.None);
        Assert.Equal(ConnectionHealthState.Degraded, missing.Health);

        var unauthorized = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "unauthorized") };
        var unauthorizedResult = await new HeraldConnectionProvider(unauthorized).TestAsync(Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorizedResult.Health);
    }

    [Fact]
    public async Task ConnectionTest_UsesStatusEndpoint()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}") };
        await new HeraldConnectionProvider(http).TestAsync(Connection(), Ctx(), CancellationToken.None);
        Assert.EndsWith("/api/v1/status", http.Requests[0].RequestUri!.PathAndQuery); // 恒公开口：未开 auth 也能测连通
    }

    // —— 描述符与 slug ——

    [Fact]
    public void Descriptors_CarriesFieldsAndTier()
    {
        Assert.Equal("herald.incidents", HeraldWidgetDescriptors.Incidents.Type);
        Assert.True(HeraldWidgetDescriptors.Incidents.FloatingOptIn);
        Assert.Equal(RefreshTiers.Default, HeraldWidgetDescriptors.Incidents.SuggestedTier);
        Assert.Contains(HeraldWidgetDescriptors.Incidents.Fields, field => field.Key == "status");

        Assert.Equal("herald.feed", HeraldWidgetDescriptors.Feed.Type);
        Assert.Contains(HeraldWidgetDescriptors.Feed.Fields, field => field.Key == "feed_category");
        Assert.Contains(HeraldWidgetDescriptors.Feed.Fields, field => field.Key == "feed_token_ref");

        Assert.Equal("herald.deliveries", HeraldWidgetDescriptors.Deliveries.Type);
        Assert.True(HeraldWidgetDescriptors.Deliveries.FloatingOptIn);
        Assert.Equal(RefreshTiers.Default, HeraldWidgetDescriptors.Deliveries.SuggestedTier);
        Assert.Contains(HeraldWidgetDescriptors.Deliveries.Fields, field => field.Key == "provider");
        Assert.Contains(HeraldWidgetDescriptors.All, item => item.Type == HeraldWidgetDescriptors.DeliveriesType);
    }

    [Fact]
    public void FeedSlug_Validation()
    {
        Assert.True(HeraldFeedProvider.IsFeedSlug("dev"));
        Assert.True(HeraldFeedProvider.IsFeedSlug("a.b_c-d"));
        Assert.False(HeraldFeedProvider.IsFeedSlug("bad cat"));
        Assert.False(HeraldFeedProvider.IsFeedSlug("a/b"));
        Assert.False(HeraldFeedProvider.IsFeedSlug(new string('x', 65)));
    }
}
