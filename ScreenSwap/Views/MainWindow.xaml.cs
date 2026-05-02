using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenSwap.Configuration;
using ScreenSwap.Configuration.Ipc;
using ScreenSwap.Views;
using ScreenSwap.Windows.Utilities.HotKeys;
using ScreenSwap.Windows.Utilities.ScreenManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Graphics;
using HotKeyVirtualKey = ScreenSwap.Windows.Utilities.HotKeys.VirtualKey;
using VirtualKey = Windows.System.VirtualKey;
using WinRT.Interop;
using static ScreenSwap.Views.ShortcutSettingRow;

namespace ScreenSwap;

[SupportedOSPlatform("windows10.0.19041.0")]
public sealed partial class MainWindow : Window
{
    private const int InitialWindowWidth = 600;
    private const int InitialWindowHeight = 900;
    private const int MinimumWindowWidth = InitialWindowWidth;
    private const int MinimumWindowHeight = InitialWindowHeight;
    private const int WmGetMinMaxInfo = 0x0024;
    private static readonly UIntPtr windowSubclassId = new(1);

    private readonly Settings settings = Settings.HasSavedState ? Settings.LoadCurrent() : Settings.CreateWithShortcutDefaults();
    private readonly MonitorPicker monitorPicker = new();
    private readonly ShortcutSettingRow allWindowsShortcutRow = new();
    private readonly ShortcutSettingRow topWindowShortcutRow = new();
    private readonly SubclassProc windowSubclassProc;
    private IntPtr windowHandle;
    private string mainMonitorName = string.Empty;
    private string currentMonitorProfileKey = string.Empty;
    private bool hasExternalMonitor = true;
    private bool currentMonitorProfileKnown = true;
    private readonly DispatcherTimer animationDurationReloadTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly bool initialized;

    public MainWindow()
    {
        windowSubclassProc = WindowSubclassProc;
        InitializeComponent();
        Title = "ScreenSwap";
        InitializeCustomControls();
        ConfigureWindowBounds();
        Closed += OnClosed;
        animationDurationReloadTimer.Tick += AnimationDurationReloadTimer_Tick;
        LoadSettings();
        initialized = true;
    }

    private void InitializeCustomControls()
    {
        monitorPicker.MonitorSelected += MonitorPicker_MonitorSelected;
        MonitorPickerHost.Children.Add(monitorPicker);

        ConfigureShortcutRow(allWindowsShortcutRow, "All windows shortcut", "Move all windows between displays", ShortcutKind.Monitor, AllWindowsShortcut_EditRequested);
        ConfigureShortcutRow(topWindowShortcutRow, "Top window only shortcut", "Move only the foreground window", ShortcutKind.Window, TopWindowShortcut_EditRequested);
        ShortcutRowsHost.Children.Add(allWindowsShortcutRow);
        ShortcutRowsHost.Children.Add(topWindowShortcutRow);
    }

    private static void ConfigureShortcutRow(ShortcutSettingRow row, string title, string desc, ShortcutKind iconKind, EventHandler editRequested)
    {
        row.Title = title;
        row.Description = desc;
        row.IconKind = iconKind;
        row.EditRequested += editRequested;
    }

    private void ConfigureWindowBounds()
    {
        windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.ResizeClient(new SizeInt32(InitialWindowWidth, InitialWindowHeight));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Resources", "ScreenSwap-Enabled.ico"));
        _ = SetWindowSubclass(windowHandle, windowSubclassProc, windowSubclassId, UIntPtr.Zero);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        animationDurationReloadTimer.Stop();
        if (windowHandle != IntPtr.Zero)
            _ = RemoveWindowSubclass(windowHandle, windowSubclassProc, windowSubclassId);
    }

