using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Windows_10_MenuBar.Models;

public partial class WifiInfo : ObservableObject
{
    [ObservableProperty] private string _ssid = string.Empty;
    [ObservableProperty] private byte _signalStrength;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isPasswordPromptVisible;
}

public partial class BluetoothDeviceInfo : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = string.Empty;
    
    // Reference to the actual Windows DeviceInformation for Pairing
    public Windows.Devices.Enumeration.DeviceInformation? DeviceInfo { get; set; }
}

public class MediaInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public bool IsPlaying { get; set; }
}

public partial class BatteryInfo : ObservableObject
{
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private int _level = 100;
    [ObservableProperty] private bool _isCharging;
    [ObservableProperty] private Wpf.Ui.Common.SymbolRegular _icon = Wpf.Ui.Common.SymbolRegular.Battery1024;
}
