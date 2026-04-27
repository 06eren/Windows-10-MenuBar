using System;
using System.IO;
using System.Text.Json;
using Windows_10_MenuBar.Models;

namespace Windows_10_MenuBar.Services;

public static class SettingsService
{
    private static readonly string _settingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Windows10MenuBar");
    private static readonly string _settingsFile;

    public static BarSettings Current { get; private set; } = new();

    static SettingsService()
    {
        _settingsFile = Path.Combine(_settingsDir, "settings.json");
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                Current = JsonSerializer.Deserialize<BarSettings>(json) ?? new BarSettings();
            }
        }
        catch { Current = new BarSettings(); }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    // ── Predefined Themes ──
    public static readonly (string Name, string Color, double Opacity)[] Themes =
    {
        ("Dark",       "#1A1A1A", 0.92),
        ("Midnight",   "#0D0D1A", 0.96),
        ("Blue Tint",  "#0D1A2A", 0.90),
        ("Purple",     "#1A0D2A", 0.90),
        ("Forest",     "#0D1A0D", 0.90),
        ("Glass",      "#000000", 0.55),
    };
}
