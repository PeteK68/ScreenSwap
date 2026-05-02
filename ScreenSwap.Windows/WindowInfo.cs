using System;
using System.Drawing;

namespace ScreenSwap.Windows;

public class WindowInfo
{
    public IntPtr WindowHandle { get; set; }
    public string Title { get; set; }
    public bool IsMaximized { get; set; }
    public bool IsMinimized { get; set; }
    public Rectangle RestoreBounds { get; set; }
}
