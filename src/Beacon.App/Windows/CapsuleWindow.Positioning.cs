using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;


public sealed partial class CapsuleWindow
{
    private double GetDpi() => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>光标屏幕物理像素（GetCursorPos；与 AppWindow.Position/Move 同一坐标系）。</summary>
    private static (int X, int Y) CursorPx()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>
    /// 宽随内容实测（RFC §6.2.2：常态 48–120 DIP；2026-10-29 UI 升级环+灯并置后极值文案
    /// 「1 ok · 1 err」实测放宽到 156，悬停预览/离线横幅同上限），高最小 32 DIP；
    /// AppWindow.Resize/Size 一律物理像素，XAML 布局是 DIP——统一 ×DPI，不乘会在高缩放下被裁成一条。
    /// </summary>
    private (int Width, int Height) MeasureSizePx()
    {
        Root.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = Root.DesiredSize;
        var dpi = GetDpi();
        if (_island)
        {
            // 岛（BrimDeck Notch 口径）：宽 = max(NotchWidthDefault 238, 收起行 + 2×NotchInset 30)，
            // 下限 NotchWidthMin 72、上限屏宽 - 16 DIP；展开不改宽（BrimDeck 面板宽随岛宽，只向下长），
            // 高 = max(BaseHeight 32, 内容实测)——收起恒 32，悬停预览把岛向下拉长。
            var rowDips = PillArea.DesiredSize.Width + CapsuleIslandMath.InsetDips * 2;
            var widthDips = Math.Clamp(Math.Ceiling(Math.Max(CapsuleIslandMath.DefaultWidthDips, rowDips)),
                CapsuleIslandMath.MinWidthDips, IslandMaxWidthDips(dpi));
            _islandWidthDips = widthDips;
            var heightDips = Math.Max(CapsuleIslandMath.HeightDips, Math.Ceiling(desired.Height));
            if (SourcePreview.Visibility != Visibility.Visible && OfflineBanner.Visibility != Visibility.Visible)
            {
                // 收起态：岛=贴顶平齐刘海（Dynamic Island 口径），窗高=曲面高 32 恒——无底部阴影带。
                // ① 刘海是贴顶的平齐黑药丸，不是带落影的浮块：底部阴影带把窗口读成矩形板
                //    （真机截图 slab：面下 8 DIP 全宽暗带 + 桌面剪影随高度变宽的实证）。
                // ② 确定性高度还消除「展开→收起后窗高卡在 69 而非 40」的实测残留（真机 trace
                //    root=238x69：刚收面的那一帧 Root.DesiredSize 仍是展开态残留，实测值不可信）。
                // 收起内容恒 ≤ 曲面高（PillArea.MinHeight=32 行容器内环/条/徽标都 ≤32），不会裁切；
                // 离线横幅在面下独立成块时随内容（横幅可见走上方实测分支）。
                heightDips = CapsuleIslandMath.HeightDips;
            }
            return (DpiLayoutMath.ToPx(widthDips, dpi), DpiLayoutMath.ToPx(heightDips, dpi));
        }

        // 经典胶囊：观感口径不动（48–156 DIP 宽随内容、最小 32 高）
        var classicWidth = Math.Clamp(Math.Ceiling(desired.Width), 48, 156);
        var classicHeight = Math.Max(32, Math.Ceiling(desired.Height));
        return (DpiLayoutMath.ToPx(classicWidth, dpi), DpiLayoutMath.ToPx(classicHeight, dpi));
    }

