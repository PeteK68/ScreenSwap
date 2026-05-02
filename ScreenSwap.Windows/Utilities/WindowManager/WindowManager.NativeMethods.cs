using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenSwap.Windows.Utilities.WindowManager;

public partial class WindowManager
{
    private static Rectangle RectToRectangle(Rect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    // WINDOWPLACEMENT.normalPosition uses workspace coordinates — screen coords offset by
    // the primary monitor's working-area origin (non-zero when taskbar is on left or top).
    // Convert a screen-coord rectangle to workspace coords so it matches normalPosition.
    private static unsafe Rectangle ScreenToWorkspace(Rectangle r)
    {
        var hPrimary = MonitorFromPoint(new System.Drawing.Point(0, 0), MonitorDefaultToNearest);
        MonitorInfo mi = new() { cbSize = sizeof(MonitorInfo) };
        if (!GetMonitorInfo(hPrimary, ref mi)) return r;
        var offset = new System.Drawing.Point(mi.rcWork.Left - mi.rcMonitor.Left, mi.rcWork.Top - mi.rcMonitor.Top);
        return new Rectangle(r.X - offset.X, r.Y - offset.Y, r.Width, r.Height);
    }

    private const int MonitorDefaultToNearest = 2;
    private const int SwShownormal = 1;
    private const int SwShowminimized = 2;
    private const int SwShowmaximized = 3;
    private const int SwShownoactivate = 4;
    private const int SwMinimize = 6;
    private const int GwOwner = 4;
    private const int GwHwndNext = 2;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private static readonly IntPtr hWndTop = IntPtr.Zero;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpAsyncWindowPos = 0x4000;
    private const uint SwpFrameChanged = 0x0020;

    private static readonly IntPtr dpiAwarenessContextPerMonitorAware = new(-3);
    private static readonly IntPtr dpiAwarenessContextPerMonitorAwareV2 = new(-4);

    private static bool IsPerMonitorDpiAware(IntPtr hWnd)
    {
        var ctx = GetWindowDpiAwarenessContext(hWnd);
        return AreDpiAwarenessContextsEqual(ctx, dpiAwarenessContextPerMonitorAware)
            || AreDpiAwarenessContextsEqual(ctx, dpiAwarenessContextPerMonitorAwareV2);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point minPosition;
        public Point maxPosition;
        public Rect normalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
    private static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromPoint(System.Drawing.Point pt, int dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, ref Rect lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [LibraryImport("winmm.dll")]
    private static partial uint timeBeginPeriod(uint uPeriod);

    [LibraryImport("winmm.dll")]
    private static partial uint timeEndPeriod(uint uPeriod);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetTopWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr BeginDeferWindowPos(int nNumWindows);

    // Returns the DPI awareness context of the given window's thread.
    // PROCESS_DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE   = -3
    // PROCESS_DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AreDpiAwarenessContextsEqual(IntPtr dpiContextA, IntPtr dpiContextB);

    [LibraryImport("user32.dll")]
    private static partial IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndDeferWindowPos(IntPtr hWinPosInfo);
}
