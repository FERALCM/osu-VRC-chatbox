using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OsuVrcChatbox.App.Services;

/// <summary>
/// Registers a single global hotkey (default global pause, plan §22) via the Win32 RegisterHotKey API
/// and routes WM_HOTKEY through the window's message loop. No global keyboard hook, no admin rights.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xB001;

    [Flags]
    private enum Mod { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    /// <summary>Raised on the UI thread when the hotkey fires.</summary>
    public event Action? Pressed;

    /// <summary>Hooks the window and (re)registers <paramref name="hotkey"/> (e.g. "Control+Alt+P").</summary>
    public bool Initialize(Window window, string hotkey)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        return Register(hotkey);
    }

    public bool Register(string hotkey)
    {
        Unregister();
        if (!TryParse(hotkey, out Mod mods, out uint vk)) return false;
        _registered = RegisterHotKey(_hwnd, HotkeyId, (uint)mods, vk);
        return _registered;
    }

    private void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string hotkey, out Mod mods, out uint vk)
    {
        mods = Mod.None;
        vk = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        foreach (string raw in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= Mod.Control; break;
                case "alt": mods |= Mod.Alt; break;
                case "shift": mods |= Mod.Shift; break;
                case "win":
                case "windows": mods |= Mod.Win; break;
                default:
                    if (Enum.TryParse<Key>(raw, ignoreCase: true, out var key) && key != Key.None)
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return vk != 0;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