    /// <summary>岛宽上限 = 所在屏物理宽 - 16 DIP 边距（BrimDeck WidthRange 用屏宽收窄的同口径）；
    /// 取不到显示器时回退 1024 DIP（Place 的夹取仍会把窗放回屏内）。</summary>
    private double IslandMaxWidthDips(double dpi)
    {
        var monitor = _positioned
            ? MonitorService.FromPixel(_x + DpiLayoutMath.ToPx((int)_islandWidthDips, dpi) / 2, _y + 1)
            : MonitorService.Primary();
        var screen = monitor?.MonitorPx;
        if (screen is not { } rect || rect.Right - rect.Left <= 0)
        {
            return 1024;
        }

        var marginPx = DpiLayoutMath.ToPx(16, dpi);
        return Math.Max(CapsuleIslandMath.MinWidthDips, (rect.Right - rect.Left - marginPx) / dpi);
    }

    /// <summary>外框物理尺寸 = 客户区需求（内容实测）+ 非客户区差值。AppWindow.Resize 收的是外框，
    /// 直接喂客户区需求 → 底部 ~6px 不可见（无边框 presenter 仍保留 resize 边框），内容裁切
    /// （2026-10-14 进度条缺陷单同型根因）。_sizePx 与 RestorePosition/SavePosition 的锚点数学
    /// 同为外框口径，save/restore 往返不漂移。</summary>
    private (int Width, int Height) FrameSizePx()
    {
        var client = MeasureSizePx();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        return (client.Width + chromeWidth, client.Height + chromeHeight);
    }

    private void ApplySize()
    {
        if (_appWindow is null)
        {
            return;
        }
        _sizePx = FrameSizePx();
        // 岛：预览区钉死在曲面内容宽内（SurfaceStack 已两侧各内缩 30），行文本只在岛内裁尾不越缘；
        // 经典：恢复无上限（旧口径）。MaxWidth 只影响预览行布局，不影响岛宽（宽只量收起行）。
        SourcePreview.MaxWidth = _island
            ? Math.Max(0, _islandWidthDips - CapsuleIslandMath.InsetDips * 2)
            : double.PositiveInfinity;
        if (_sizePx.Width == _appWindow.Size.Width && _sizePx.Height == _appWindow.Size.Height)
        {
            return;
        }
        _appWindow.Resize(new SizeInt32(_sizePx.Width, _sizePx.Height));
        if (_positioned)
        {
            RestorePosition(); // 宽随内容变化后按锚点角重新贴位
        }
    }

    private void RestorePosition()
    {
        if (_island)
        {
            RestoreIslandPosition(); // 岛=顶中锚点独立持久化（与经典四角布局互不覆盖）
            return;
        }
        var layout = _shellState.LoadCapsuleLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var work = monitor?.WorkPx ?? _defaultWorkAreaFallback;
        var dpi = GetDpi();
        // 用实测尺寸而非 _appWindow.Size：Resize 刚发出时 Size 可能还是旧值，会把定位算偏
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();

        if (layout is null)
        {
            // 默认：主屏工作区右下角（任务书 Level 1）
            _x = work.Right - widthPx - DpiLayoutMath.ToPx(24, dpi);
            _y = work.Bottom - heightPx - DpiLayoutMath.ToPx(96, dpi);
        }
        else
        {
            var offsetX = (int)Math.Round(layout.OffsetDips.X * dpi);
            var offsetY = (int)Math.Round(layout.OffsetDips.Y * dpi);
            _x = layout.Anchor is PinAnchor.TopLeft or PinAnchor.BottomLeft
                ? work.Left + offsetX
                : work.Right - widthPx - offsetX;
            _y = layout.Anchor is PinAnchor.TopLeft or PinAnchor.TopRight
                ? work.Top + offsetY
                : work.Bottom - heightPx - offsetY;
        }

        // 夹回工作区：锚点+偏移在换屏/改缩放后可能把窗口推出屏幕（与 PinnedHost 的 ClampInto 同理）
        _x = Math.Clamp(_x, work.Left, Math.Max(work.Left, work.Right - widthPx));
        _y = Math.Clamp(_y, work.Top, Math.Max(work.Top, work.Bottom - heightPx));

        _appWindow.Move(new PointInt32(_x, _y));
        _positioned = true;
    }

