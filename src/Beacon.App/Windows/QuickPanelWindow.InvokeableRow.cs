using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

public sealed partial class QuickPanelWindow
{
    /// <summary>
    /// UIA 可达行（todo 行 46 解锁面）：ListViewItem 默认只暴露 SelectionItem/ScrollItem——S4 三 pattern
    /// 实测（38325074321/38330530278）全打不开 L3。加 Invoke pattern 后 CI/辅助技术与 ItemClick 同链开
    /// L3 详情，人手点击行为零变化（ItemClick 处理器未动，Invoke 走同一 OpenDetailFromRow）。
    /// </summary>
    private sealed class InvokeableListItem : ListViewItem
    {
        /// <summary>UIA Invoke 请求；宿主窗接到与 ItemClick 相同的处理链。</summary>
        public event Action? InvokeRequested;

        public void RaiseInvoke()
        {
            var queue = DispatcherQueue;
            if (queue is not null && !queue.HasThreadAccess)
            {
                queue.EnqueueGuarded(() => InvokeRequested?.Invoke()); // UIA 调用可能来自非 UI 线程，转回 UI 线程
            }
            else
            {
                InvokeRequested?.Invoke();
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new InvokeableListItemPeer(this);
    }

    /// <summary>Invoke pattern 只增不改：其余 pattern 走 ListViewItem 基线（SelectionItem 等保持默认语义）。</summary>
    private sealed class InvokeableListItemPeer : ListViewItemAutomationPeer, IInvokeProvider
    {
        public InvokeableListItemPeer(InvokeableListItem owner)
            : base(owner)
        {
        }

        protected override object? GetPatternCore(PatternInterface patternInterface)
            => patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

        void IInvokeProvider.Invoke() => ((InvokeableListItem)Owner).RaiseInvoke();
    }
}
