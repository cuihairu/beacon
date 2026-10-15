using System.Reflection;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 6：修改布局不触发无意义的数据请求。两个方向的锚定——
/// ① 结构性：组件层五个类型全是静态类（无缓存面），且任何公共方法都不接收运行时服务
///    （Provider / 执行器 / Store / 事件总线…），状态与配置只能作为数据参数传入；
/// ② 行为性：状态查取次数只由源声明决定，与布局形状无关；引擎解析期零查取。
/// 改布局 = 重新解析同一份快照，不产生新的数据请求面。
/// </summary>
public sealed class ComponentLayerInvariantTests
{
    private static readonly Type[] ComponentLayerTypes =
    [
        typeof(ComponentTreeValidator),
        typeof(ComponentSourcesValidator),
        typeof(WidgetInstanceResolver),
        typeof(ComponentBindingEngine),
        typeof(ComponentSourceIndex),
    ];

    /// <summary>任何都会产生数据请求或状态读写的运行时服务——组件层不得接收。</summary>
    private static readonly Type[] BannedDependencies =
    [
        typeof(IConnectionProvider),
        typeof(IActionExecutor),
        typeof(IStateStore),
        typeof(IConfigurationStore),
        typeof(ICacheStore),
        typeof(ISecretStore),
        typeof(IEventBus),
        typeof(IClock),
        typeof(INotificationSink),
    ];

    private static WidgetState State(string summary, string widgetId = "w-1")
        => new()
        {
            WidgetId = widgetId,
            WidgetType = "github.pr.list",
            ConnectionId = "conn-1",
            Severity = Severity.Warning,
            Summary = summary,
            FetchedAt = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
        };

    private static Dictionary<string, string> Surface(string widgetId)
        => new() { ["runs"] = widgetId };

    private static WidgetInstance Instance(ComponentDescriptor root, Dictionary<string, string>? sources = null)
        => new()
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
            Config = new Dictionary<string, string>(),
            Root = root,
            Sources = sources ?? new Dictionary<string, string>(),
        };

    private static ComponentDescriptor Chain(int levels)
    {
        var node = new ComponentDescriptor { Type = "text" };
        for (var level = 0; level < levels; level++)
        {
            node = new ComponentDescriptor { Type = "list", Children = [node] };
        }
        return node;
    }

    [Fact]
    public void ComponentLayer_IsStatelessAndTakesNoRuntimeService()
    {
        var offenders = new List<string>();
        foreach (var type in ComponentLayerTypes)
        {
            Assert.True(type.IsAbstract && type.IsSealed, $"{type.Name} 应为静态类（无缓存面）");
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var param in method.GetParameters())
                {
                    if (BannedDependencies.Contains(param.ParameterType))
                    {
                        offenders.Add($"{type.Name}.{method.Name}({param.Name}:{param.ParameterType.Name})");
                    }
                }
            }
        }

        // 一旦有人把 Provider / Store 塞进组件层，改布局就会顺带发起数据请求
        Assert.Empty(offenders);
    }

    [Fact]
    public void ResolveSources_StateLookupCountIsDeclared_NotDrivenByLayout()
    {
        var sources = Surface("w-2");
        var snapshot = new Dictionary<string, WidgetState>
        {
            ["w-1"] = State("own", "w-1"),
            ["w-2"] = State("runs", "w-2"),
        };
        var calls = new List<string>();
        Func<string, WidgetState?> stateOf = id =>
        {
            calls.Add(id);
            return snapshot.TryGetValue(id, out var state) ? state : null;
        };

        calls.Clear();
        WidgetInstanceResolver.ResolveSources(Instance(new ComponentDescriptor { Type = "surface-card" }, sources), stateOf);
        var shallowCalls = calls.Count;

        calls.Clear();
        WidgetInstanceResolver.ResolveSources(Instance(Chain(200), sources), stateOf);

        // 布局从 1 个节点到 200 层嵌套，状态查取次数不变：只由源声明决定
        Assert.Equal(2, shallowCalls);
        Assert.Equal(shallowCalls, calls.Count);
        Assert.Contains("w-1", calls);
        Assert.Contains("w-2", calls);
    }

    [Fact]
    public void ResolveBindingEngine_MakesNoStateLookups()
    {
        var sources = Surface("w-2");
        var instance = Instance(Chain(200), sources);
        var snapshot = new Dictionary<string, WidgetState>
        {
            ["w-1"] = State("own", "w-1"),
            ["w-2"] = State("runs", "w-2"),
        };
        var calls = new List<string>();
        Func<string, WidgetState?> stateOf = id =>
        {
            calls.Add(id);
            return snapshot.TryGetValue(id, out var state) ? state : null;
        };
        var map = WidgetInstanceResolver.ResolveSources(instance, stateOf);
        calls.Clear();

        ComponentBindingEngine.Resolve(instance, map);

        // 引擎拿到映射就是纯计算，不再查状态
        Assert.Equal(2, map.Count);
        Assert.Empty(calls);
    }
}
