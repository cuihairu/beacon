using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Core.Tests;

/// <summary>B-003 验收：模型往返序列化单测；枚举存字符串；无 Provider 类型进入 Core。</summary>
public class SerializationRoundTripTests
{
    [Fact]
    public void WidgetConfig_round_trips_with_pin_layout()
    {
        var widget = new WidgetConfig
        {
            Id = "w-ci-client",
            Type = "github.actions.runs",
            ConnectionId = "conn-github-main",
            Config = new Dictionary<string, string> { ["repository"] = "cuihairu/example", ["workflow"] = "build-client.yml" },
            RefreshTier = RefreshTiers.Ci,
            Pinned = true,
            PinLayout = new PinLayout
            {
                Monitor = "DEL-U2723QE-3840x2160",
                Anchor = PinAnchor.BottomRight,
                OffsetDips = new PinOffset(24, 120),
                Collapsed = false,
            },
        };

        var restored = BeaconJson.RoundTrip(widget);

        Assert.Equal(widget.Id, restored.Id);
        Assert.Equal(widget.Type, restored.Type);
        Assert.Equal(widget.ConnectionId, restored.ConnectionId);
        Assert.Equal(widget.Config["repository"], restored.Config["repository"]);
        Assert.Equal(widget.RefreshTier, restored.RefreshTier);
        Assert.True(restored.Pinned);
        Assert.NotNull(restored.PinLayout);
        Assert.Equal(PinAnchor.BottomRight, restored.PinLayout!.Anchor);
        Assert.Equal(24, restored.PinLayout.OffsetDips.X);
        Assert.Equal(120, restored.PinLayout.OffsetDips.Y);
    }

    [Fact]
    public void WidgetState_round_trips_and_stores_enum_as_string()
    {
        var state = new WidgetState
        {
            WidgetId = "w-ci-client",
            WidgetType = "github.actions.runs",
            ConnectionId = "conn-github-main",
            Severity = Severity.Error,
            Summary = "Build Client · Failed",
            DetailUrl = "https://github.com/cuihairu/example/actions/runs/1024",
            Progress = null,
            Lifecycle = LifecycleState.Failed,
            Payload = new Dictionary<string, string> { ["run_id"] = "1024", ["conclusion"] = "failure" },
            FetchedAt = new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero),
            IsStale = false,
            ConnectionHealthy = true,
        };

        var json = BeaconJson.Serialize(state, indented: false);
        Assert.Contains("\"severity\":\"error\"", json);
        Assert.Contains("\"lifecycle\":\"failed\"", json);

        var restored = BeaconJson.Deserialize<WidgetState>(json);
        Assert.Equal(state, restored); // record 值相等
        Assert.Equal("1024", restored.Payload["run_id"]);
    }

    [Fact]
    public void ConnectionConfig_round_trips_without_plaintext_secret()
    {
        var connection = new ConnectionConfig
        {
            Id = "conn-github-main",
            Type = "github",
            Endpoint = "https://api.github.com",
            CredentialRef = "beacon/github-main",
            Settings = new Dictionary<string, string> { ["owner"] = "cuihairu" },
        };

        var restored = BeaconJson.RoundTrip(connection);

        Assert.Equal(connection.Id, restored.Id);
        Assert.Equal(connection.Type, restored.Type);
        Assert.Equal(connection.Endpoint, restored.Endpoint);
        Assert.Equal(connection.CredentialRef, restored.CredentialRef);
        Assert.Equal("cuihairu", restored.Settings["owner"]);
        // RFC §9：连接配置里只允许 credentialRef，不允许 token 字段语义
        Assert.DoesNotContain("token", BeaconJson.Serialize(connection).ToLowerInvariant());
    }

    [Fact]
    public void NotificationModels_round_trip()
    {
        var rule = new NotificationRule
        {
            Id = "ci-failed",
            WidgetType = "github.actions.runs",
            SeverityAtLeast = Severity.Error,
            Toast = true,
            Sound = true,
            Cooldown = TimeSpan.FromMinutes(10),
        };
        var restoredRule = BeaconJson.RoundTrip(rule);
        Assert.Equal(TimeSpan.FromMinutes(10), restoredRule.Cooldown);
        Assert.Equal(Severity.Error, restoredRule.SeverityAtLeast);

        var record = new NotificationRecord
        {
            Id = "abc",
            SourceWidgetId = "w-ci-client",
            WidgetType = "github.actions.runs",
            Severity = Severity.Critical,
            Title = "Deploy failed",
            Message = "ci-failed",
            DetailUrl = "https://example.com",
            Timestamp = new DateTimeOffset(2026, 10, 7, 3, 30, 0, TimeSpan.Zero),
            Read = false,
        };
        var restoredRecord = BeaconJson.RoundTrip(record);
        Assert.Equal(record.Id, restoredRecord.Id);
        Assert.Equal(record.SourceWidgetId, restoredRecord.SourceWidgetId);
        Assert.Equal(record.Severity, restoredRecord.Severity);
        Assert.Equal(record.Title, restoredRecord.Title);
        Assert.Equal(record.Timestamp, restoredRecord.Timestamp);
        Assert.False(restoredRecord.Read);
    }

    [Fact]
    public void ActionModels_round_trip()
    {
        var action = new ActionConfig
        {
            Id = "a-retry-client",
            Type = "gh.workflow_dispatch",
            Name = "Retry Build Client",
            RequireConfirmation = false,
            Parameters = new Dictionary<string, string>
            {
                ["repository"] = "cuihairu/example",
                ["workflow"] = "build-client.yml",
                ["operation"] = "rerun-failed",
            },
        };
        var restored = BeaconJson.RoundTrip(action);
        Assert.Equal(action.Type, restored.Type);
        Assert.False(restored.RequireConfirmation);
        Assert.Equal("rerun-failed", restored.Parameters["operation"]);

        var result = new ActionResult("a-retry-client", true, "dispatched", null, new DateTimeOffset(2026, 10, 7, 3, 35, 0, TimeSpan.Zero));
        var restoredResult = BeaconJson.RoundTrip(result);
        Assert.Equal(result, restoredResult);
    }

    [Fact]
    public void AppConfig_and_pins_round_trip()
    {
        var app = new AppConfig
        {
            Hotkey = "Ctrl+Alt+Q",
            LaunchOnStartup = false,
            Theme = "dark",
            UiOpacity = 0.85,
            ShowCapsule = true,
            NotificationRules = [new NotificationRule { Id = "r1", SeverityAtLeast = Severity.Warning, Cooldown = TimeSpan.FromMinutes(5) }],
        };
        var restoredApp = BeaconJson.RoundTrip(app);
        Assert.Equal("Ctrl+Alt+Q", restoredApp.Hotkey);
        Assert.False(restoredApp.LaunchOnStartup);
        Assert.Equal("dark", restoredApp.Theme);
        Assert.Equal(0.85, restoredApp.UiOpacity);
        Assert.Single(restoredApp.NotificationRules);
        Assert.Equal(Severity.Warning, restoredApp.NotificationRules[0].SeverityAtLeast);

        var pins = new PinsConfig
        {
            Tiles = [new PinTile
            {
                WidgetId = "w-ci-client",
                Layout = new PinLayout { Monitor = "MON-1", Anchor = PinAnchor.TopRight, OffsetDips = new PinOffset(8, 8), Collapsed = true },
            }],
        };
        var restoredPins = BeaconJson.RoundTrip(pins);
        Assert.Single(restoredPins.Tiles);
        Assert.True(restoredPins.Tiles[0].Layout.Collapsed);
    }
}
