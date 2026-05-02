using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenSwap.Windows.Utilities.ScreenManager;

public partial class ScreenManager
{
    public static IEnumerable<ScreenInfo> GetScreens()
    {
        List<ScreenInfo> screens = [];

        _ = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            AddMonitor(screens, hMonitor);
            return true;
        }, IntPtr.Zero);

        return screens;
    }

    private static unsafe void AddMonitor(List<ScreenInfo> screens, IntPtr hMonitor)
    {
        MonitorInfoEx mi = new() { cbSize = sizeof(MonitorInfoEx) };
        if (!GetMonitorInfo(hMonitor, ref mi))
            return;

        var bounds = new Rectangle(mi.rcMonitor.Left,
                                   mi.rcMonitor.Top,
                                   mi.rcMonitor.Right - mi.rcMonitor.Left,
                                   mi.rcMonitor.Bottom - mi.rcMonitor.Top);

        var workArea = new Rectangle(mi.rcWork.Left,
                                     mi.rcWork.Top,
                                     mi.rcWork.Right - mi.rcWork.Left,
                                     mi.rcWork.Bottom - mi.rcWork.Top);

        screens.Add(new ScreenInfo
        {
            Name = new string(mi.szDevice),
            IsPrimary = (mi.dwFlags & MonitorPrimary) != 0,
            Bounds = bounds,
            WorkingArea = workArea
        });
    }

    private const uint MonitorPrimary = 1;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct MonitorInfoEx
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
        public fixed char szDevice[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);
}