    private IntPtr WindowSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData)
    {
        if (message == WmGetMinMaxInfo)
        {
            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            minMaxInfo.MinTrackSize.X = MinimumWindowWidth;
            minMaxInfo.MinTrackSize.Y = MinimumWindowHeight;
            Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: false);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void LoadSettings()
    {
        AnimationDurationSlider.Value = settings.AnimationDurationMs;
        AnimationDurationValue.Text = $"{settings.AnimationDurationMs} ms";
        ThemeComboBox.SelectedIndex = settings.ThemeMode;
        RefreshMonitorProfile();
        RefreshMonitorPicker();
        RenderShortcutRows();
        UpdateMonitorWarning();
    }

    private void SaveSettings(bool reloadAgent)
    {
        settings.AnimationDurationMs = (int)Math.Round(AnimationDurationSlider.Value / 5d) * 5;
        settings.ThemeMode = ThemeComboBox.SelectedIndex < 0 ? 0 : ThemeComboBox.SelectedIndex;

        if (hasExternalMonitor && !string.IsNullOrEmpty(currentMonitorProfileKey) && !string.IsNullOrEmpty(mainMonitorName))
        {
            settings.MonitorProfiles[currentMonitorProfileKey] = mainMonitorName;
            settings.MainMonitorName = mainMonitorName;
            currentMonitorProfileKnown = true;
        }

        settings.Save();

        if (reloadAgent)
            _ = ScreenSwapIpc.SendCommandAsync(ScreenSwapIpcCommand.ReloadSettings);
    }

    private void RefreshMonitorProfile()
    {
        var screens = ScreenManager.GetScreensForPanel().ToList();
        hasExternalMonitor = screens.Count > 1;
        currentMonitorProfileKey = BuildMonitorProfileKey(screens);

        if (!hasExternalMonitor)
        {
            mainMonitorName = string.Empty;
            currentMonitorProfileKnown = false;
            return;
        }

        if (settings.MonitorProfiles.TryGetValue(currentMonitorProfileKey, out var profileMainMonitor)
            && screens.Any(s => s.Name == profileMainMonitor))
        {
            mainMonitorName = profileMainMonitor;
            currentMonitorProfileKnown = true;
            return;
        }

        if (settings.MonitorProfiles.Count == 0
            && !string.IsNullOrEmpty(settings.MainMonitorName)
            && screens.Any(s => s.Name == settings.MainMonitorName))
        {
            mainMonitorName = settings.MainMonitorName;
            settings.MonitorProfiles[currentMonitorProfileKey] = mainMonitorName;
            settings.Save();
            currentMonitorProfileKnown = true;
            return;
        }

        mainMonitorName = string.Empty;
        currentMonitorProfileKnown = false;
    }

    private void RefreshMonitorPicker()
    {
        monitorPicker.MainMonitorName = mainMonitorName;
        monitorPicker.Refresh();
    }

    private void UpdateMonitorWarning()
    {
        MonitorWarning.Visibility = !hasExternalMonitor || !currentMonitorProfileKnown
            ? Visibility.Visible
            : Visibility.Collapsed;
        MonitorWarningMessage.Text = !hasExternalMonitor
            ? "Connect a second display to use ScreenSwap."
            : "Monitor configuration changed; click a display to set the main source.";
    }

    private void RenderShortcutRows()
    {
        allWindowsShortcutRow.SetShortcut((VirtualKey)settings.Key, (ModifierKeys)settings.Modifiers);
        topWindowShortcutRow.SetShortcut((VirtualKey)settings.TopWindowOnlyKey, (ModifierKeys)settings.TopWindowOnlyModifiers);
    }

    private void MonitorPicker_MonitorSelected(object sender, string deviceName)
    {
        mainMonitorName = deviceName;
        currentMonitorProfileKnown = true;
        RefreshMonitorPicker();
        UpdateMonitorWarning();
        SaveSettings(reloadAgent: true);
    }

    private async void AllWindowsShortcut_EditRequested(object sender, EventArgs e)
    {
        var updated = await ShowShortcutDialogAsync("All windows shortcut", settings.Key, settings.Modifiers, settings.TopWindowOnlyKey, settings.TopWindowOnlyModifiers);

        if (updated is null)
            return;

        settings.Key = (int)updated.Key;
        settings.Modifiers = (int)updated.Modifiers;
        SaveSettings(reloadAgent: true);
        RenderShortcutRows();
    }

    private async void TopWindowShortcut_EditRequested(object sender, EventArgs e)
    {
        var updated = await ShowShortcutDialogAsync("Top window only shortcut", settings.TopWindowOnlyKey, settings.TopWindowOnlyModifiers, settings.Key, settings.Modifiers);

        if (updated is null)
            return;

        settings.TopWindowOnlyKey = (int)updated.Key;
        settings.TopWindowOnlyModifiers = (int)updated.Modifiers;
        SaveSettings(reloadAgent: true);
        RenderShortcutRows();
    }

    private async Task<HotKey> ShowShortcutDialogAsync(string title, int key, int modifiers, int conflictingKey, int conflictingModifiers)
    {
        await ScreenSwapIpc.SendCommandAsync(ScreenSwapIpcCommand.PauseHotKeys);
        var dialog = new ShortcutCaptureWindow(
            title,
            new HotKey { Key = (HotKeyVirtualKey)key, Modifiers = (ModifierKeys)modifiers },
            new HotKey { Key = (HotKeyVirtualKey)conflictingKey, Modifiers = (ModifierKeys)conflictingModifiers });
        var result = await dialog.ShowAsync(Root.XamlRoot, windowHandle);
        await ScreenSwapIpc.SendCommandAsync(result is null ? ScreenSwapIpcCommand.ResumeHotKeys : ScreenSwapIpcCommand.ReloadSettings);

        return result;
    }

    private void AnimationDurationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!initialized)
            return;

        var value = (int)Math.Round(e.NewValue / 5d) * 5;
        AnimationDurationSlider.Value = value;
        AnimationDurationValue.Text = $"{value} ms";
        SaveSettings(reloadAgent: false);
        ScheduleAnimationDurationReload();
    }

    private void ScheduleAnimationDurationReload()
    {
        animationDurationReloadTimer.Stop();
        animationDurationReloadTimer.Start();
    }

    private void AnimationDurationReloadTimer_Tick(object sender, object e)
    {
        animationDurationReloadTimer.Stop();
        _ = ScreenSwapIpc.SendCommandAsync(ScreenSwapIpcCommand.ReloadSettings);
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized)
            return;

        ((App)Application.Current).ApplyTheme(ThemeComboBox.SelectedIndex);
        SaveSettings(reloadAgent: true);
    }

    private void LearnMore_Click(object sender, RoutedEventArgs e) => _ = Process.Start(new ProcessStartInfo("https://github.com/petek68/ScreenSwap") { UseShellExecute = true });

    private static string BuildMonitorProfileKey(IEnumerable<Windows.ScreenInfo> screens)
        => string.Join("|", screens
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => $"{s.Name}:{s.Bounds.Left},{s.Bounds.Top},{s.Bounds.Width}x{s.Bounds.Height}"));

    private static bool AreSameAssignedShortcut(HotKey candidate, int existingKey, int existingModifiers)
        => candidate.IsValid
           && (HotKeyVirtualKey)existingKey == candidate.Key
           && (ModifierKeys)existingModifiers == candidate.Modifiers;

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [LibraryImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, UIntPtr subclassId, UIntPtr refData);

    [LibraryImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, UIntPtr subclassId);

    [LibraryImport("comctl32.dll")]
    private static partial IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
