using ScreenSwap.Windows.Utilities.VirtualDesktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: DisableRuntimeMarshalling]

namespace ScreenSwap.Windows.Utilities.WindowManager;

[SupportedOSPlatform("windows6.1")]
public partial class WindowManager
{
    public static unsafe List<WindowInfo> GetWindowsOnCurrentDesktop()
    {
        using VirtualDesktopManager vdm = new();
        List<WindowInfo> windows = [];

        _ = EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            // Skip owned windows (tooltips, dropdowns, child dialogs)
            if (GetWindow(hWnd, GwOwner) != IntPtr.Zero)
            {
                return true;
            }

            // Skip tool windows that don't appear in the taskbar
            var exStyle = GetWindowLong(hWnd, GwlExStyle);
            if ((exStyle & WsExToolWindow) != 0)
            {
                return true;
            }

            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
            {
                return true;
            }

            if (!vdm.IsWindowOnCurrentVirtualDesktop(hWnd))
            {
                return true;
            }

            var titleBuffer = new char[titleLength + 1];
            _ = GetWindowText(hWnd, titleBuffer, titleBuffer.Length);
            var title = new string(titleBuffer, 0, titleLength);

            if (string.IsNullOrEmpty(title))
            {
                return true;
            }

            WindowPlacement placement = new() { length = sizeof(WindowPlacement) };
            if (!GetWindowPlacement(hWnd, ref placement))
            {
                return true;
            }

            var isMaximized = placement.showCmd == SwShowmaximized;
            var isMinimized = placement.showCmd == SwShowminimized;

            // All movement uses SetWindowPlacement which takes workspace coordinates for
            // normalPosition. For normal windows GetWindowRect gives screen coords, which
            // differ from workspace coords by the primary monitor's working-area offset
            // (typically the taskbar height). Convert to workspace coords so everything
            // is in the same coordinate space as SetWindowPlacement expects.
            Rectangle restoreBounds;
            if (isMaximized || isMinimized)
            {
                // normalPosition is already in workspace coords.
                restoreBounds = RectToRectangle(placement.normalPosition);
            }
            else
            {
                // GetWindowRect returns screen coords — convert to workspace coords.
                Rect actualRect = default;
                _ = GetWindowRect(hWnd, ref actualRect);
                restoreBounds = ScreenToWorkspace(RectToRectangle(actualRect));
            }

            Debug.WriteLine($"Window: {title}, showCmd={placement.showCmd}, restore={restoreBounds}");

            windows.Add(new WindowInfo
            {
                Title = title,
                WindowHandle = hWnd,
                IsMaximized = isMaximized,
                IsMinimized = isMinimized,
                RestoreBounds = restoreBounds
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }
}
