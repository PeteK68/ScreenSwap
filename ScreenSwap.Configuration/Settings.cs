using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScreenSwap.Configuration;

public sealed class Settings
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public static Settings Default { get; } = Load();

    public int Key { get; set; } = 0;
    public int Modifiers { get; set; } = 0;
    public int TopWindowOnlyKey { get; set; } = 0;
    public int TopWindowOnlyModifiers { get; set; } = 0;
    public int AnimationDurationMs { get; set; } = 40;
    public bool TopWindowOnly { get; set; } = false;
    public string MainMonitorName { get; set; } = string.Empty;
    public int ThemeMode { get; set; } = 0;
    public Dictionary<string, string> MonitorProfiles { get; set; } = [];

    public static Settings LoadCurrent() => Load();

    public static bool HasSavedState => File.Exists(SettingsFilePath);

    public static string SettingsFilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                           "ScreenSwap",
                                                           "settings.json");

    private static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFilePath)) ?? new Settings();
                settings.MonitorProfiles ??= [];
                return settings;
            }
        }
        catch { }

        return new Settings();
    }

    public void Save()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(this, jsonOptions));
    }

    public static Settings CreateWithShortcutDefaults()
    {
        var settings = new Settings
        {
            Key = 140,
            Modifiers = 8,
            TopWindowOnlyKey = 140,
            TopWindowOnlyModifiers = 12
        };

        return settings;
    }

    public void Reset()
    {
        Key = 0;
        Modifiers = 0;
        TopWindowOnlyKey = 0;
        TopWindowOnlyModifiers = 0;
        AnimationDurationMs = 40;
        TopWindowOnly = false;
        MainMonitorName = string.Empty;
        ThemeMode = 0;
        MonitorProfiles.Clear();
        Save();
    }
}
