using ScreenSwap.Windows;
using ScreenSwap.Windows.Utilities.BorderDrawing;
using ScreenSwap.Windows.Utilities.ScreenManager;
using ScreenSwap.Windows.Utilities.WindowManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ScreenSwap.Core;

[SupportedOSPlatform("windows6.1")]
public class ScreenSwapManager(Color highlightColor)
{
    private int cycleIndex = -1;
    private List<(ScreenInfo screen, WindowInfo window)> cycleTargets;
    private WindowInfo primaryHeroWindow;
    private ScreenInfo selectedScreen;
    private WindowInfo selectedTargetWindow;
    private Task activeSwap = Task.CompletedTask;

    public Color HighlightColor { get; } = highlightColor;
    public int AnimationDurationMs { get; set; } = 40;
    public string MainMonitorName { get; set; }

    public ScreenSwapManager(System.Windows.Media.Color highlightColor)
        : this(Color.FromArgb(highlightColor.A, highlightColor.R, highlightColor.G, highlightColor.B)) { }

    public void SwapAllWindows() => QueueSwap(SwapMode.AllWindows);
    public void SwapTopWindow() => QueueSwap(SwapMode.TopWindowOnly);

    public void MoveNextAllWindows() => MoveNext(SwapMode.AllWindows);

    public void MoveNextTopWindow()
    {
        if (cycleIndex == -1)
            BuildCycleTargets();

        MoveNext(SwapMode.TopWindowOnly);
    }

    public void Cancel()
    {
        BorderDrawer.ClearScreen();
        ResetCycle();
    }

    // -------------------------------------------------------------------------
    // Cycle state
    // -------------------------------------------------------------------------

    private void BuildCycleTargets()
    {
        var screens = ScreenManager.GetScreens().ToList();
        var primaryName = PrimaryName(screens);
        var primaryScreen = screens.FirstOrDefault(s => s.Name == primaryName);

        var nonPrimaryScreens = screens.Where(s => s.Name != primaryName)
                                       .OrderBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top)
                                       .ToList();

        var windows = WindowManager.GetWindowsOnCurrentDesktop();
        var windowMap = MapWindowsToScreens(windows, screens);

        // Capture the primary screen's top window so we can highlight it alongside the target.
        primaryHeroWindow = primaryScreen is not null && windowMap.TryGetValue(primaryScreen, out var pw) && pw.Count > 0
            ? pw[0] : null;

