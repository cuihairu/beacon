using System.Globalization;
using System.Reflection;
using System.Threading;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;

namespace Beacon.App.Windows;

internal sealed partial class SettingsWindow
{
    /// <summary>当前共享布局（读口径同 PinnedHostWindow.RestoreLayout：首个钉选组件便利引用 → pins.json 权威 → 默认）。</summary>
    private PinLayout CurrentSharedLayout()
    {
        var stored = _runtime.Config.Widgets.Where(w => w.Pinned).Select(w => w.PinLayout).FirstOrDefault(l => l is not null)
            ?? _runtime.Config.Pins.Tiles.Select(t => t.Layout).FirstOrDefault(l => l is not null);
        return stored is null
            ? new PinLayout
            {
                Monitor = MonitorService.Primary() is { } primary ? PinnedHostWindow.Identity(primary) : "",
                Anchor = PinAnchor.TopRight,
                OffsetDips = PinLayoutMath.DefaultOffset,
                Collapsed = false,
            }
            : new PinLayout { Monitor = stored.Monitor, Anchor = stored.Anchor, OffsetDips = stored.OffsetDips, Collapsed = stored.Collapsed };
    }

    /// <summary>尺寸档保存（组件级 Config["sizeTier"]，词汇沿 UiDensity 三档；同值不落盘防空转重建）。</summary>
    private void SaveSizeTier()
    {
        if (_instanceLoading)
        {
            return; // 载入回填
        }
        var widget = _componentEditTarget;
        if (widget is null)
        {
            return; // 未载入目标：下拉是死件
        }
        var tier = TagOf(_sizeTierBox) ?? "regular";
        if (string.Equals(widget.Config.GetValueOrDefault(TileSizing.SizeTierKey), tier, StringComparison.Ordinal))
        {
            return; // 同值幂等
        }
        if (tier == "regular")
        {
            widget.Config.Remove(TileSizing.SizeTierKey); // 常规=基准，不落键保持配置干净（fail-open 缺省同义）
        }
        else
        {
            widget.Config[TileSizing.SizeTierKey] = tier;
        }
        _runtime.Config.UpsertWidget(widget);
        _runtime.ReloadWidgets([widget.Id]);
        RebuildWidgets();
        PinsChanged?.Invoke(); // L0 重建取新尺寸档高
        Feedback(_componentFeedback, $"✓ 尺寸档已保存（{tier}），桌面 tile 即时重排。", error: false);
    }

