using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>全局热键（RFC §38）：RegisterHotKey 挂在消息窗口上，设置可改，冲突时注册失败。
/// SET-UX-11：失败保留原有效绑定——旧实现先 Unregister 再校验/试挂，无效输入或被占用会把能用的
/// 热键摘没；现改为新键先试挂临时槽（id 2）证可用，再摘临时槽与旧键、挂主槽（同窗同组合双槽
/// 会被系统拒 1409，临时槽不可与主槽并存），失败旧键原样保留。</summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private const int TempHotkeyId = 2; // SET-UX-11：试注册槽——新键可用性先在临时槽验证，不动主槽

    private readonly Win32MessageWindow _messageWindow;
    private readonly ILogger<HotkeyService> _logger;
    private string _current = string.Empty;

    public event Action? Triggered;

    public HotkeyService(Win32MessageWindow messageWindow, ILogger<HotkeyService> logger)
    {
        _messageWindow = messageWindow;
        _logger = logger;
        _messageWindow.MessageReceived += OnWindowMessage;
    }

    /// <summary>当前生效热键文本。</summary>
    public string Current => _current;

    /// <summary>注册/重注册热键（如 "Ctrl+Alt+B"）。失败（解析错误或被占用）返回 false 且旧键原样保留。</summary>
    public bool Register(string hotkey)
    {
        // SET-UX-11：解析失败不再摘旧绑定（校验先于任何 Unregister）
        if (!TryParse(hotkey, out var modifiers, out var virtualKey))
        {
            _logger.LogWarning("Invalid hotkey string: {Hotkey}", hotkey);
            return false;
        }
        if (string.Equals(hotkey, _current, StringComparison.OrdinalIgnoreCase))
        {
            return true; // 同键幂等：不重挂（免抖动窗口）
        }
        // 新键先试挂临时槽：失败=被占用/系统拒绝，旧键原样保留（原有效绑定不丢）
        if (!NativeMethods.RegisterHotKey(_messageWindow.Hwnd, TempHotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
        {
            _logger.LogWarning(
                "RegisterHotKey({Hotkey}) failed, error {Error}（可能与其他应用冲突；原热键 {Current} 保留）",
                hotkey, Marshal.GetLastWin32Error(), _current);
            return false;
        }
        // 试挂证可用：先摘临时槽再换主槽——同一窗口同组合挂双槽会被系统拒（1409，run 45381459081
        // 日志实证：temp 槽不清就挂主槽必撞自己），顺序必须 temp 摘 → 主摘 → 主挂
        NativeMethods.UnregisterHotKey(_messageWindow.Hwnd, TempHotkeyId);
        NativeMethods.UnregisterHotKey(_messageWindow.Hwnd, HotkeyId);
        if (!NativeMethods.RegisterHotKey(_messageWindow.Hwnd, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
        {
            // 极窄竞态（试挂通过后他进程抢注同组合）：回滚旧键保底，不静默丢绑定
            _logger.LogWarning(
                "RegisterHotKey({Hotkey}) primary slot re-register failed, error {Error}",
                hotkey, Marshal.GetLastWin32Error());
            var previous = _current;
            _current = string.Empty;
            if (previous.Length > 0 && TryParse(previous, out var prevModifiers, out var previousKey) &&
                NativeMethods.RegisterHotKey(_messageWindow.Hwnd, HotkeyId, prevModifiers | NativeMethods.MOD_NOREPEAT, previousKey))
            {
                _current = previous; // 旧键抢回：绑定不丢，仅本次改键失败
            }
            return false;
        }
        _current = hotkey;
        return true;
    }

    public void Unregister()
    {
        NativeMethods.UnregisterHotKey(_messageWindow.Hwnd, HotkeyId);
        NativeMethods.UnregisterHotKey(_messageWindow.Hwnd, TempHotkeyId);
        _current = string.Empty;
    }

    private void OnWindowMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Triggered?.Invoke();
        }
    }

    /// <summary>解析 "Ctrl+Alt+B" / "Ctrl+Shift+F5" 形式；必须含修饰键（禁单键全局热键）。</summary>
    public static bool TryParse(string hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }
        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "win": modifiers |= NativeMethods.MOD_WIN; break;
                default: return false;
            }
        }
        var key = parts[^1];
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is (>= '0' and <= '9') or (>= 'A' and <= 'Z'))
            {
                virtualKey = c;
                return true;
            }
            return false;
        }
        if (key.Length >= 2
            && key[0] is 'F' or 'f'
            && int.TryParse(key[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            virtualKey = 0x70u + (uint)(functionKey - 1); // VK_F1 = 0x70
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        _messageWindow.MessageReceived -= OnWindowMessage;
        Unregister();
    }
}
