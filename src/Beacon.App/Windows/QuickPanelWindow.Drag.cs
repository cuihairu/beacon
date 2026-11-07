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
    /// <summary>头部把手拖动会话（左键按住起手）：光标屏幕物理像素锚定，与 AppWindow.Move 同坐标系。</summary>
    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(HeaderDragArea).Properties.IsLeftButtonPressed || _appWindow is null)
        {
            return;
        }
        _dragMoved = false;
        _drag = new DragSession(HeaderDragArea, CursorPx(), _appWindow.Position.X, _appWindow.Position.Y);
        HeaderDragArea.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender) || _appWindow is null)
        {
            return;
        }
        var (cursorX, cursorY) = CursorPx();
        var dx = cursorX - _drag.StartCursor.X;
        var dy = cursorY - _drag.StartCursor.Y;
        if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
        {
            return; // 死区，区分点击与拖动
        }
        _dragMoved = true;
        var work = MonitorService.Primary()?.WorkPx ?? _fallbackWorkArea;
        var width = _appWindow.Size.Width;
        var height = _appWindow.Size.Height;
        // 夹回工作区：拖出去的看板收不回来（与 CapsuleWindow RestorePosition 同口径）
        var x = Math.Clamp(_drag.StartX + dx, work.Left, Math.Max(work.Left, work.Right - width));
        var y = Math.Clamp(_drag.StartY + dy, work.Top, Math.Max(work.Top, work.Bottom - height));
        if (x != _appWindow.Position.X || y != _appWindow.Position.Y)
        {
            _appWindow.Move(new PointInt32(x, y)); // 目标没变不重发 Move：减少 SetWindowPos 抖动
        }
        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender))
        {
            return;
        }
        _drag = null;
        HeaderDragArea.ReleasePointerCapture(e.Pointer);
        if (_dragMoved)
        {
            PersistLayout(); // 拖动落点记忆；死区内点击不落库
        }
        e.Handled = true;
    }

    /// <summary>光标屏幕物理像素（GetCursorPos；与 AppWindow.Position/Move 同一坐标系）。</summary>
    private (int X, int Y) CursorPx()
        => NativeMethods.GetCursorPos(out var point) ? (point.X, point.Y) : (_appWindow.Position.X, _appWindow.Position.Y);

    /// <summary>Win32 RECT（Left/Top/Right/Bottom 物理像素）→ Core 布局矩形（X/Y/Width/Height）：
    /// PinLayoutMath 是纯 Core 域（CapsuleWindow 同款换算），边界换算留在窗体侧。</summary>
    private static PinRect ToPinRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}