    /// <summary>岛形态恢复：显示器 + 顶中锚偏移（CapsuleIslandMath 纯函数，Core.Tests 锁口径）。
    /// 入参是**屏幕矩形**（非工作区——刘海越过任务栏贴屏幕顶，BrimDeck Notch surface.Y=0 同口径）；
    /// 布局缺失用主屏顶中默认位（DefaultOffset = 居中、Y 恒 0）。
    /// 外框顶 ≠ 可见曲面顶：无边框窗仍可能带隐藏非客户区，按实测「客户区顶-外框顶」补偿，
    /// 保证肉眼看到的曲面顶真的压在屏幕顶（真机三态证据里量的就是这条）。</summary>
    private void RestoreIslandPosition()
    {
        var layout = _shellState.LoadIslandLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var screen = monitor is { } found ? found.MonitorPx : _defaultWorkAreaFallback;
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var rect = CapsuleIslandMath.Place(
            ToPinRect(screen), layout?.OffsetDips ?? CapsuleIslandMath.DefaultOffset, GetDpi(), widthPx, heightPx);
        _x = rect.X;
        _y = rect.Y - ClientTopOffsetPx();
        _appWindow.Move(new PointInt32(_x, _y));
        _positioned = true;
    }

    /// <summary>可见客户区顶相对窗口外框顶的物理偏移（≥0）：曲面顶贴屏顶时要把窗口上提这一截。</summary>
    private int ClientTopOffsetPx()
    {
        if (!NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return 0;
        }

        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        return NativeMethods.ClientToScreen(_hwnd, out origin) ? Math.Max(0, origin.Y - window.Top) : 0;
    }

    /// <summary>Win32 RECT（Left/Top/Right/Bottom 物理像素）→ Core 布局矩形（X/Y/Width/Height）：
    /// CapsuleIslandMath 是纯 Core 域（PinLayoutMath 同口径不依赖 Win32），边界换算留在窗体侧。</summary>
    private static PinRect ToPinRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private void SavePosition()
    {
        if (_island)
        {
            SaveIslandPosition();
            return;
        }
        var dpi = GetDpi();
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var monitor = MonitorService.FromPixel(_x + widthPx / 2, _y + heightPx / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        var work = monitor.WorkPx;

        var anchorLeft = _x + widthPx / 2 < (work.Left + work.Right) / 2;
        var anchorTop = _y + heightPx / 2 < (work.Top + work.Bottom) / 2;
        var anchor = anchorLeft
            ? (anchorTop ? PinAnchor.TopLeft : PinAnchor.BottomLeft)
            : (anchorTop ? PinAnchor.TopRight : PinAnchor.BottomRight);

        var offsetX = Math.Max(0, (anchorLeft ? _x - work.Left : work.Right - _x - widthPx) / dpi);
        var offsetY = Math.Max(0, (anchorTop ? _y - work.Top : work.Bottom - _y - heightPx) / dpi);

        _shellState.SaveCapsuleLayout(new PinLayout
        {
            Monitor = monitor.DeviceName,
            Anchor = anchor,
            OffsetDips = new PinOffset(offsetX, offsetY),
            Collapsed = false,
        });
    }

    /// <summary>岛形态拖动落点持久化：显示器 + 顶中锚偏移（Locate 互逆换算），与经典胶囊布局分存互不覆盖。</summary>
    private void SaveIslandPosition()
    {
        var dpi = GetDpi();
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var monitor = MonitorService.FromPixel(_x + widthPx / 2, _y + heightPx / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        _shellState.SaveIslandLayout(new CapsuleIslandLayout
        {
            Monitor = monitor.DeviceName,
            // 屏幕矩形（非工作区）：贴顶岛的锚在屏顶，工作区会因任务栏把 X 居中算偏
            OffsetDips = CapsuleIslandMath.Locate(
                ToPinRect(monitor.MonitorPx), new PinRect(_x, _y, widthPx, heightPx), dpi),
        });
    }
}
