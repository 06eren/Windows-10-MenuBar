using System.Text.Json.Serialization;

namespace Windows_10_MenuBar.Models;

public class BarSettings
{
    // ── Appearance ──
    public string Theme { get; set; } = "Dark";          // Dark / Midnight / BlueTint / Purple / Auto
    public string BarColor { get; set; } = "#1A1A1A";
    public double BarOpacity { get; set; } = 0.92;
    public double BarHeight { get; set; } = 32;
    public string FontSizePreset { get; set; } = "Medium"; // Small / Medium / Large
    public string CustomHexColor { get; set; } = "";

    // ── Behavior ──
    public bool AutoHide { get; set; } = false;
    public bool Use24HourClock { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    // ── Icon Visibility ──
    public bool ShowBluetooth { get; set; } = true;
    public bool ShowWifi { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowVpn { get; set; } = true;
    public bool ShowMicrophone { get; set; } = true;
    public bool ShowCamera { get; set; } = true;
    public bool ShowMedia { get; set; } = true;
    public bool ShowBrightness { get; set; } = true;
    public bool ShowTaskView { get; set; } = true;
    public bool ShowScreenshot { get; set; } = true;
}
