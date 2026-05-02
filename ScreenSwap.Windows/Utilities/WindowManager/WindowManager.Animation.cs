using System;
using System.Diagnostics;
using System.Threading;

namespace ScreenSwap.Windows.Utilities.WindowManager;

public partial class WindowManager
{
    private static double EaseInOut(double t) => (t < 0.5) ? (2 * t * t) : (-1 + ((4 - (2 * t)) * t));

    private static void AnimateWindowMove(IntPtr hWnd, int fromX, int fromY, int fromW, int fromH, int toX, int toY, int toW, int toH, int durationMs)
    {
        const int steps = 20;

        // 1ms timer resolution so Thread.Sleep(n) actually sleeps ~n ms instead of ~15 ms.
        _ = timeBeginPeriod(1);
        try
        {
            // SWP_ASYNCWINDOWPOS returns immediately even when the target app's UI thread
            // is busy, preventing the animation from blocking on the target's message loop.
            var flags = SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos;

            var sw = Stopwatch.StartNew();
            for (var i = 1; i <= steps; i++)
            {
                var t = EaseInOut((double)i / steps);

                // Last frame: synchronous (no SWP_ASYNCWINDOWPOS) so it's guaranteed to
                // be the final position processed — async frames can't arrive after it.
                var frameFlags = i == steps ? SwpNoZOrder | SwpNoActivate : flags;

                _ = SetWindowPos(hWnd, IntPtr.Zero,
                    fromX + (int)((toX - fromX) * t),
                    fromY + (int)((toY - fromY) * t),
                    fromW + (int)((toW - fromW) * t),
                    fromH + (int)((toH - fromH) * t),
                    frameFlags);

                var remaining = (int)(((long)durationMs * i / steps) - sw.ElapsedMilliseconds);
                if (remaining > 0)
                    Thread.Sleep(remaining);
            }
        }
        finally
        {
            _ = timeEndPeriod(1);
        }
    }
}
