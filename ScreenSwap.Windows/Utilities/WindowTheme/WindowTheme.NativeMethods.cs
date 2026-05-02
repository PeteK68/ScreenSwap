using System;
using System.Runtime.InteropServices;

namespace ScreenSwap.Windows.Utilities.WindowTheme;

public static partial class WindowTheme
{
    private const int DwmaUseImmersiveDarkMode = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, IntPtr pvAttribute, int cbAttribute);
}
