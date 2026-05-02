using System;
using System.Runtime.Versioning;

namespace ScreenSwap.Windows.Utilities.WindowTheme;

[SupportedOSPlatform("windows6.1")]
public static partial class WindowTheme
{
    public static unsafe void SetDarkMode(IntPtr hwnd, bool isDark)
    {
        var value = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmaUseImmersiveDarkMode, (IntPtr)(&value), sizeof(int));
    }
}
