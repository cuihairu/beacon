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

// ---- 记录水合与落盘钩子（notifications.json，RFC §8 跨重启） ----

public sealed class NotificationEnginePersistenceTests
{
    private sealed class NoopSink : INotificationSink
    {
        public void Show(NotificationRecord notification, NotificationDelivery delivery) { }
    }

    private static NotificationRecord Record(string id, Severity severity = Severity.Error, bool read = false) => new()
    {
        Id = id,
        SourceWidgetId = "w-ci",
        WidgetType = "github.actions.runs",
        Severity = severity,
        Title = "ci.yml · failure",
        Timestamp = DateTimeOffset.UtcNow,
        Read = read,
    };

    [Fact]
    public void Hydrate_RestoresRecords_AndCapsAtRollingLimit()
    {
        using var engine = new NotificationEngine(new EventBus(), new NoopSink(), new FakeClock());
        var records = Enumerable.Range(1, NotificationEngine.MaxRecords + 40)
            .Select(i => Record($"r-{i}"))
            .ToList();

        engine.Hydrate(records);

        Assert.Equal(NotificationEngine.MaxRecords, engine.Records.Count);
        Assert.Equal("r-41", engine.Records[0].Id); // 旧→新：掐掉最老的 40 条
        Assert.Equal($"r-{NotificationEngine.MaxRecords + 40}", engine.Records[^1].Id);
    }

    [Fact]
    public void Hydrate_DoesNotRaiseRecordsChanged()
    {
        using var engine = new NotificationEngine(new EventBus(), new NoopSink(), new FakeClock());
        var raised = 0;
        engine.RecordsChanged += () => raised++;

        engine.Hydrate([Record("r-1")]);

        Assert.Equal(0, raised); // 启动水合不回写盘（读什么还写什么没有意义）
    }

    [Fact]
    public void Deliver_RaisesRecordsChanged()
    {
        var bus = new EventBus();
        // 单条显式规则（不传 provider 会走默认表——Error 状态同时命中 ci-failed 与 connection-degraded 两条）
        using var engine = new NotificationEngine(
            bus, new NoopSink(), new FakeClock(),
            () => [new NotificationRule { Id = "r-1", SeverityAtLeast = Severity.Warning }]);
        var raised = 0;
        engine.RecordsChanged += () => raised++;

        bus.Publish(new WidgetStateChanged(new WidgetState
        {
            WidgetId = "w-ci",
            WidgetType = "github.actions.runs",
            ConnectionId = "gh",
            Severity = Severity.Error,
            Summary = "failed",
        }));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MarkRead_RaisesRecordsChanged()
    {
        var bus = new EventBus();
        using var engine = new NotificationEngine(bus, new NoopSink(), new FakeClock());
        engine.Hydrate([Record("r-1"), Record("r-2", read: false)]);
        var raised = 0;
        engine.RecordsChanged += () => raised++;

        engine.MarkRead("r-1");
        engine.MarkAllRead();

        Assert.Equal(2, raised); // 已读态随盘：单条与全读各触发一次
        Assert.Equal(0, engine.UnreadCount);
    }
}
