using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace ScreenSwap.Windows.Utilities.WindowManager;

public partial class WindowManager
{
    // -------------------------------------------------------------------------
    // Screen swap: move a hero window to another screen, keeping its own state.
    // -------------------------------------------------------------------------

    public static unsafe void MoveWindowToScreen(WindowInfo window, ScreenInfo source, ScreenInfo dest, bool animate, int animMs)
    {
        var destBounds = ScaleBounds(window.RestoreBounds, source, dest);

        WindowPlacement placement = new() { length = sizeof(WindowPlacement) };
        _ = GetWindowPlacement(window.WindowHandle, ref placement);
        placement.flags = 0;
        placement.normalPosition = ToRect(destBounds);

        if (window.IsMinimized)
        {
            MoveMinimizedWindow(window.WindowHandle, placement);
        }
        else if (animate && !window.IsMaximized && !WillTriggerDpiChange(window.WindowHandle, source, dest))
        {
            MoveWindowAnimated(window.WindowHandle, placement, destBounds, animMs);
        }
        else
        {
            MoveWindowDirect(window.WindowHandle, placement, window.IsMaximized);
        }
    }

    // -------------------------------------------------------------------------
    // Window swap: each window takes the other's exact bounds and state.
    // -------------------------------------------------------------------------

    public static unsafe void SwapWindows(WindowInfo a, WindowInfo b, bool animate, int animMs)
    {
        var aDest = ToTuple(b.RestoreBounds);
        var bDest = ToTuple(a.RestoreBounds);

        var taskA = Task.Run(() => MoveWindowWithState(a, aDest, b.IsMaximized, animate, animMs));
        var taskB = Task.Run(() => MoveWindowWithState(b, bDest, a.IsMaximized, animate, animMs));
        Task.WaitAll(taskA, taskB);
    }

    private static unsafe void MoveWindowWithState(WindowInfo window, (int x, int y, int w, int h) dest, bool adoptMaximized, bool animate, int animMs)
    {
        WindowPlacement placement = new() { length = sizeof(WindowPlacement) };
        _ = GetWindowPlacement(window.WindowHandle, ref placement);
        placement.flags = 0;
        placement.normalPosition = ToRect(dest);

        if (window.IsMinimized)
        {
            placement.showCmd = SwShowminimized;
            _ = SetWindowPlacement(window.WindowHandle, ref placement);
            return;
        }

        if (animate && !window.IsMaximized && !adoptMaximized && !WillTriggerDpiChange(window.WindowHandle, window.RestoreBounds, dest))
            MoveWindowAnimated(window.WindowHandle, placement, dest, animMs);
        else
            MoveWindowDirect(window.WindowHandle, placement, adoptMaximized);
    }

    // -------------------------------------------------------------------------
    // Reveal: show hidden windows at their scaled destination (non-heroes after swap).
    // Uses SetWindowPlacement(SW_SHOWNOACTIVATE) — sends WM_SHOWWINDOW (restores
    // taskbar button), clears WS_MAXIMIZE before moving (so position isn't ignored).
    // -------------------------------------------------------------------------

    public static void RevealWindowsMoved(List<(WindowInfo window, ScreenInfo source, ScreenInfo dest)> moves)
    {
        _ = Parallel.ForEach(moves, item => RevealWindowMoved(item.window, item.source, item.dest));
    }

    public static unsafe void RevealWindowMoved(WindowInfo window, ScreenInfo source, ScreenInfo dest)
    {
        var d = ScaleBounds(window.RestoreBounds, source, dest);

        WindowPlacement placement = new() { length = sizeof(WindowPlacement) };
        _ = GetWindowPlacement(window.WindowHandle, ref placement);
        placement.flags = 0;
        placement.normalPosition = ToRect(d);

        if (window.IsMinimized)
        {
            MoveMinimizedWindow(window.WindowHandle, placement);
        }
        else
        {
            placement.showCmd = SwShownoactivate;
            _ = SetWindowPlacement(window.WindowHandle, ref placement);
            if (window.IsMaximized)
                _ = ShowWindow(window.WindowHandle, SwShowmaximized);
        }
    }

    // -------------------------------------------------------------------------
    // Teleport: move already-visible windows without hide/show cycle.
    // Used for non-heroes that teleport before hero animation starts.
    // -------------------------------------------------------------------------

