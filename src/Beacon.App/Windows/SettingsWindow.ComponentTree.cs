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
    /// <summary>
    /// 组件树 JSON 编辑器（Phase 5 行 4 第一片，契约.md §3.2 声明式树的编辑面）：
    /// 面键 → 树根；组件行「组件树」载入，保存走 ComponentTreeValidator 前置校验
    /// （非法整次拒绝不落盘）+ UpsertWidget 写入面二次防呆（016ba97）双闸。
    /// 清空保存 = Components 置 null 回传统渲染（无树无错）。Components 是 init-only
    /// （契约「实例与配置只读共享」），更新走克隆整体替换（UpsertWidget 按 Id 替换）。
    /// </summary>
    private UIElement BuildComponentTreeEditor()
    {
        _componentEditHeader = new TextBlock
        {
            Text = "组件实例面（点组件行「组件树」载入）",
            FontSize = DesignTokens.FontSubtitle,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        // —— 尺寸档（Phase 5 行 4）：组件级 tile 高度档，词汇沿 UiDensity 三档（TileSizing 单源）——
        _sizeTierBox = new ComboBox { Header = "尺寸档（桌面 tile 高度）", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_sizeTierBox, "instance-size-tier-box"); // UIA 取证：尺寸档锚点
        foreach (var (label, value) in new[] { ("紧凑", "compact"), ("标准", "regular"), ("宽松", "spacious") })
        {
            _sizeTierBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        _sizeTierBox.SelectionChanged += (_, _) => SaveSizeTier();

        // —— 桌面布局区（Phase 5 行 4）：单宿主共享布局（锚点/偏移/收起/显示器），
        //     写入口径同 PinnedHostWindow.PersistLayout：widgets.json 便利引用 + pins.json 权威 ——
        _layoutMonitorBox = new ComboBox { Header = "显示器", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_layoutMonitorBox, "instance-layout-monitor-box");
        _layoutAnchorBox = new ComboBox { Header = "锚点角", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_layoutAnchorBox, "instance-layout-anchor-box");
        foreach (var (label, value) in new[] { ("左上", "TopLeft"), ("右上", "TopRight"), ("左下", "BottomLeft"), ("右下", "BottomRight") })
        {
            _layoutAnchorBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        var offsetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _layoutOffsetXBox = new TextBox { Header = "偏移 X（DIP）", Width = 110 };
        _layoutOffsetYBox = new TextBox { Header = "偏移 Y（DIP）", Width = 110 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_layoutOffsetXBox, "instance-layout-offset-x");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_layoutOffsetYBox, "instance-layout-offset-y");
        offsetRow.Children.Add(_layoutOffsetXBox);
        offsetRow.Children.Add(_layoutOffsetYBox);
        _layoutCollapsedToggle = new ToggleSwitch { Header = "吸边收起", OnContent = "收起", OffContent = "展开" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_layoutCollapsedToggle, "instance-layout-collapsed-toggle");
        var saveLayout = new Button { Content = "保存桌面布局" };
        saveLayout.Click += (_, _) => SaveDesktopLayout();

        // —— 可视化树编辑器（Phase 5 行 4 第三片）：TreeView 结构化编辑——树操作回写下方 JSON 框，
        //     保存仍走「保存组件树」单一校验落盘链（前置 ComponentTreeValidator + UpsertWidget 二次防呆）——
        _componentTree = new TreeView { Height = 150 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_componentTree, "instance-component-tree"); // UIA 取证：树面锚点
        _componentTree.SelectionChanged += (_, _) => LoadNodeEditor();

        var nodeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _nodeTypeBox = new ComboBox { Header = "类型", Width = 150 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_nodeTypeBox, "instance-node-type-box");
        foreach (var type in ComponentSurfaceRenderer.KnownTypes) // 类型词汇=渲染注册表单源（渲染侧已知什么才允许声明什么）
        {
            _nodeTypeBox.Items.Add(new ComboBoxItem { Content = type, Tag = type });
        }
        _nodeIdBox = new TextBox { Header = "Id（绑定目标定位；空=匿名）", Width = 170 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_nodeIdBox, "instance-node-id-box");
        nodeRow.Children.Add(_nodeTypeBox);
        nodeRow.Children.Add(_nodeIdBox);
        _nodePropsBox = new TextBox
        {
            Header = "Props JSON（键值均字符串；{}=空）",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 60,
            FontFamily = new FontFamily("Consolas"),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_nodePropsBox, "instance-node-props-box");
        var nodeButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var applyNode = new Button { Content = "应用节点修改" };
        applyNode.Click += (_, _) => ApplyNodeEdit();
        var addChild = new Button { Content = "添加子节点" };
        addChild.Click += (_, _) => AddChildNode();
        var deleteNode = new Button { Content = "删除选中" };
        deleteNode.Click += (_, _) => DeleteSelectedNode();
        var refreshTree = new Button { Content = "从 JSON 刷新树" }; // 手改 JSON 框后把树投影拉回（JSON 是落盘真源）
        refreshTree.Click += (_, _) => RefreshTreeFromJson();
        var surfaceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var surfaceBox = new ComboBox { Header = "面", Width = 110 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(surfaceBox, "instance-surface-box");
        foreach (var surface in new[] { "tile", "row", "detail" }) // 三面词汇=契约 §3.2 面键（Resolve 调用面实证）
        {
            surfaceBox.Items.Add(new ComboBoxItem { Content = surface, Tag = surface });
        }
        surfaceBox.SelectedIndex = 0;
        var addSurface = new Button { Content = "添加面" };
        addSurface.Click += (_, _) => AddSurface(TagOf(surfaceBox) ?? "tile");
        nodeButtons.Children.Add(applyNode);
        nodeButtons.Children.Add(addChild);
        nodeButtons.Children.Add(deleteNode);
        nodeButtons.Children.Add(refreshTree);
        surfaceRow.Children.Add(surfaceBox);
        surfaceRow.Children.Add(addSurface);
        _nodeFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_nodeFeedback, "instance-node-feedback");

        _componentJsonBox = new TextBox
        {
            Header = "组件树 JSON（surface 键 → 树根；留空保存=清空树回传统渲染；树编辑会同步本框）",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            FontFamily = new FontFamily("Consolas"),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_componentJsonBox, "component-json-box"); // UIA 取证：JSON 面可断言
        _componentFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_componentFeedback, "component-feedback");
        var save = new Button { Content = "保存组件树" };
        save.Click += (_, _) => SaveComponentTree();

        var layoutForm = new StackPanel { Spacing = 6 };
        layoutForm.Children.Add(new TextBlock { Text = "桌面布局（全部钉选组件共享；写入 widgets.json + pins.json）", FontSize = DesignTokens.FontCaption, Opacity = 0.75 });
        layoutForm.Children.Add(_layoutMonitorBox);
        layoutForm.Children.Add(_layoutAnchorBox);
        layoutForm.Children.Add(offsetRow);
        layoutForm.Children.Add(_layoutCollapsedToggle);
        layoutForm.Children.Add(saveLayout);

        var treeForm = new StackPanel { Spacing = 6 };
        treeForm.Children.Add(new TextBlock { Text = "组件树（选中节点编辑；操作同步下方 JSON 框）", FontSize = DesignTokens.FontCaption, Opacity = 0.75 });
        treeForm.Children.Add(_componentTree);
        treeForm.Children.Add(nodeRow);
        treeForm.Children.Add(_nodePropsBox);
        treeForm.Children.Add(nodeButtons);
        treeForm.Children.Add(surfaceRow);
        treeForm.Children.Add(_nodeFeedback);

        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(_componentEditHeader);
        form.Children.Add(_sizeTierBox);
        form.Children.Add(new Border { BorderBrush = new SolidColorBrush(SeverityPalette.Rgb(60, 62, 70, 74)), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(0, 8, 0, 8), Child = layoutForm });
        form.Children.Add(new Border { BorderBrush = new SolidColorBrush(SeverityPalette.Rgb(60, 62, 70, 74)), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(0, 8, 0, 8), Child = treeForm });
        form.Children.Add(_componentJsonBox);
        form.Children.Add(save);
        form.Children.Add(_componentFeedback);
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = DesignTokens.RadiusCard,
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
    }

    private void OpenComponentTreeEditor(WidgetConfig widget)
    {
        if (_componentJsonBox is null)
        {
            return; // 非组件页（常规/外观/高级）无编辑器，理论不可达（行钮只在组件页），空守卫防 NRE
        }
        _componentEditTarget = widget;
        _componentEditHeader.Text = $"组件实例面 · {widget.Id}";
        _componentJsonBox.Text = widget.Components is { Count: > 0 } components
            ? BeaconJson.Serialize(components, indented: true)
            : "";
        _componentFeedback.Text = "";
        _instanceLoading = true; // 载入回填不触发保存链（SelectionChanged/文本回填幂等但会空转重建）
        try
        {
            _sizeTierBox.SelectedIndex = Math.Max(0, IndexOfTag(_sizeTierBox, widget.Config.GetValueOrDefault(TileSizing.SizeTierKey) ?? "regular"));
            var layout = CurrentSharedLayout();
            _layoutMonitorBox.Items.Clear();
            foreach (var monitor in MonitorService.GetAll())
            {
                _layoutMonitorBox.Items.Add(new ComboBoxItem { Content = monitor.DeviceName, Tag = PinnedHostWindow.Identity(monitor) });
            }
            if (_layoutMonitorBox.Items.OfType<ComboBoxItem>().All(i => i.Tag?.ToString() != layout.Monitor))
            {
                _layoutMonitorBox.Items.Add(new ComboBoxItem { Content = $"{layout.Monitor}（已断开）", Tag = layout.Monitor }); // 显示器拔了不丢已存标识
            }
            _layoutMonitorBox.SelectedIndex = Math.Max(0, IndexOfTag(_layoutMonitorBox, layout.Monitor));
            _layoutAnchorBox.SelectedIndex = Math.Max(0, IndexOfTag(_layoutAnchorBox, layout.Anchor.ToString()));
            _layoutOffsetXBox.Text = layout.OffsetDips.X.ToString(CultureInfo.InvariantCulture);
            _layoutOffsetYBox.Text = layout.OffsetDips.Y.ToString(CultureInfo.InvariantCulture);
            _layoutCollapsedToggle.IsOn = layout.Collapsed;
        }
        finally
        {
            _instanceLoading = false;
        }
        // 树编辑器工作副本：与 JSON 框同源（载入时深拷贝一份，零两份真源——树操作只改副本再回写 JSON 框）
        _editSurfaces = widget.Components is { Count: > 0 } loaded
            ? BeaconJson.Deserialize<Dictionary<string, ComponentDescriptor>>(BeaconJson.Serialize(loaded, indented: true)) ?? []
            : [];
        RebuildComponentTree();
    }

    /// <summary>树节点定位（重建后即失效——每次操作后整树重建，位置恒新鲜）：面根（SurfaceKey）或父 Children 列表位。</summary>
    private sealed class TreeItem(ComponentDescriptor node, string? surfaceKey, List<ComponentDescriptor>? siblings, int index)
    {
        public ComponentDescriptor Node { get; } = node;
        public string? SurfaceKey { get; } = surfaceKey;
        public List<ComponentDescriptor>? Siblings { get; } = siblings;
        public int Index { get; } = index;
        public override string ToString() => SurfaceKey is not null
            ? $"▍{SurfaceKey} · {Node.Type}"
            : $"  {Node.Type}{(string.IsNullOrEmpty(Node.Id) ? "" : $" #{Node.Id}")}{(Node.Children.Count > 0 ? $" ({Node.Children.Count})" : "")}";
    }

    /// <summary>整树重建（工作副本 → TreeView；面根一层 + 递归子节点）。</summary>
    private void RebuildComponentTree()
    {
        _componentTree.RootNodes.Clear();
        foreach (var (surface, root) in _editSurfaces)
        {
            var surfaceNode = new TreeViewNode { Content = new TreeItem(root, surface, null, -1), IsExpanded = true };
            _componentTree.RootNodes.Add(surfaceNode);
            AppendChildNodes(surfaceNode, root);
        }
    }

    private static void AppendChildNodes(TreeViewNode parent, ComponentDescriptor node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var childNode = new TreeViewNode { Content = new TreeItem(child, null, node.Children, i) };
            parent.Children.Add(childNode);
            AppendChildNodes(childNode, child);
        }
    }

    /// <summary>选中节点 → 节点编辑器（未知类型临时插首项防误改——不静默替用户改类型）。</summary>
    private void LoadNodeEditor()
    {
        if (_componentTree.SelectedNodes.FirstOrDefault() is not { Content: TreeItem item })
        {
            return; // 无选中：编辑器保持上次内容（有「先选中」提示兜底）
        }
        var typeIndex = IndexOfTag(_nodeTypeBox, item.Node.Type);
        if (typeIndex < 0)
        {
            _nodeTypeBox.Items.Insert(0, new ComboBoxItem { Content = $"{item.Node.Type}（未注册）", Tag = item.Node.Type });
            typeIndex = 0;
        }
        _nodeTypeBox.SelectedIndex = typeIndex;
        _nodeIdBox.Text = item.Node.Id;
        _nodePropsBox.Text = item.Node.Props.Count == 0 ? "{}" : BeaconJson.Serialize(item.Node.Props, indented: true);
        _nodeFeedback.Text = "";
    }

    /// <summary>应用节点修改：Type/Id 重建节点（init-only），Props 就地换表；子树/绑定随迁。
    /// Props 解析失败不落改（fail-closed——半改状态比拒绝更伤）。</summary>
    private void ApplyNodeEdit()
    {
        if (_componentTree.SelectedNodes.FirstOrDefault() is not { Content: TreeItem item })
        {
            Feedback(_nodeFeedback, "✗ 先在树中选中一个节点。", error: true);
            return;
        }
        var type = TagOf(_nodeTypeBox);
        if (string.IsNullOrWhiteSpace(type))
        {
            Feedback(_nodeFeedback, "✗ 类型不能为空。", error: true);
            return;
        }
        Dictionary<string, string> props;
        try
        {
            var trimmed = _nodePropsBox.Text?.Trim() ?? "";
            props = trimmed is "" or "{}"
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : BeaconJson.Deserialize<Dictionary<string, string>>(trimmed)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Feedback(_nodeFeedback, $"✗ Props 解析失败（未修改）：{ex.Message}", error: true);
            return;
        }
        var updated = new ComponentDescriptor
        {
            Id = _nodeIdBox.Text?.Trim() ?? "",
            Type = type,
            Props = props,
            Children = item.Node.Children, // 子树随迁（Children 是 init 但集合可变，引用复用）
            Bindings = item.Node.Bindings, // 绑定不在可视化面编辑（JSON 框是高级通道）
        };
        ReplaceTreeItem(item, updated);
        SyncTreeToJson();
        Feedback(_nodeFeedback, "✓ 节点已修改（已同步 JSON 框；点「保存组件树」落盘）。", error: false);
    }

    private void ReplaceTreeItem(TreeItem item, ComponentDescriptor updated)
    {
        if (item.SurfaceKey is not null)
        {
            _editSurfaces[item.SurfaceKey] = updated;
        }
        else if (item.Siblings is { } list && item.Index >= 0 && item.Index < list.Count)
        {
            list[item.Index] = updated;
        }
    }

    /// <summary>添加子节点（默认 text「新节点」——注册表词汇，渲染可见）：挂在选中节点 Children 末尾。</summary>
    private void AddChildNode()
    {
        if (_componentTree.SelectedNodes.FirstOrDefault() is not { Content: TreeItem item })
        {
            Feedback(_nodeFeedback, "✗ 先选中父节点（面根或子节点）。", error: true);
            return;
        }
        item.Node.Children.Add(new ComponentDescriptor
        {
            Type = "text",
            Props = new Dictionary<string, string> { ["text"] = "新节点" },
        });
        SyncTreeToJson();
        Feedback(_nodeFeedback, "✓ 已添加子节点（text）。点「保存组件树」落盘。", error: false);
    }

    /// <summary>删除选中（面根=删整面；子节点=从父 Children 移除）。工作副本操作，落盘仍走保存链。</summary>
    private void DeleteSelectedNode()
    {
        if (_componentTree.SelectedNodes.FirstOrDefault() is not { Content: TreeItem item })
        {
            Feedback(_nodeFeedback, "✗ 先选中要删除的节点。", error: true);
            return;
        }
        if (item.SurfaceKey is not null)
        {
            _editSurfaces.Remove(item.SurfaceKey);
        }
        else if (item.Siblings is { } list && item.Index >= 0 && item.Index < list.Count)
        {
            list.RemoveAt(item.Index);
        }
        SyncTreeToJson();
        Feedback(_nodeFeedback, "✓ 已删除（已同步 JSON 框；点「保存组件树」落盘）。", error: false);
    }

    /// <summary>添加面（tile/row/detail 契约三面）：根组件默认 column（容器件，随后挂子）。</summary>
    private void AddSurface(string surface)
    {
        if (_editSurfaces.ContainsKey(surface))
        {
            Feedback(_nodeFeedback, $"✗ 面 {surface} 已存在（选中其根节点编辑）。", error: true);
            return;
        }
        _editSurfaces[surface] = new ComponentDescriptor { Type = "column" };
        SyncTreeToJson();
        Feedback(_nodeFeedback, $"✓ 面 {surface} 已添加（根 column）。点「保存组件树」落盘。", error: false);
    }

    /// <summary>JSON 框 → 工作副本 + 重建（手改 JSON 后拉回树投影；解析失败保留原树——fail-closed 不清用户工作）。</summary>
    private void RefreshTreeFromJson()
    {
        if (string.IsNullOrWhiteSpace(_componentJsonBox.Text))
        {
            _editSurfaces = [];
            RebuildComponentTree();
            return;
        }
        try
        {
            _editSurfaces = BeaconJson.Deserialize<Dictionary<string, ComponentDescriptor>>(_componentJsonBox.Text) ?? [];
            RebuildComponentTree();
            Feedback(_nodeFeedback, $"✓ 树已按 JSON 重建（{_editSurfaces.Count} 个面）。", error: false);
        }
        catch (Exception ex)
        {
            Feedback(_nodeFeedback, $"✗ JSON 解析失败（树未动）：{ex.Message}", error: true);
        }
    }

    /// <summary>工作副本 → JSON 框 + 整树重建（JSON 框是落盘真源，树只是结构化投影——单一落盘链不分叉）。</summary>
    private void SyncTreeToJson()
    {
        _componentJsonBox.Text = _editSurfaces.Count == 0
            ? ""
            : BeaconJson.Serialize(_editSurfaces, indented: true);
        RebuildComponentTree();
    }
}
