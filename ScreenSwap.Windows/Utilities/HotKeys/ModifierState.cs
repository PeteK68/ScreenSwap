using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ScreenSwap.Windows.Utilities.HotKeys;

public static partial class ModifierState
{
    public static bool AreModifiersStillHeld(ModifierKeys modifiers)
    {
        if ((modifiers & ModifierKeys.Windows) != 0 && GetAsyncKeyState(0x5B) >= 0 && GetAsyncKeyState(0x5C) >= 0) return false;
        if ((modifiers & ModifierKeys.Alt)     != 0 && GetAsyncKeyState(0x12) >= 0) return false;
        if ((modifiers & ModifierKeys.Control) != 0 && GetAsyncKeyState(0x11) >= 0) return false;
        if ((modifiers & ModifierKeys.Shift)   != 0 && GetAsyncKeyState(0x10) >= 0) return false;
        return true;
    }

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);
}