    public static void TeleportWindows(List<(WindowInfo window, ScreenInfo source, ScreenInfo dest)> moves)
    {
        _ = Parallel.ForEach(moves, item => TeleportWindow(item.window, item.source, item.dest));
    }

    private static unsafe void TeleportWindow(WindowInfo window, ScreenInfo source, ScreenInfo dest)
    {
        var d = ScaleBounds(window.RestoreBounds, source, dest);

        WindowPlacement placement = new() { length = sizeof(WindowPlacement) };
        _ = GetWindowPlacement(window.WindowHandle, ref placement);
        placement.flags = 0;
        placement.normalPosition = ToRect(d);

        if (window.IsMinimized)
        {
            placement.showCmd = SwShowminimized;
            _ = SetWindowPlacement(window.WindowHandle, ref placement);
        }
        else
        {
            placement.showCmd = SwShownormal;
            _ = SetWindowPlacement(window.WindowHandle, ref placement);
            if (window.IsMaximized)
                _ = ShowWindow(window.WindowHandle, SwShowmaximized);
        }
    }

    // -------------------------------------------------------------------------
    // Primitives
    // -------------------------------------------------------------------------

    // Restore briefly on dest screen so the shell reassigns the taskbar button to that
    // monitor's taskbar, then re-minimize. SW_SHOWMINIMIZED alone only updates the stored
    // restore position — it doesn't cause the shell to move the taskbar button.
    private static void MoveMinimizedWindow(IntPtr hwnd, WindowPlacement placement)
    {
        placement.showCmd = SwShownormal;
        _ = SetWindowPlacement(hwnd, ref placement);
        _ = ShowWindow(hwnd, SwMinimize);
    }

    private static void MoveWindowAnimated(IntPtr hwnd, WindowPlacement placement, (int x, int y, int w, int h) dest, int animMs)
    {
        Rect currentRect = default;
        _ = GetWindowRect(hwnd, ref currentRect);

        AnimateWindowMove(hwnd,
            currentRect.Left, currentRect.Top, currentRect.Right - currentRect.Left, currentRect.Bottom - currentRect.Top,
            dest.x, dest.y, dest.w, dest.h, animMs);

        placement.showCmd = SwShownormal;
        _ = SetWindowPlacement(hwnd, ref placement);
    }

    private static void MoveWindowDirect(IntPtr hwnd, WindowPlacement placement, bool maximize)
    {
        placement.showCmd = SwShownormal;
        _ = SetWindowPlacement(hwnd, ref placement);

        if (maximize)
            _ = ShowWindow(hwnd, SwShowmaximized);
    }

    // -------------------------------------------------------------------------
    // Scaling and DPI helpers
    // -------------------------------------------------------------------------

    private static (int x, int y, int w, int h) ScaleBounds(Rectangle bounds, ScreenInfo source, ScreenInfo dest)
    {
        var sx = (double)dest.Bounds.Width  / source.Bounds.Width;
        var sy = (double)dest.Bounds.Height / source.Bounds.Height;
        return (
            dest.Bounds.X + (int)((bounds.X - source.Bounds.X) * sx),
            dest.Bounds.Y + (int)((bounds.Y - source.Bounds.Y) * sy),
            (int)(bounds.Width  * sx),
            (int)(bounds.Height * sy));
    }

    private static bool WillTriggerDpiChange(IntPtr hwnd, ScreenInfo source, ScreenInfo dest)
        => IsPerMonitorDpiAware(hwnd) && (source.Bounds.Width != dest.Bounds.Width || source.Bounds.Height != dest.Bounds.Height);

    private static bool WillTriggerDpiChange(IntPtr hwnd, Rectangle src, (int x, int y, int w, int h) dst)
        => IsPerMonitorDpiAware(hwnd) && (src.Width != dst.w || src.Height != dst.h);

    private static Rect CaptureRect(IntPtr hwnd) { Rect r = default; _ = GetWindowRect(hwnd, ref r); return r; }

    private static Rect ToRect((int x, int y, int w, int h) d) =>
        new() { Left = d.x, Top = d.y, Right = d.x + d.w, Bottom = d.y + d.h };

    private static (int x, int y, int w, int h) ToTuple(Rectangle r) => (r.X, r.Y, r.Width, r.Height);
}
