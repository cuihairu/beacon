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
    // —— 全屏避让（USER-RPT-4，BrimDeck ScreenContext 全屏规则的最小实现面）——

    /// <summary>挂前台事件钩子（out-of-context，随本线程消息泵派发，同 TopmostGuard 口径）：
    /// 避让与哨兵必须同源触发，两边都读 IsGuardVisible 才不会出现「哨兵先 Show、避让后 Hide」的闪现残留。</summary>
    private void HookFullscreenAvoidance()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            return;
        }

        _foregroundCallback = OnForegroundChanged;
        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0)
        {
            return; // 只要 OBJID_WINDOW（0）的窗级事件
        }

        EvaluateFullscreenAvoidance();
    }

    /// <summary>评估全屏避让：前台出现「无标题栏且外框盖满岛所在屏」的窗 → 岛退场；退出全屏 → 回场。
    /// BrimDeck 默认 Borderless/Exclusive → Hide，Maximized → 降级 Line（我方无 Line 形态，差异表记为已知差距，
    /// 本实现不处理最大化窗）。只对岛形态生效（经典胶囊是历史口径，行为不动）。</summary>
    private void EvaluateFullscreenAvoidance()
    {
        if (_appWindow is null)
        {
            return;
        }

        var hide = _island && _showAllowed && ForegroundCoversIslandMonitor();
        if (hide == _ruleHidden)
        {
            return;
        }

        _ruleHidden = hide;
        var fg = NativeMethods.GetForegroundWindow();
        var fgTitle = new System.Text.StringBuilder(256);
        NativeMethods.GetWindowTextW(fg, fgTitle, 256);
        NativeMethods.GetWindowRect(fg, out var fgRect);
        IslandTrace($"avoidance hide={hide} fg='{fgTitle}' fgRect={fgRect.Left},{fgRect.Top}-{fgRect.Right},{fgRect.Bottom}");
        if (!_showAllowed)
        {
            return; // 用户本就关着岛：只记状态，等 SetVisible(true) 时按 _ruleHidden 决定是否 Show
        }

        if (hide)
        {
            _appWindow.Hide();
            _edgeBand?.Hide(); // 边带随岛体避让退场（独立原生窗不跟窗隐藏）
        }
        else
        {
            _appWindow.Show();
            _edgeBand?.Show(_hwnd); // 边带随岛体回场
        }
    }

    /// <summary>前台窗是否=盖满岛所在屏的无边框全屏窗（外框含任务栏，2px 容差；
    /// 有 WS_CAPTION 的是窗口化/最大化窗，不在本避让面）。</summary>
    private bool ForegroundCoversIslandMonitor()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == _hwnd || !NativeMethods.IsWindowVisible(foreground))
        {
            return false;
        }

        var monitor = _positioned
            ? MonitorService.FromPixel(_x + _sizePx.Width / 2, _y + _sizePx.Height / 2)
            : MonitorService.Primary();
        monitor ??= MonitorService.Primary();
        if (monitor is null || !NativeMethods.GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        const int tolerance = 2;
        var screen = monitor.MonitorPx;
        var covers = rect.Left <= screen.Left + tolerance && rect.Top <= screen.Top + tolerance
                     && rect.Right >= screen.Right - tolerance && rect.Bottom >= screen.Bottom - tolerance;
        if (!covers)
        {
            return false;
        }

        // shell 桌面窗盖满屏且无标题栏，但不是全屏应用——岛本就画在桌面上方（前台翻回桌面时
        // 误判会导致岛消失：CI 43822978060 岛窗被 Hide「GLM 面不在树」+ 真机点桌面岛隐没同根因）
        var className = new System.Text.StringBuilder(64);
        NativeMethods.GetClassNameW(foreground, className, 64);
        if (className.ToString() is "Progman" or "WorkerW")
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(foreground, NativeMethods.GWL_STYLE).ToInt64();
        return (style & NativeMethods.WS_CAPTION) == 0;
    }
}
