using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>
/// 系统托盘（RFC §10 Tray）：Win32 Shell_NotifyIcon 自实现，不引第三方托盘库。
/// 左键/双击 → 打开面板；右键 → 菜单；Exit 是唯一退出路径。
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const uint IconId = 1;

    private const int MenuOpenPanel = 1;
    private const int MenuSettings = 2;
    private const int MenuNotifications = 3;
    private const int MenuRefreshAll = 4;
    private const int MenuExit = 99;

    private readonly Win32MessageWindow _messageWindow;
    private readonly ILogger<TrayIconService> _logger;
    private readonly Dictionary<Severity, IntPtr> _severityIcons = [];
    private readonly Dictionary<(byte A, byte R, byte G, byte B), IntPtr> _runtimeIcons = [];
    private NativeMethods.NOTIFYICONDATA _nid;
    private IntPtr _icon;
    private bool _added;

    public event Action? OpenPanelRequested;
    public event Action? SettingsRequested;
    public event Action? NotificationsRequested;
    public event Action? RefreshAllRequested;
    public event Action? ExitRequested;

    public TrayIconService(Win32MessageWindow messageWindow, ILogger<TrayIconService> logger)
    {
        _messageWindow = messageWindow;
        _logger = logger;
        _messageWindow.MessageReceived += OnWindowMessage;
    }

    private static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "beacon.ico");

    /// <summary>
    /// B-706：非空时 SetSeverity 按配置色运行时绘制托盘圆点（appearance.SeverityColors 即刻生效）；
    /// 空（未注入）退回 Assets/*.ico 固定资产。
    /// </summary>
    public UiPalette? Palette { get; set; }

    public void Initialize()
    {
        _icon = NativeMethods.LoadImageW(
            IntPtr.Zero, IconPath, NativeMethods.IMAGE_ICON, 0, 0, NativeMethods.LR_LOADFROMFILE);
        if (_icon == IntPtr.Zero)
        {
            _logger.LogWarning("Tray icon not loaded from {Path}: {Error}", IconPath, Marshal.GetLastWin32Error());
        }

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _messageWindow.Hwnd,
            uID = IconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_APP_TRAYICON,
            hIcon = _icon,
            szTip = "Beacon",
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
        _added = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref _nid);
        if (!_added)
        {
            _logger.LogWarning("Shell_NotifyIcon NIM_ADD failed: {Error}", Marshal.GetLastWin32Error());
        }
    }

    /// <summary>托盘气泡（unpackaged Toast 未就绪时的兜底通道，RFC §16 风险 2）。</summary>
    public void ShowBalloon(string title, string message, uint iconFlags = NativeMethods.NIIF_INFO)
    {
        if (!_added)
        {
            return;
        }
        _nid.uFlags = NativeMethods.NIF_INFO;
        _nid.szInfoTitle = title;
        _nid.szInfo = message;
        _nid.dwInfoFlags = iconFlags;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref _nid);
        _nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
    }

    /// <summary>随总体 Severity 着色（B-503 绿/黄/红/灰；B-706 起按配置色运行时绘制）：聚合变化 ≤1 刷新周期内调用。</summary>
    public void SetSeverity(Severity severity)
    {
        if (!_added)
        {
            return;
        }
        var icon = GetSeverityIcon(severity);
        if (icon == IntPtr.Zero)
        {
            return;
        }
        _nid.uFlags = NativeMethods.NIF_ICON;
        _nid.hIcon = icon;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref _nid);
        _nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
    }

    private IntPtr GetSeverityIcon(Severity severity)
    {
        if (Palette is { } palette)
        {
            var color = palette.SeverityColor(severity);
            return DrawSeverityIcon(color);
        }
        if (_severityIcons.TryGetValue(severity, out var cached))
        {
            return cached;
        }
        var file = severity switch
        {
            Severity.Success or Severity.Info => "tray-success.ico",
            Severity.Warning => "tray-warning.ico",
            Severity.Error or Severity.Critical => "tray-error.ico",
            _ => "tray-idle.ico",
        };
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", file);
        var icon = NativeMethods.LoadImageW(IntPtr.Zero, path, NativeMethods.IMAGE_ICON, 0, 0, NativeMethods.LR_LOADFROMFILE);
        if (icon == IntPtr.Zero)
        {
            _logger.LogWarning("Severity icon not loaded from {Path}: {Error}", path, Marshal.GetLastWin32Error());
            return IntPtr.Zero;
        }
        _severityIcons[severity] = icon;
        return icon;
    }

    /// <summary>32×32 抗锯齿实心圆（托盘 16×16 缩显仍为圆点）；按颜色缓存 HICON，退出时统一销毁。</summary>
    private IntPtr DrawSeverityIcon(global::Windows.UI.Color color)
    {
        var key = (color.A, color.R, color.G, color.B);
        if (_runtimeIcons.TryGetValue(key, out var cached))
        {
            return cached;
        }
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
            graphics.FillEllipse(brush, 2, 2, 28, 28);
        }
        var icon = bitmap.GetHicon();
        _runtimeIcons[key] = icon;
        return icon;
    }

    /// <summary>更新托盘提示文本（B-503 接聚合状态）。</summary>
    public void SetTip(string tip)
    {
        if (!_added)
        {
            return;
        }
        _nid.uFlags = NativeMethods.NIF_TIP;
        _nid.szTip = tip;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref _nid);
        _nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
    }

    private void OnWindowMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_APP_TRAYICON)
        {
            return;
        }

        if (wParam.ToInt64() != IconId)
        {
            return;
        }

        switch (lParam.ToInt64() & 0xFFFF)
        {
            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_LBUTTONDBLCLK:
                OpenPanelRequested?.Invoke();
                break;
            case NativeMethods.WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuOpenPanel, "Open Beacon");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuNotifications, "Notifications");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuRefreshAll, "Refresh All");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuSettings, "Settings");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuExit, "Exit");

            NativeMethods.GetCursorPos(out var position);
            // TrackPopupMenu 前必须 SetForegroundWindow，否则点击外部菜单不会消失
            NativeMethods.SetForegroundWindow(_messageWindow.Hwnd);
            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                position.X, position.Y, 0, _messageWindow.Hwnd, IntPtr.Zero);

            switch (command)
            {
                case MenuOpenPanel:
                    OpenPanelRequested?.Invoke();
                    break;
                case MenuNotifications:
                    NotificationsRequested?.Invoke();
                    break;
                case MenuRefreshAll:
                    RefreshAllRequested?.Invoke();
                    break;
                case MenuSettings:
                    SettingsRequested?.Invoke();
                    break;
                case MenuExit:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        _messageWindow.MessageReceived -= OnWindowMessage;
        if (_added)
        {
            NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref _nid);
            _added = false;
        }
        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
        foreach (var icon in _runtimeIcons.Values)
        {
            NativeMethods.DestroyIcon(icon);
        }
        _runtimeIcons.Clear();
    }
}