    /// <summary>桌面布局保存（单宿主共享语义，写入口径同 PersistLayout：全部钉选组件便利引用 + pins.json 权威）。
    /// 偏移必须是有限数（NaN/∞ 拒绝不落盘——坏值会把宿主推出工作区且无 UI 可见反馈）。</summary>
    private void SaveDesktopLayout()
    {
        if (_componentEditTarget is null)
        {
            Feedback(_componentFeedback, "✗ 先点组件行的「组件树」载入要编辑的组件。", error: true);
            return;
        }
        if (!double.TryParse(_layoutOffsetXBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(_layoutOffsetYBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
        {
            Feedback(_componentFeedback, "✗ 偏移必须是有限数字（未保存）。", error: true);
            return;
        }
        var layout = new PinLayout
        {
            Monitor = TagOf(_layoutMonitorBox) ?? CurrentSharedLayout().Monitor,
            Anchor = Enum.TryParse<PinAnchor>(TagOf(_layoutAnchorBox), out var anchor) ? anchor : PinAnchor.TopRight,
            OffsetDips = new PinOffset(x, y),
            Collapsed = _layoutCollapsedToggle.IsOn,
        };
        var pinned = _runtime.Config.Widgets.Where(w => w.Pinned).ToList();
        var touched = new List<WidgetConfig>(pinned.Count);
        foreach (var widget in pinned)
        {
            widget.PinLayout = new PinLayout { Monitor = layout.Monitor, Anchor = layout.Anchor, OffsetDips = layout.OffsetDips, Collapsed = layout.Collapsed };
            touched.Add(widget);
        }
        // 批量落盘（审核修复 2026-11-12，与 PersistLayout 同口径）：widgets.json 只写一次。
        _runtime.Config.UpsertWidgets(touched);
        var pins = _runtime.Config.Pins;
        pins.Tiles = [.. pinned.Select(w => new Beacon.Core.Models.PinTile
        {
            WidgetId = w.Id,
            Layout = new PinLayout { Monitor = layout.Monitor, Anchor = layout.Anchor, OffsetDips = layout.OffsetDips, Collapsed = layout.Collapsed },
        })];
        _runtime.Config.SavePins();
        PinsChanged?.Invoke(); // App 侧 ReloadTiles + ReloadLayout 即时重排
        Feedback(_componentFeedback, pinned.Count == 0
            ? "✓ 桌面布局已写入 pins.json（当前无钉选组件，钉选后生效）。"
            : $"✓ 桌面布局已保存（{pinned.Count} 个钉选组件 + pins.json），宿主即时重排。", error: false);
    }

    private void SaveComponentTree()
    {
        var widget = _componentEditTarget;
        if (widget is null)
        {
            Feedback(_componentFeedback, "✗ 先点组件行的「组件树」载入要编辑的组件。", error: true);
            return;
        }

        // 清空保存 = 无声明式树（无树无错，契约 §3.2）
        if (string.IsNullOrWhiteSpace(_componentJsonBox.Text))
        {
            ReplaceWidgetComponents(widget, null);
            PinsChanged?.Invoke(); // 展示面即时回传统渲染（不落到「等下一次状态到达」）
            _editSurfaces = [];
            RebuildComponentTree(); // 树投影与落盘态一致（清空后树也清）
            Feedback(_componentFeedback, "✓ 组件树已清空——该组件回传统渲染（展示面已即时重渲）。", error: false);
            return;
        }

        Dictionary<string, ComponentDescriptor>? surfaces;
        try
        {
            surfaces = BeaconJson.Deserialize<Dictionary<string, ComponentDescriptor>>(_componentJsonBox.Text);
        }
        catch (Exception ex)
        {
            // JSON 语法错 / required 字段缺失（Type）都从这里出，未保存不落盘
            Feedback(_componentFeedback, $"✗ JSON 解析失败（未保存）：{ex.Message}", error: true);
            return;
        }
        if (surfaces is not { Count: > 0 })
        {
            ReplaceWidgetComponents(widget, null);
            PinsChanged?.Invoke();
            Feedback(_componentFeedback, "✓ 无 surface 声明——该组件回传统渲染（展示面已即时重渲）。", error: false);
            return;
        }

        // 前置校验（与存储同口径 ComponentTreeValidator）：非法整次拒绝，不让写入面默默清树
        var problems = new List<string>();
        foreach (var (surface, root) in surfaces)
        {
            var validation = ComponentTreeValidator.Validate(root);
            if (!validation.Ok)
            {
                problems.Add($"{surface}: {string.Join("; ", validation.Errors)}");
            }
        }
        if (problems.Count > 0)
        {
            Feedback(_componentFeedback, $"✗ 组件树非法（未保存）：{string.Join("；", problems)}", error: true);
            return;
        }

        ReplaceWidgetComponents(widget, surfaces);
        // 2026-10-18 外部评审 P0-3：落盘即通知 App 侧 ReloadTiles（既有窗带 _latest 状态重放，
        // 不取数、不增网络请求），保存成功即见新树——不再把生效时机推给下一次状态到达。
        PinsChanged?.Invoke();
        Feedback(_componentFeedback, $"✓ 组件树已保存（{surfaces.Count} 个 surface）；展示面已按最新状态即时重渲。", error: false);
    }

    /// <summary>克隆整体替换（Components init-only；UpsertWidget 按 Id 替换并落盘+二次防呆），
    /// 随后重建注册与首拉、刷新行区并把编辑目标切到新对象。</summary>
    private void ReplaceWidgetComponents(WidgetConfig widget, Dictionary<string, ComponentDescriptor>? surfaces)
    {
        var updated = new WidgetConfig
        {
            Id = widget.Id,
            Type = widget.Type,
            ConnectionId = widget.ConnectionId,
            Config = widget.Config,
            RefreshTier = widget.RefreshTier,
            RefreshIntervalSeconds = widget.RefreshIntervalSeconds,
            Pinned = widget.Pinned,
            ColorOverride = widget.ColorOverride,
            PinLayout = widget.PinLayout,
            Components = surfaces,
            Sources = widget.Sources,
        };
        _runtime.Config.UpsertWidget(updated);
        _runtime.ReloadWidgetRegistrations(); // 树保存属展示面（P3-02 分流）：只重排注册不取数，与增删/钉选区分
        _componentEditTarget = updated;
        RebuildWidgets();
    }

    /// <summary>组件中心「载入组件树编辑器」回调（组件中心不新增写入路径）：把生成的组件树作为
    /// tile 面推进树编辑工作副本并同步 JSON 框，仍由用户在组件页点「保存组件树」走单一校验落盘链
    /// （SaveComponentTree 前置校验 + UpsertWidget 二次防呆）。✗ 前缀=失败（组件中心按此显示红色反馈）。</summary>
    private string ApplyGalleryComponent(ComponentDescriptor root)
    {
        if (_componentEditTarget is null || _componentJsonBox is null)
        {
            return "✗ 先在组件页点某组件行的「组件树」载入要编辑的组件。";
        }
        _editSurfaces["tile"] = root;
        _componentJsonBox.Text = BeaconJson.Serialize(_editSurfaces, indented: true);
        RebuildComponentTree();
        return $"✓ 已载入 tile 面到 {_componentEditTarget.Id} 的组件树编辑器；回组件页点「保存组件树」落盘。";
    }

    /// <summary>组件行「展示样式」：定向打开组件中心——自动带入该组件最新真实状态
    /// （Host.GetState 单源快照，可空=空状态等待数据，评审 P0 断链修复），类型数据能力
    /// （DataKinds）随描述符带入供能力级筛选；已存面反解（DisplayTemplates.Identify）供样式恢复。
    /// 只捕获 widget.Id：应用/恢复时按 Id 重取最新配置（评审 P0 旧对象覆盖——中途在组件页
    /// 保存过树也不被闭包旧对象覆写）。</summary>
    private void OpenComponentStyleCenter(WidgetConfig widget)
    {
        if (_gallery is not null)
        {
            _gallery.Close(); // 单实例换目标：闭包内旧 widget 失效，重建而非复用
        }
        var latest = _runtime.Config.FindWidget(widget.Id) ?? widget;
        var state = _runtime.Host.GetState(latest.Id);
        var descriptor = AllWidgetDescriptors().FirstOrDefault(d => d.Type == latest.Type);
        var (restoreId, restoreSurface, restoreRoot) = ResolveCurrentStyle(latest);
        _gallery = new SampleGalleryWindow(
            _runtime,
            () => _gallery = null,
            applyToSettings: null,
            targetWidgetId: latest.Id,
            targetState: state,
            applyTargeted: (root, surface) => ApplyComponentDirect(latest.Id, root, surface),
            resetTargeted: () => ApplyComponentReset(latest.Id),
            restoreTemplateId: restoreId,
            restoreSurface: restoreSurface,
            restoreRootJson: restoreRoot,
            dataKinds: descriptor?.DataKinds);
        _gallery.Activate();
    }

    /// <summary>反解组件当前已存展示样式（评审 P0 样式恢复）：按 tile→row→detail 顺序找首个
    /// 已存面，经 DisplayTemplates.Identify 反解模板身份 + 面名 + 树 JSON（参数回填用）；
    /// 无树/自定义树 → (null, tile, null)——组件中心按能力筛选默认档，不假装是某模板。</summary>
    private (string? TemplateId, string Surface, string? RootJson) ResolveCurrentStyle(WidgetConfig widget)
    {
        foreach (var surface in new[] { "tile", "row", "detail" })
        {
            if (widget.Components is not null
                && widget.Components.TryGetValue(surface, out var root)
                && root is not null)
            {
                return (DisplayTemplates.Identify(root), surface, BeaconJson.Serialize(root));
            }
        }
        return (null, "tile", null);
    }

    /// <summary>组件中心定向模式「应用展示样式」回调（评审 P1）：校验通过即所选面落盘
    /// （其余既有面保留）+ PinsChanged 即时重渲（ReloadTiles 带 _latest 状态重放，不取数不加
    /// 请求），与 SaveComponentTree 同一条校验/UpsertWidget 单一落盘链，不新增写入路径。
    /// 按 Id 重取最新配置（评审 P0 旧对象覆盖）：窗打开期间组件页/另一应用面可能已改过树，
    /// 闭包旧对象会把那些改动整面覆写丢掉。</summary>
    private string ApplyComponentDirect(string widgetId, ComponentDescriptor root, string surface)
    {
        var validation = ComponentTreeValidator.Validate(root);
        if (!validation.Ok)
        {
            return $"✗ 组件树非法（未保存）：{string.Join("；", validation.Errors)}";
        }
        var target = _runtime.Config.FindWidget(widgetId);
        if (target is null)
        {
            return $"✗ 组件 {widgetId} 已不存在（未保存）。";
        }
        var surfaces = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, existing) in target.Components ?? new Dictionary<string, ComponentDescriptor>())
        {
            surfaces[key] = existing; // 保留该组件其他面（tile/row/detail 各面独立，评审 P1）
        }
        surfaces[surface] = root;
        ReplaceWidgetComponents(target, surfaces);
        PinsChanged?.Invoke();
        return $"✓ 已应用展示样式到 {target.Id} 的 {surface} 面（其余面保留，即时重渲）；高级组合仍可走「组件树」编辑器。";
    }

    /// <summary>组件中心定向模式「恢复传统渲染」回调（评审 P1 兜底）：清空该组件全部展示样式面
    /// 回传统渲染；同样按 Id 重取最新配置（与 ApplyComponentDirect 同口径）。</summary>
    private string ApplyComponentReset(string widgetId)
    {
        var target = _runtime.Config.FindWidget(widgetId);
        if (target is null)
        {
            return $"✗ 组件 {widgetId} 已不存在（未保存）。";
        }
        ReplaceWidgetComponents(target, null);
        PinsChanged?.Invoke(); // 展示面即时回传统渲染
        return $"✓ 已恢复 {target.Id} 的传统渲染（全部展示样式面已清空，即时重渲）。";
    }
}
