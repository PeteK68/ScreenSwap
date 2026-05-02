using Microsoft.UI.Xaml;
using ScreenSwap.Configuration;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;

namespace ScreenSwap;

[SupportedOSPlatform("windows10.0.19041.0")]
public partial class App : Application
{
    private Window window;
    private Mutex singleInstanceMutex;
    private bool ownsSingleInstanceMutex;

    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("Settings UI launch requested.");
        singleInstanceMutex = new Mutex(initiallyOwned: true, "ScreenSwap.SettingsUi", out ownsSingleInstanceMutex);

        if (!ownsSingleInstanceMutex)
        {
            Log("Settings UI exiting because another instance owns the mutex.");
            Exit();
            return;
        }

        window = new MainWindow();
        ApplyTheme(Settings.HasSavedState ? Settings.LoadCurrent().ThemeMode : Settings.CreateWithShortcutDefaults().ThemeMode);
        window.Closed += OnWindowClosed;
        window.Activate();
        Log("Settings UI window activated.");
    }

    internal void ApplyTheme(int themeMode)
    {
        if (window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = themeMode switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Log("Settings UI window closed.");
        if (ownsSingleInstanceMutex)
            singleInstanceMutex?.ReleaseMutex();

        singleInstanceMutex?.Dispose();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) => Log(e.Exception.ToString());

    private static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScreenSwap");
            _ = Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "settings-ui.log"),
                $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never affect app startup.
        }
    }
}
