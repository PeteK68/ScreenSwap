using ScreenSwap.Windows.Utilities.KeyboardInterceptor;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenSwap.Windows.Utilities.HotKeys;

public partial class HotKeyManager : IDisposable
{
    private const int WmHotKey = 0x0312;
    private bool messageHandlerRegistered;
    private readonly Dictionary<int, HotKeyAction> hotKeyRegistry = [];
    private readonly GlobalKeyboardHook globalKeyboardHook = new();
    private HotKeyAction activeHotKey;

    public event Action EscapePressed;

    public HotKeyManager()
    {
        globalKeyboardHook.KeyUp += GlobalKeyboardHotKey_KeyUp;
        globalKeyboardHook.KeyDown += GlobalKeyboardHook_KeyDown;
    }

    private void GlobalKeyboardHook_KeyDown(object sender, RawKeyEventArgs e)
    {
        if (e.VkCode == (int)VirtualKey.Escape && activeHotKey != null)
        {
            activeHotKey = null;
            EscapePressed?.Invoke();
        }
    }

    private void GlobalKeyboardHotKey_KeyUp(object sender, RawKeyEventArgs e)
    {
        var vk = (VirtualKey)e.VkCode;
        var mod = HotKey.KeysToModifers.TryGetValue(vk, out var value) ? value : ModifierKeys.None;

        if (activeHotKey is not null
            && mod != ModifierKeys.None
            && (activeHotKey.Modifiers & mod) != 0)
        {
            activeHotKey.OnHotKeyTerminated?.Invoke(activeHotKey);
            activeHotKey = null;
        }
    }

    public HotKeyAction this[int id] => hotKeyRegistry[id];

    public bool Register(HotKeyAction hotKeyAction)
    {
        var registered = RegisterHotKey(IntPtr.Zero, hotKeyAction.Id, (uint)hotKeyAction.Modifiers, hotKeyAction.VirtualKeyCode);

        if (registered)
        {
            if (!messageHandlerRegistered)
            {
                ComponentDispatcher.ThreadFilterMessage += ComponentDispatcherThreadFilterMessage;
                messageHandlerRegistered = true;
            }

            hotKeyRegistry.Add(hotKeyAction.Id, hotKeyAction);
        }

        return registered;
    }

    public void UnregisterAll()
    {
        int[] ids = [.. hotKeyRegistry.Keys];
        foreach (var id in ids)
            _ = Unregister(id);
    }

    public bool Unregister(int id)
    {
        if (!hotKeyRegistry.ContainsKey(id))
            return false;

        var success = UnregisterHotKey(IntPtr.Zero, id);
        if (success)
            _ = hotKeyRegistry.Remove(id);

        return success;
    }

    public bool Unregister(HotKeyAction hotKeyAction) => Unregister(hotKeyAction.Id);

    private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (!handled)
        {
            if (msg.message == WmHotKey)
            {
                if (hotKeyRegistry.TryGetValue((int)msg.wParam, out var hotKeyAction))
                {
                    if (hotKeyAction == activeHotKey)
                    {
                        hotKeyAction.OnHotKeyRepeated?.Invoke(hotKeyAction);
                    }
                    else
                    {
                        activeHotKey = hotKeyAction;
                        hotKeyAction.OnHotKeyActivated?.Invoke(hotKeyAction);

                        if (!ModifierState.AreModifiersStillHeld(hotKeyAction.Modifiers))
                        {
                            hotKeyAction.OnHotKeyTerminated?.Invoke(hotKeyAction);
                            activeHotKey = null;
                        }
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        if (messageHandlerRegistered)
        {
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
            messageHandlerRegistered = false;
        }

        globalKeyboardHook.KeyUp -= GlobalKeyboardHotKey_KeyUp;
        globalKeyboardHook.KeyDown -= GlobalKeyboardHook_KeyDown;

        globalKeyboardHook.Dispose();
        GC.SuppressFinalize(this);
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

}
