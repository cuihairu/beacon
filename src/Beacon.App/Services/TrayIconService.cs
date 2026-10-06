using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
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
    private const int MenuExit = 99;

    private readonly Win32MessageWindow _messageWindow;
    private readonly ILogger<TrayIconService> _logger;
    private NativeMethods.NOTIFYICONDATA _nid;
    private IntPtr _icon;
    private bool _added;

    public event Action? OpenPanelRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService(Win32MessageWindow messageWindow, ILogger<TrayIconService> logger)
    {
        _messageWindow = messageWindow;
        _logger = logger;
        _messageWindow.MessageReceived += OnWindowMessage;
    }

    private static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "beacon.ico");

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
    }
}