        cycleTargets = [.. nonPrimaryScreens.SelectMany(screen =>
        {
            var sw = windowMap.GetValueOrDefault(screen, []);
            return sw.Where((w, i) => !WindowManager.IsCompletelyOccluded(w, sw.Take(i)))
                     .Select(w => (screen, w));
        })];
    }

    private void ResetCycle()
    {
        selectedScreen = null;
        selectedTargetWindow = null;
        primaryHeroWindow = null;
        cycleIndex = -1;
        cycleTargets = null;
    }

    private void MoveNext(SwapMode mode)
    {
        if (mode == SwapMode.TopWindowOnly)
            MoveNextWindow();
        else
            MoveNextScreen();
    }

    private void MoveNextWindow()
    {
        if (cycleTargets is null || cycleTargets.Count == 0) return;

        cycleIndex = (cycleIndex + 1) % cycleTargets.Count;

        var (screen, window) = cycleTargets[cycleIndex];
        selectedScreen = screen;
        selectedTargetWindow = window;

        Debug.WriteLine($"Cycle [{cycleIndex}]: screen={screen.Name}, window={window.Title}");

        HighlightTopWindows(window, HighlightColor);
    }

    private void MoveNextScreen()
    {
        var screens = GetNonPrimaryScreens();

        if (screens.Count == 0) return;

        cycleIndex = (cycleIndex + 1) % screens.Count;
        selectedScreen = screens[cycleIndex];

        Debug.WriteLine($"Selecting screen: {selectedScreen.Name}");

        HighlightScreen(selectedScreen, HighlightColor);
    }

    // -------------------------------------------------------------------------
    // Swap execution
    // -------------------------------------------------------------------------

    private void QueueSwap(SwapMode mode)
    {
        if (selectedScreen is null) return;

        var screens = ScreenManager.GetScreens().ToList();
        var targetScreen = screens.FirstOrDefault(s => s.Name == selectedScreen.Name);
        var primaryScreen = screens.FirstOrDefault(s => s.Name == PrimaryName(screens))
                         ?? screens.FirstOrDefault(s => s.IsPrimary);

        var capturedTargetWindow = selectedTargetWindow;
        ResetCycle();

        if (targetScreen is null || primaryScreen is null) return;

        var windows = WindowManager.GetWindowsOnCurrentDesktop();
        var windowMap = MapWindowsToScreens(windows, screens);
        var primaryWin = windowMap.GetValueOrDefault(primaryScreen, []);
        var targetWin = windowMap.GetValueOrDefault(targetScreen, []);

        if (mode == SwapMode.TopWindowOnly && capturedTargetWindow is not null)
            targetWin = ReorderToFront(targetWin, capturedTargetWindow.WindowHandle);

        var animMs = AnimationDurationMs;
        var prev = activeSwap;

        activeSwap = Task.Run(() =>
        {
            prev.Wait();
            BorderDrawer.ClearScreen();
            if (mode == SwapMode.AllWindows)
                ExecuteScreenSwap(primaryWin, targetWin, primaryScreen, targetScreen, animMs);
            else
                ExecuteWindowSwap(primaryWin, targetWin, animMs);
        });
    }

    // Screen swap — proven 3-phase pattern from old version:
    // Phase 1: hide non-heroes, Phase 2: animate heroes, Phase 3: reveal non-heroes
    private static void ExecuteScreenSwap(List<WindowInfo> primaryWin, List<WindowInfo> targetWin,
                                          ScreenInfo primaryScreen, ScreenInfo targetScreen, int animMs)
    {
        var heroPrimary = primaryWin.Count > 0 ? primaryWin[0] : null;
        var heroTarget = targetWin.Count > 0 ? targetWin[0] : null;
        var nonHeroPrimary = primaryWin.Skip(1).ToList();
        var nonHeroTarget = targetWin.Skip(1).ToList();

        // Phase 1: Hide non-hero non-minimized windows so they don't flash during animation.
        _ = Parallel.ForEach(nonHeroPrimary.Concat(nonHeroTarget).Where(w => !w.IsMinimized),
                             w => WindowManager.HideWindow(w.WindowHandle));

        // Phase 2: Animate both heroes simultaneously.
        var heroTasks = new List<Task>(2);

        if (heroTarget is not null)
            heroTasks.Add(Task.Run(() => WindowManager.MoveWindowToScreen(heroTarget, targetScreen, primaryScreen, animate: true, animMs)));

        if (heroPrimary is not null)
            heroTasks.Add(Task.Run(() => WindowManager.MoveWindowToScreen(heroPrimary, primaryScreen, targetScreen, animate: true, animMs)));

        Task.WaitAll([.. heroTasks]);

        // Phase 3: Reveal non-heroes at their new positions atomically.
        var nonHeroMoves = nonHeroPrimary.Select(w => (window: w, source: primaryScreen, dest: targetScreen))
                                         .Concat(nonHeroTarget.Select(w => (window: w, source: targetScreen, dest: primaryScreen)))
                                         .ToList();

        WindowManager.RevealWindowsMoved(nonHeroMoves);
        FocusArriving(heroTarget);
    }

    // Window swap — each window takes the other's exact bounds and state.
    private static void ExecuteWindowSwap(List<WindowInfo> primaryWin, List<WindowInfo> targetWin, int animMs)
    {
        var heroPrimary = primaryWin.Count > 0 ? primaryWin[0] : null;
        var heroTarget = targetWin.Count > 0 ? targetWin[0] : null;

        if (heroPrimary is null || heroTarget is null) return;

        WindowManager.SwapWindows(heroPrimary, heroTarget, animate: true, animMs);
        FocusArriving(heroTarget);
    }

    private static void FocusArriving(WindowInfo hero)
    {
        if (hero is { IsMinimized: false })
            WindowManager.FocusWindow(hero.WindowHandle);
    }

    private static List<WindowInfo> ReorderToFront(List<WindowInfo> list, IntPtr hwnd)
    {
        var matched = list.FirstOrDefault(w => w.WindowHandle == hwnd);
        return matched is null ? list : [matched, .. list.Where(w => w.WindowHandle != hwnd)];
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string PrimaryName(List<ScreenInfo> screens) =>
        !string.IsNullOrEmpty(MainMonitorName) ? MainMonitorName
        : screens.FirstOrDefault(s => s.IsPrimary)?.Name;

    private List<ScreenInfo> GetNonPrimaryScreens()
    {
        var screens = ScreenManager.GetScreens().ToList();
        return [.. screens.Where(s => s.Name != PrimaryName(screens))
                          .OrderBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top)];
    }

    private static Dictionary<ScreenInfo, List<WindowInfo>> MapWindowsToScreens(List<WindowInfo> windows, List<ScreenInfo> screens)
    {
        var map = screens.ToDictionary(s => s, _ => new List<WindowInfo>());
        foreach (var window in windows)
        {
            var screen = WindowManager.GetScreenForWindow(screens, window);
            if (screen is not null) map[screen].Add(window);
        }

        return map;
    }

    public static void HighlightScreen(ScreenInfo screen, Color color, string overlay = null) =>
        BorderDrawer.SelectScreen([screen.Bounds], color, overlay);

    private void HighlightTopWindows(WindowInfo targetWindow, Color color)
    {
        var rects = new List<Rectangle>();

        var targetBounds = WindowManager.GetWindowBounds(targetWindow.WindowHandle);
        if (!targetBounds.IsEmpty) rects.Add(targetBounds);

        if (primaryHeroWindow is not null)
        {
            var primaryBounds = WindowManager.GetWindowBounds(primaryHeroWindow.WindowHandle);
            if (!primaryBounds.IsEmpty) rects.Add(primaryBounds);
        }

        if (rects.Count > 0) BorderDrawer.SelectScreen(rects, color);
    }

    private enum SwapMode { AllWindows, TopWindowOnly }
}
