using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Windows_10_MenuBar.Models;

public partial class BarSettings : ObservableObject
{
    // ── Appearance ──
    [ObservableProperty] private string _theme = "Dark";          // Dark / Midnight / BlueTint / Purple / Auto
    [ObservableProperty] private string _barColor = "#1A1A1A";
    [ObservableProperty] private double _barOpacity = 0.92;
    [ObservableProperty] private double _barHeight = 32;
    [ObservableProperty] private string _fontSizePreset = "Medium"; // Small / Medium / Large
    [ObservableProperty] private string _customHexColor = "";

    // ── Behavior ──
    [ObservableProperty] private bool _autoHide = false;
    [ObservableProperty] private bool _hideOnFullscreen = true;
    [ObservableProperty] private bool _use24HourClock = true;
    [ObservableProperty] private bool _startWithWindows = false;

    // ── Icon Visibility ──
    [ObservableProperty] private bool _showBluetooth = true;
    [ObservableProperty] private double _weatherOpacity = 0.1;
    [ObservableProperty] private string _weatherProvince = string.Empty;
    [ObservableProperty] private string _weatherDistrict = string.Empty;
    [ObservableProperty] private bool _showWifi = true;
    [ObservableProperty] private bool _showBattery = true;
    [ObservableProperty] private bool _showVpn = true;
    [ObservableProperty] private bool _showMicrophone = true;
    [ObservableProperty] private bool _showCamera = true;
    [ObservableProperty] private bool _showMedia = true;
    [ObservableProperty] private bool _showBrightness = true;
    [ObservableProperty] private bool _showTaskView = true;
    [ObservableProperty] private bool _showScreenshot = true;
}
