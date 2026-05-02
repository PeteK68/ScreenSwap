using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ScreenSwap.Windows.Utilities.HotKeys;

[SupportedOSPlatform("windows6.1")]
public static partial class GlobalHotkeyConflictManager
{
    private const int TestHotKeyId = 0x53535750;

    public static HotKeyConflict Check(HotKey hotKey, params HotKey[] screenSwapHotKeys)
    {
        if (hotKey is null || !hotKey.IsValid)
            return HotKeyConflict.None;

        foreach (var existing in screenSwapHotKeys)
        {
            if (existing is not null && existing.IsValid && AreSame(hotKey, existing))
                return HotKeyConflict.ScreenSwapShortcut;
        }

        return CanRegister(hotKey)
            ? HotKeyConflict.None
            : HotKeyConflict.UnavailableGlobalShortcut;
    }

    public static bool AreSame(HotKey left, HotKey right)
        => left.Key == right.Key && left.Modifiers == right.Modifiers;

    private static bool CanRegister(HotKey hotKey)
    {
        var registered = RegisterHotKey(IntPtr.Zero, TestHotKeyId, (uint)hotKey.Modifiers, (uint)hotKey.VirtualKeyCode);
        if (registered)
            _ = UnregisterHotKey(IntPtr.Zero, TestHotKeyId);

        return registered;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}

public enum HotKeyConflict
{
    None,
    ScreenSwapShortcut,
    UnavailableGlobalShortcut
}
