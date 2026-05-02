using System;
using System.Collections.Generic;
using System.Drawing;

namespace ScreenSwap.Windows.Utilities.WindowManager;

public partial class WindowManager
{
    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static Rectangle GetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return Rectangle.Empty;
        Rect r = default;
        return GetWindowRect(hwnd, ref r) ? RectToRectangle(r) : Rectangle.Empty;
    }

    public static Rectangle GetTopWindowBoundsOnScreen(Rectangle screenBounds)
    {
        var hwnd = GetTopWindow(IntPtr.Zero);

        while (hwnd != IntPtr.Zero)
        {
            if (IsWindowVisible(hwnd)
                && GetWindow(hwnd, GwOwner) == IntPtr.Zero
                && (GetWindowLong(hwnd, GwlExStyle) & WsExToolWindow) == 0
                && GetWindowTextLength(hwnd) > 0)
            {
                Rect r = default;
                if (GetWindowRect(hwnd, ref r))
                {
                    var bounds = RectToRectangle(r);
                    if (screenBounds.Contains(new Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2))))
                        return bounds;
                }
            }

            hwnd = GetWindow(hwnd, GwHwndNext);
        }

        return Rectangle.Empty;
    }

    public static bool IsCompletelyOccluded(WindowInfo window, IEnumerable<WindowInfo> windowsAbove)
    {
        if (window.IsMinimized) return true;
        Rect r = default;
        if (!GetWindowRect(window.WindowHandle, ref r)) return false;
        var bounds = RectToRectangle(r);
        if (bounds.IsEmpty) return true;

        var uncovered = new List<Rectangle> { bounds };
        foreach (var above in windowsAbove)
        {
            if (above.IsMinimized) continue;
            Rect ar = default;
            if (!GetWindowRect(above.WindowHandle, ref ar)) continue;
            var ab = RectToRectangle(ar);
            var next = new List<Rectangle>();
            foreach (var region in uncovered)
            {
                if (!region.IntersectsWith(ab)) { next.Add(region); continue; }

                if (region.Top < ab.Top) next.Add(Rectangle.FromLTRB(region.Left, region.Top, region.Right, ab.Top));
                if (region.Bottom > ab.Bottom) next.Add(Rectangle.FromLTRB(region.Left, ab.Bottom, region.Right, region.Bottom));
                if (region.Left < ab.Left) next.Add(Rectangle.FromLTRB(region.Left, Math.Max(region.Top, ab.Top), ab.Left, Math.Min(region.Bottom, ab.Bottom)));
                if (region.Right > ab.Right) next.Add(Rectangle.FromLTRB(ab.Right, Math.Max(region.Top, ab.Top), region.Right, Math.Min(region.Bottom, ab.Bottom)));
            }

            uncovered = next;
            if (uncovered.Count == 0) return true;
        }

        return false;
    }

    public static void HideWindow(IntPtr hWnd) =>
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpHideWindow);

    public static void ShowWindowNoActivate(IntPtr hWnd) =>
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpShowWindow | SwpNoActivate);

    public static void ShowMaximized(IntPtr hWnd) => ShowWindow(hWnd, SwShowmaximized);

    public static void FocusWindow(IntPtr hWnd)
    {
        _ = SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        _ = SetForegroundWindow(hWnd);
    }
}
