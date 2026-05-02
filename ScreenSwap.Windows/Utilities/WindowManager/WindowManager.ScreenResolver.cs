using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace ScreenSwap.Windows.Utilities.WindowManager;

public partial class WindowManager
{
    public static unsafe ScreenInfo GetScreenForWindow(List<ScreenInfo> screens, WindowInfo window)
    {
        var hMonitor = MonitorFromWindow(window.WindowHandle, MonitorDefaultToNearest);

        MonitorInfo monitorInfo = new() { cbSize = sizeof(MonitorInfo) };
        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var monitorBounds = RectToRectangle(monitorInfo.rcMonitor);
            var match = screens.Find(s => s.Bounds == monitorBounds);

            if (match is not null)
            {
                Debug.WriteLine($"[ScreenResolver] '{window.Title}' → {match.Name} via MonitorFromWindow (bounds={monitorBounds})");
                return match;
            }

            Debug.WriteLine($"[ScreenResolver] '{window.Title}' MonitorFromWindow bounds {monitorBounds} matched no screen. Available: {string.Join(", ", screens.Select(s => s.Bounds.ToString()))}");
        }

        // Fallback: use the actual window rect center point to find the screen.
        Rect actualRect = default;
        if (GetWindowRect(window.WindowHandle, ref actualRect))
        {
            var center = new Point((actualRect.Left + actualRect.Right) / 2,
                                   (actualRect.Top + actualRect.Bottom) / 2);

            var match = screens.Find(s => s.Bounds.Contains(center));

            if (match is not null)
            {
                Debug.WriteLine($"[ScreenResolver] '{window.Title}' → {match.Name} via center-point fallback (center={center})");
                return match;
            }
        }

        // Last resort: most overlap with restore bounds
        var lastResort = GetScreenByArea(screens, window.RestoreBounds);
        Debug.WriteLine($"[ScreenResolver] '{window.Title}' → {lastResort.Name} via area overlap (RestoreBounds={window.RestoreBounds})");

        return lastResort;
    }

    private static ScreenInfo GetScreenByArea(List<ScreenInfo> screens, Rectangle bounds)
    {
        var bestScreen = screens[0];
        var bestArea = 0;

        foreach (var screen in screens)
        {
            var intersection = Rectangle.Intersect(bounds, screen.Bounds);
            var area = intersection.Width * intersection.Height;
            if (area > bestArea)
            {
                bestArea = area;
                bestScreen = screen;
            }
        }

        return bestScreen;
    }
}
