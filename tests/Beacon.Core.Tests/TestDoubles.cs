using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

internal sealed class FakeClock(DateTimeOffset? start = null) : IClock
{
    private DateTimeOffset _now = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow
    {
        get => _now;
        set => _now = value;
    }

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>可手动推进的延迟：调度器每次等待产生一条记录，测试按需 Complete/Cancel。</summary>
internal sealed class FakeDelayer
{
    private readonly object _gate = new();
    private readonly List<FakeDelayCall> _calls = [];

    public sealed class FakeDelayCall(TimeSpan requested)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan Requested { get; } = requested;

        public bool IsCompleted => _completion.Task.IsCompleted;

        internal Task Task => _completion.Task;

        public void Complete() => _completion.TrySetResult();

        public void Cancel() => _completion.TrySetCanceled();
    }

    public IReadOnlyList<FakeDelayCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public Task Delay(TimeSpan interval, CancellationToken cancellationToken)
    {
        var call = new FakeDelayCall(interval);
        lock (_gate)
        {
            _calls.Add(call);
        }
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(static state => ((FakeDelayCall)state!).Cancel(), call);
        }
        return call.Task;
    }
}

internal static class Poll
{
    public static async Task UntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met within timeout.");
            }
            await Task.Delay(10);
        }
    }
}

internal sealed class RecordingDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _queue = new();

    public int PendingCount
    {
        get
        {
            lock (_queue)
            {
                return _queue.Count;
            }
        }
    }

    public void Post(Action action)
    {
        lock (_queue)
        {
            _queue.Enqueue(action);
        }
    }

    public void FlushAll()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
            {
                _queue.Dequeue()();
            }
        }
    }
}

internal sealed class InMemoryConfigStore : IConfigurationStore
{
    public AppConfig App { get; set; } = new();
    public List<ConnectionConfig> ConnectionItems { get; } = [];
    public List<WidgetConfig> WidgetItems { get; } = [];
    public PinsConfig PinsValue { get; set; } = new();

    public IReadOnlyList<ConnectionConfig> Connections => ConnectionItems;
    public IReadOnlyList<WidgetConfig> Widgets => WidgetItems;
    public PinsConfig Pins => PinsValue;

    public void LoadAll()
    {
    }

    public void SaveApp()
    {
    }

    public void SaveConnections()
    {
    }

    public void SaveWidgets()
    {
    }

    public void SavePins()
    {
    }

    public void UpsertConnection(ConnectionConfig connection)
    {
        var index = ConnectionItems.FindIndex(c => c.Id == connection.Id);
        if (index >= 0)
        {
            ConnectionItems[index] = connection;
        }
        else
        {
            ConnectionItems.Add(connection);
        }
    }

    public void RemoveConnection(string connectionId) => ConnectionItems.RemoveAll(c => c.Id == connectionId);

    public void UpsertWidget(WidgetConfig widget)
    {
        var index = WidgetItems.FindIndex(w => w.Id == widget.Id);
        if (index >= 0)
        {
            WidgetItems[index] = widget;
        }
        else
        {
            WidgetItems.Add(widget);
        }
    }

    public void RemoveWidget(string widgetId) => WidgetItems.RemoveAll(w => w.Id == widgetId);

    public ConnectionConfig? FindConnection(string? connectionId)
        => connectionId is null ? null : Connections.FirstOrDefault(c => c.Id == connectionId);

    public WidgetConfig? FindWidget(string widgetId) => Widgets.FirstOrDefault(w => w.Id == widgetId);
}

internal sealed class InMemoryCacheStore : ICacheStore
{
    public Dictionary<string, Dictionary<string, WidgetState>> ByConnection { get; } = [];

    public IReadOnlyDictionary<string, WidgetState> LoadStates(string connectionId)
        => ByConnection.TryGetValue(connectionId, out var states) ? states : new Dictionary<string, WidgetState>();

    public void SaveState(string connectionId, WidgetState state)
    {
        if (!ByConnection.TryGetValue(connectionId, out var states))
        {
            ByConnection[connectionId] = states = [];
        }
        states[state.WidgetId] = state;
    }
}

internal sealed class InMemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Secrets { get; } = [];

    public Task<string?> GetAsync(string credentialRef) => Task.FromResult(Secrets.GetValueOrDefault(credentialRef));

    public Task SetAsync(string credentialRef, string secret)
    {
        Secrets[credentialRef] = secret;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string credentialRef)
    {
        Secrets.Remove(credentialRef);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWidgetProvider : IWidgetProvider
{
    public required WidgetTypeDescriptor DescriptorValue { get; init; }

    public WidgetTypeDescriptor Descriptor => DescriptorValue;

    public Func<WidgetConfig, ConnectionConfig, ISecretStore, CancellationToken, Task<WidgetState>>? Handler { get; set; }

    public int CallCount;

    public Task<WidgetState> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        return Handler is not null
            ? Handler(widget, connection, context.Secrets, cancellationToken)
            : Task.FromResult(TestData.State(widget.Id, widget.Type, connection.Id));
    }
}

internal sealed class FakeResolver(IReadOnlyDictionary<string, IWidgetProvider> providers) : IWidgetProviderResolver
{
    public IWidgetProvider? Resolve(string widgetType) => providers.GetValueOrDefault(widgetType);
}

internal static class TestData
{
    public static ConnectionConfig Connection(string id = "conn-1", string type = "github")
        => new() { Id = id, Type = type, Endpoint = "https://example.invalid" };

    public static WidgetConfig Widget(
        string id = "w-1",
        string type = "test.type",
        string connectionId = "conn-1",
        string tier = RefreshTiers.Ci)
        => new() { Id = id, Type = type, ConnectionId = connectionId, RefreshTier = tier };

    public static WidgetState State(
        string widgetId = "w-1",
        string widgetType = "test.type",
        string connectionId = "conn-1",
        Severity severity = Severity.Success,
        string summary = "OK")
        => new()
        {
            WidgetId = widgetId,
            WidgetType = widgetType,
            ConnectionId = connectionId,
            Severity = severity,
            Summary = summary,
            FetchedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
