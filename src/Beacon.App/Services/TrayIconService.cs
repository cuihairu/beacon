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
    private const int MenuKeepPanel = 5;
    private const int MenuExit = 99;

    private readonly Win32MessageWindow _messageWindow;
    private readonly ILogger<TrayIconService> _logger;
    private readonly Dictionary<Severity, IntPtr> _severityIcons = [];
    private readonly Dictionary<(byte A, byte R, byte G, byte B), IntPtr> _runtimeIcons = [];
    private NativeMethods.NOTIFYICONDATA _nid;
    private IntPtr _icon;
    private bool _added;
    private uint _taskbarCreatedMsg; // Explorer 重启广播的动态消息号（Initialize 注册）

    public event Action? OpenPanelRequested;
    public event Action? SettingsRequested;
    public event Action? NotificationsRequested;
    public event Action? RefreshAllRequested;
    public event Action? ExitRequested;

    /// <summary>「Keep Panel Open」勾选态读取（App 注入 config 实时值；null=未注入按未勾选渲染）。</summary>
    public Func<bool>? IsKeepPanelOn { get; set; }

    /// <summary>「Keep Panel Open」被点击（App 落盘 + 打勾即把面板显示出来）。</summary>
    public event Action? KeepPanelToggled;

    public TrayIconService(Win32MessageWindow messageWindow, ILogger<TrayIconService> logger)
    {
        _messageWindow = messageWindow;
        _logger = logger;
        _messageWindow.MessageReceived += OnWindowMessage;
    }

    private static string IconPath => AppIcon.Path; // 图标路径唯一来源（AppIcon）

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

        // Explorer 重启会销毁全部托盘图标并以 TaskbarCreated 广播（动态消息号）通知——收到即重发 NIM_ADD（master-plan §9）
        _taskbarCreatedMsg = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
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

    /// <summary>
    /// 32×32 logo 剪影按级别色染色（用户确认的口径：级别色 = logo 换色；B-706 配置色即刻生效）；
    /// 白色剪影 × 对角色阵 = 目标色，alpha 原样保留；按颜色缓存 HICON，退出时统一销毁。
    /// </summary>
    private IntPtr DrawSeverityIcon(global::Windows.UI.Color color)
    {
        var key = (color.A, color.R, color.G, color.B);
        if (_runtimeIcons.TryGetValue(key, out var cached))
        {
            return cached;
        }
        using var mask = LoadTrayLogoMask();
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix
            {
                Matrix00 = color.R / 255f,
                Matrix11 = color.G / 255f,
                Matrix22 = color.B / 255f,
            });
            var destination = new System.Drawing.Rectangle(0, 0, 32, 32);
            graphics.DrawImage(
                mask, destination, 0, 0, mask.Width, mask.Height,
                System.Drawing.GraphicsUnit.Pixel, attributes);
        }
        var icon = bitmap.GetHicon();
        _runtimeIcons[key] = icon;
        return icon;
    }

    /// <summary>嵌入的 logo 白色剪影（Assets/tray-logo-mask.png，随程序集分发，不依赖发布目录散文件）。</summary>
    private static System.Drawing.Bitmap LoadTrayLogoMask()
    {
        using var stream = typeof(TrayIconService).Assembly.GetManifestResourceStream("Beacon.App.Assets.tray-logo-mask.png")
            ?? throw new InvalidOperationException("嵌入资源缺失：Beacon.App.Assets.tray-logo-mask.png");
        return new System.Drawing.Bitmap(stream);
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
        if (_taskbarCreatedMsg != 0 && msg == _taskbarCreatedMsg)
        {
            RestoreAfterExplorerRestart();
            return;
        }

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

    /// <summary>
    /// Explorer 重启后托盘恢复（master-plan §9）：用现成 NID 重发 NIM_ADD——hIcon 仍持最后一次
    /// SetSeverity 写入的级别色图标句柄（运行时绘制的 HICON 在本进程缓存里活着），恢复即回到当前级别色。
    /// </summary>
    private void RestoreAfterExplorerRestart()
    {
        _added = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref _nid);
        if (!_added)
        {
            _logger.LogWarning("Tray icon re-add after Explorer restart failed: {Error}", Marshal.GetLastWin32Error());
        }
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (IntPtr)MenuOpenPanel, "Open Beacon");
            // 可勾选项（2026-10-11 用户令）：打勾=面板常驻不失焦自隐；勾选态每次开菜单实时读 config
            var keepPanelOn = IsKeepPanelOn?.Invoke() == true;
            NativeMethods.AppendMenuW(
                menu,
                NativeMethods.MF_STRING | (keepPanelOn ? NativeMethods.MF_CHECKED : 0u),
                (IntPtr)MenuKeepPanel,
                "Keep Panel Open");
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
                case MenuKeepPanel:
                    KeepPanelToggled?.Invoke();
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
