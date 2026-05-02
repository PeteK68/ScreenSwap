using Microsoft.Win32;
using ScreenSwap.Agent.HotKeys;
using ScreenSwap.Configuration;
using ScreenSwap.Configuration.Ipc;
using ScreenSwap.Core;
using ScreenSwap.Windows.Utilities.HotKeys;
using ScreenSwap.Windows.Utilities.ScreenManager;
using ScreenSwap.Windows.Utilities.WindowManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenSwap.Agent;

[SupportedOSPlatform("windows6.1")]
internal sealed class AgentApplicationContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly AgentHotKeyWindow hotKeyWindow = new();
    private readonly ScreenSwapManager screenSwapManager = new(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4));
    private readonly CancellationTokenSource ipcCancellation = new();
    private readonly SynchronizationContext uiContext;

    private Settings settings = Settings.CreateWithShortcutDefaults();
    private HotKeyAction allWindowsHotKey = new();
    private HotKeyAction topWindowOnlyHotKey = new();
    private bool hotKeysPaused;
    private bool hasExternalMonitor = true;
    private bool currentMonitorProfileKnown = true;

    public AgentApplicationContext()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        hotKeyWindow.EscapePressed += screenSwapManager.Cancel;

        trayIcon = new NotifyIcon
        {
            Text = "ScreenSwap",
            Icon = LoadIcon(enabled: true),
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        trayIcon.DoubleClick += (_, _) => LaunchSettingsUi();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        ReloadSettings();
        _ = Task.Run(() => RunIpcServerAsync(ipcCancellation.Token));
        _ = Task.Run(WarmUpVirtualDesktopManager);
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        _ = menu.Items.Add("Settings", null, (_, _) => LaunchSettingsUi());
        _ = menu.Items.Add("Exit", null, (_, _) => ExitAgent());
        return menu;
    }

    private static void WarmUpVirtualDesktopManager()
    {
        try { _ = WindowManager.GetWindowsOnCurrentDesktop(); }
        catch { }
    }

    private void ReloadSettings()
    {
        settings = Settings.HasSavedState ? Settings.LoadCurrent() : Settings.CreateWithShortcutDefaults();
        ApplyShortcutDefaults(settings);

        screenSwapManager.AnimationDurationMs = settings.AnimationDurationMs;
        RefreshMonitorProfile();
        BindHotKeys();
        SyncHotKeys();
        UpdateTrayIcon();
    }

    private static void ApplyShortcutDefaults(Settings current)
    {
        if (current.Key == 0 || current.Modifiers == 0)
        {
            current.Key = Settings.CreateWithShortcutDefaults().Key;
            current.Modifiers = Settings.CreateWithShortcutDefaults().Modifiers;
        }

        if (current.TopWindowOnlyKey == 0 || current.TopWindowOnlyModifiers == 0)
        {
            current.TopWindowOnlyKey = Settings.CreateWithShortcutDefaults().TopWindowOnlyKey;
            current.TopWindowOnlyModifiers = Settings.CreateWithShortcutDefaults().TopWindowOnlyModifiers;
        }
    }

    private void BindHotKeys()
    {
        allWindowsHotKey = CreateHotKey(settings.Key, settings.Modifiers, screenSwapManager.MoveNextAllWindows, screenSwapManager.SwapAllWindows);
        topWindowOnlyHotKey = CreateHotKey(settings.TopWindowOnlyKey, settings.TopWindowOnlyModifiers, screenSwapManager.MoveNextTopWindow, screenSwapManager.SwapTopWindow);
    }

    private HotKeyAction CreateHotKey(int key, int modifiers, Action moveNext, Action swapAction)
    {
        var hotKey = new HotKeyAction
        {
            Key = (VirtualKey)key,
            Modifiers = (System.Windows.Input.ModifierKeys)modifiers,
            OnHotKeyActivated = _ => OnHotKeyActivated(moveNext),
            OnHotKeyRepeated = _ => OnHotKeyActivated(moveNext),
            OnHotKeyTerminated = _ => OnHotKeyTerminated(swapAction)
        };
        return hotKey;
    }

    private void OnHotKeyActivated(Action moveNext)
    {
        if (!hotKeysPaused && CanUseScreenSwap(showConfigurationPrompt: true))
            moveNext();
    }

    private void OnHotKeyTerminated(Action swapAction)
    {
        if (!hotKeysPaused && CanUseScreenSwap(showConfigurationPrompt: false))
            swapAction();
    }

    private void SyncHotKeys()
    {
        hotKeyWindow.UnregisterAll();

        if (hotKeysPaused || !hasExternalMonitor)
            return;

        if (allWindowsHotKey.IsValid)
            _ = hotKeyWindow.Register(allWindowsHotKey);

        if (topWindowOnlyHotKey.IsValid)
            _ = hotKeyWindow.Register(topWindowOnlyHotKey);
    }

    private bool CanUseScreenSwap(bool showConfigurationPrompt)
    {
        if (!hasExternalMonitor)
            return false;

        if (currentMonitorProfileKnown)
            return true;

        if (showConfigurationPrompt)
            LaunchSettingsUi();

        return false;
    }

    private void RefreshMonitorProfile()
    {
        var screens = ScreenManager.GetScreensForPanel().ToList();
        hasExternalMonitor = screens.Count > 1;
        var profileKey = BuildMonitorProfileKey(screens);

        if (!hasExternalMonitor)
        {
            screenSwapManager.MainMonitorName = string.Empty;
            currentMonitorProfileKnown = false;
            return;
        }

        if (settings.MonitorProfiles.TryGetValue(profileKey, out var mainMonitor)
            && screens.Any(s => s.Name == mainMonitor))
        {
            screenSwapManager.MainMonitorName = mainMonitor;
            currentMonitorProfileKnown = true;
            return;
        }

        if (settings.MonitorProfiles.Count == 0
            && !string.IsNullOrEmpty(settings.MainMonitorName)
            && screens.Any(s => s.Name == settings.MainMonitorName))
        {
            settings.MonitorProfiles[profileKey] = settings.MainMonitorName;
            settings.Save();
            screenSwapManager.MainMonitorName = settings.MainMonitorName;
            currentMonitorProfileKnown = true;
            return;
        }

        screenSwapManager.MainMonitorName = string.Empty;
        currentMonitorProfileKnown = false;
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e)
    {
        RefreshMonitorProfile();
        SyncHotKeys();
        UpdateTrayIcon();
    }

    private void UpdateTrayIcon() => trayIcon.Icon = LoadIcon(hasExternalMonitor && currentMonitorProfileKnown && !hotKeysPaused);

    private static Icon LoadIcon(bool enabled)
    {
        var fileName = enabled ? "ScreenSwap-Enabled.ico" : "ScreenSwap-Disabled.ico";
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    private static string BuildMonitorProfileKey(IEnumerable<Windows.ScreenInfo> screens)
        => string.Join("|", screens
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => $"{s.Name}:{s.Bounds.Left},{s.Bounds.Top},{s.Bounds.Width}x{s.Bounds.Height}"));

    private static void LaunchSettingsUi()
    {
        var repoRoot = FindRepoRoot();
        var candidates = new[]
        {
            // Side-by-side (installed/published layout)
            Path.Combine(AppContext.BaseDirectory, "ScreenSwap.exe"),
            // Debug builds — check these before any artifacts
            repoRoot is null ? string.Empty : Path.Combine(repoRoot, "ScreenSwap", "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "ScreenSwap.exe"),
            repoRoot is null ? string.Empty : Path.Combine(repoRoot, "ScreenSwap", "bin", "Release", "net10.0-windows10.0.19041.0", "win-x64", "ScreenSwap.exe"),
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            return;

        _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScreenSwap.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    private async Task RunIpcServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(ScreenSwapIpc.PipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                if (Enum.TryParse<ScreenSwapIpcCommand>(text, out var command))
                    uiContext.Post(_ => HandleIpcCommand(command), null);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException) { }
        }
    }

    private void HandleIpcCommand(ScreenSwapIpcCommand command)
    {
        switch (command)
        {
            case ScreenSwapIpcCommand.PauseHotKeys:
                hotKeysPaused = true;
                screenSwapManager.Cancel();
                SyncHotKeys();
                UpdateTrayIcon();
                break;
            case ScreenSwapIpcCommand.ResumeHotKeys:
                hotKeysPaused = false;
                SyncHotKeys();
                UpdateTrayIcon();
                break;
            case ScreenSwapIpcCommand.ReloadSettings:
                hotKeysPaused = false;
                ReloadSettings();
                break;
            case ScreenSwapIpcCommand.Shutdown:
                ExitAgent();
                break;
        }
    }

    private void ExitAgent()
    {
        ipcCancellation.Cancel();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            if (!ipcCancellation.IsCancellationRequested)
                ipcCancellation.Cancel();
            ipcCancellation.Dispose();
            hotKeyWindow.Dispose();
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
