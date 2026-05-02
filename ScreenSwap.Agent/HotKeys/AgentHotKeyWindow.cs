using ScreenSwap.Windows.Utilities.HotKeys;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ScreenSwap.Agent.HotKeys;

[SupportedOSPlatform("windows6.1")]
internal sealed partial class AgentHotKeyWindow : NativeWindow, IDisposable
{
    private const int WmHotKey = 0x0312;

    private readonly Dictionary<int, HotKeyAction> hotKeys = [];
    private readonly LowLevelKeyboardHook keyboardHook;
    private HotKeyAction activeHotKey;

    public event Action EscapePressed;

    public AgentHotKeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "ScreenSwap.Agent.HotKeys" });
        keyboardHook = new LowLevelKeyboardHook(OnKeyDown, OnKeyUp);
    }

    public bool Register(HotKeyAction hotKey)
    {
        if (hotKeys.ContainsKey(hotKey.Id))
            return true;

        var registered = RegisterHotKey(Handle, hotKey.Id, (uint)hotKey.Modifiers, hotKey.VirtualKeyCode);
        if (registered)
            hotKeys.Add(hotKey.Id, hotKey);

        return registered;
    }

    public void UnregisterAll()
    {
        foreach (var id in hotKeys.Keys)
            _ = UnregisterHotKey(Handle, id);

        hotKeys.Clear();
        activeHotKey = null;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && hotKeys.TryGetValue((int)m.WParam, out var hotKey))
        {
            if (ReferenceEquals(activeHotKey, hotKey))
            {
                hotKey.OnHotKeyRepeated?.Invoke(hotKey);
            }
            else
            {
                activeHotKey = hotKey;
                hotKey.OnHotKeyActivated?.Invoke(hotKey);

                if (!ModifierState.AreModifiersStillHeld(hotKey.Modifiers))
                {
                    hotKey.OnHotKeyTerminated?.Invoke(hotKey);
                    activeHotKey = null;
                }
            }

            return;
        }

        base.WndProc(ref m);
    }

    private void OnKeyDown(VirtualKey key)
    {
        if (key != VirtualKey.Escape || activeHotKey is null)
            return;

        activeHotKey = null;
        EscapePressed?.Invoke();
    }

    private void OnKeyUp(VirtualKey key)
    {
        var modifier = HotKey.KeysToModifers.TryGetValue(key, out var value)
            ? value
            : System.Windows.Input.ModifierKeys.None;

        if (activeHotKey is not null
            && modifier != System.Windows.Input.ModifierKeys.None
            && (activeHotKey.Modifiers & modifier) != 0)
        {
            var hotKey = activeHotKey;
            activeHotKey = null;
            hotKey.OnHotKeyTerminated?.Invoke(hotKey);
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        keyboardHook.Dispose();
        DestroyHandle();
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
