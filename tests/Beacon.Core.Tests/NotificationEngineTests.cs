using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-502 验收：规则引擎表驱动单测（含冷却与阈值跨越去重）。</summary>
public sealed class NotificationEngineTests
{
    private sealed class RecordingSink : INotificationSink
    {
        public List<(NotificationRecord Record, NotificationDelivery Delivery)> Shown { get; } = [];

        public void Show(NotificationRecord notification, NotificationDelivery delivery)
            => Shown.Add((notification, delivery));
    }

    private sealed class Harness
    {
        public EventBus Bus { get; } = new();
        public RecordingSink Sink { get; } = new();
        public FakeClock Clock { get; } = new();
        public List<NotificationRaised> Raised { get; } = [];
        public List<NotificationRule> Rules { get; } = [];

        public NotificationEngine Create()
        {
            Bus.Subscribe<NotificationRaised>(Raised.Add);
            return new NotificationEngine(Bus, Sink, Clock, () => Rules);
        }
    }

    private static WidgetState State(
        string widgetId = "w-ci",
        string widgetType = "github.actions.runs",
        Severity severity = Severity.Error,
        string summary = "ci.yml · failure",
        bool stale = false) => new()
        {
            WidgetId = widgetId,
            WidgetType = widgetType,
            ConnectionId = "gh-main",
            Severity = severity,
            Summary = summary,
            IsStale = stale,
            FetchedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static NotificationRule Rule(
        string id = "r-1",
        string? widgetType = null,
        Severity atLeast = Severity.Warning,
        Severity? atMost = null,
        TimeSpan? cooldown = null,
        bool enabled = true,
        bool toast = true,
        bool sound = false) => new()
        {
            Id = id,
            WidgetType = widgetType,
            SeverityAtLeast = atLeast,
            SeverityAtMost = atMost,
            Cooldown = cooldown,
            Enabled = enabled,
            Toast = toast,
            Sound = sound,
        };

    public static TheoryData<Severity, bool> ThresholdCases => new()
    {
        { Severity.Success, false },
        { Severity.Info, false },
        { Severity.Warning, true },
        { Severity.Error, true },
        { Severity.Critical, true },
    };

    [Theory]
    [MemberData(nameof(ThresholdCases))]
    public void DefaultThreshold_SeverityBand_TableDriven(Severity severity, bool shouldNotify)
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule());

        harness.Bus.Publish(new WidgetStateChanged(State(severity: severity)));

        Assert.Equal(shouldNotify ? 1 : 0, harness.Sink.Shown.Count);
    }

    [Fact]
    public void SeverityAtMost_Band_WarningOnlyRule_IgnoresError()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(id: "stuck", atLeast: Severity.Warning, atMost: Severity.Warning));

        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Warning)));
        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Error)));

        Assert.Single(harness.Sink.Shown); // Warning 触发，Error 不属于频带
    }

    [Fact]
    public void Crossing_SameAlertPeriod_FiresOnce_RearmsAfterRecovery()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule()); // Cooldown = null → 跨越语义

        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Warning)));
        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Warning)));
        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Error))); // 同段内升级：仍只有一次
        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Info))); // 恢复 → 重新武装
        harness.Bus.Publish(new WidgetStateChanged(State(severity: Severity.Warning)));

        Assert.Equal(2, harness.Sink.Shown.Count);
    }

    [Fact]
    public void Cooldown_Window_SuppressesRepeat_AllowsAfterExpiry()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(cooldown: TimeSpan.FromMinutes(10)));

        harness.Bus.Publish(new WidgetStateChanged(State()));           // t0：触发
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        harness.Bus.Publish(new WidgetStateChanged(State()));           // t5m：冷却内 → 跳过
        harness.Clock.Advance(TimeSpan.FromMinutes(6));
        harness.Bus.Publish(new WidgetStateChanged(State()));           // t11m：冷却外 → 触发

        Assert.Equal(2, harness.Sink.Shown.Count);
    }

    [Fact]
    public void DisabledRule_Skips()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(enabled: false));

        harness.Bus.Publish(new WidgetStateChanged(State()));

        Assert.Empty(harness.Sink.Shown);
    }

    [Fact]
    public void WidgetTypeAndId_Filters()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(id: "pr-only", widgetType: "github.pull_requests"));

        harness.Bus.Publish(new WidgetStateChanged(State(widgetType: "github.actions.runs")));
        harness.Bus.Publish(new WidgetStateChanged(State(widgetType: "github.pull_requests", widgetId: "w-other")));

        Assert.Single(harness.Sink.Shown); // 类型过滤命中第二条
    }

    [Fact]
    public void Delivery_ComesFromRule_ToastAndSound()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(toast: true, sound: true));

        harness.Bus.Publish(new WidgetStateChanged(State()));

        var (_, delivery) = harness.Sink.Shown.Single();
        Assert.True(delivery.Toast);
        Assert.True(delivery.Sound);
    }

    [Fact]
    public void Records_RollingWindow_KeepsLatest200()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(cooldown: TimeSpan.Zero)); // 每条都触发

        for (var i = 0; i < 210; i++)
        {
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
            harness.Bus.Publish(new WidgetStateChanged(State(summary: $"n{i}")));
        }

        var records = engine.Records;
        Assert.Equal(NotificationEngine.MaxRecords, records.Count);
        Assert.Equal("n209", records[^1].Title); // 最新在尾部
        Assert.Equal("n10", records[0].Title);   // 最旧的 10 条被滚动淘汰
    }

    [Fact]
    public void MarkRead_TracksUnread()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule(cooldown: TimeSpan.Zero));

        harness.Bus.Publish(new WidgetStateChanged(State()));
        harness.Bus.Publish(new WidgetStateChanged(State()));
        Assert.Equal(2, engine.UnreadCount);

        engine.MarkRead(engine.Records[0].Id);
        Assert.Equal(1, engine.UnreadCount);

        engine.MarkAllRead();
        Assert.Equal(0, engine.UnreadCount);
    }

    [Fact]
    public void SinkException_DoesNotBlockEventPublication()
    {
        var bus = new EventBus();
        var throwingSink = new ThrowingSink();
        var raised = new List<NotificationRaised>();
        bus.Subscribe<NotificationRaised>(raised.Add);
        using var engine = new NotificationEngine(bus, throwingSink, new FakeClock(), () => [Rule()]);
        bus.Subscribe<WidgetStateChanged>(_ => { });

        bus.Publish(new WidgetStateChanged(State()));

        Assert.Single(raised); // 记录与事件照常
    }

    private sealed class ThrowingSink : INotificationSink
    {
        public void Show(NotificationRecord notification, NotificationDelivery delivery)
            => throw new InvalidOperationException("toast exploded");
    }

    [Fact]
    public void DefaultRules_ContainBuiltins()
    {
        Assert.Contains(DefaultNotificationRules.All, r => r.Id == "builtin.ci-failed" && r.SeverityAtLeast == Severity.Error);
        Assert.Contains(DefaultNotificationRules.All, r => r.Id == "builtin.ci-stuck" && r.SeverityAtMost == Severity.Warning);
        Assert.Contains(DefaultNotificationRules.All, r => r.Id == "builtin.pr-review");
    }

    [Fact]
    public void StaleState_ProducesLastUpdateMessage()
    {
        var harness = new Harness();
        using var engine = harness.Create();
        harness.Rules.Add(Rule());

        harness.Bus.Publish(new WidgetStateChanged(State(stale: true)));

        var (record, _) = harness.Sink.Shown.Single();
        Assert.Contains("Last update", record.Message);
    }
}
