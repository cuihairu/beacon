using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>全局热键（RFC §38）：RegisterHotKey 挂在消息窗口上，设置可改，冲突时注册失败。</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;

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

    /// <summary>注册/重注册热键（如 "Ctrl+Alt+B"）。失败（解析错误或被占用）返回 false。</summary>
    public bool Register(string hotkey)
    {
        Unregister();
        if (!TryParse(hotkey, out var modifiers, out var virtualKey))
        {
            _logger.LogWarning("Invalid hotkey string: {Hotkey}", hotkey);
            return false;
        }
        if (!NativeMethods.RegisterHotKey(_messageWindow.Hwnd, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
        {
            _logger.LogWarning(
                "RegisterHotKey({Hotkey}) failed, error {Error}（可能与其他应用冲突）",
                hotkey, Marshal.GetLastWin32Error());
            return false;
        }
        _current = hotkey;
        return true;
    }

    public void Unregister()
    {
        NativeMethods.UnregisterHotKey(_messageWindow.Hwnd, HotkeyId);
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
