using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows_10_MenuBar.Models;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;
using Windows.Devices.WiFi;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Media.Control;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Management;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Windows_10_MenuBar.ViewModels;

public partial class BarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentTime = "";

    [ObservableProperty]
    private string _currentDate = "";

    [ObservableProperty]
    private string _activeWindowTitle = "Windows";

    [ObservableProperty]
    private MediaInfo _currentMedia = new();

    [ObservableProperty]
    private bool _isWifiConnected;

    [ObservableProperty]
    private Wpf.Ui.Common.SymbolRegular _currentNetworkIcon = Wpf.Ui.Common.SymbolRegular.WifiOff24;

    [ObservableProperty]
    private bool _isBluetoothOn;

    public ObservableCollection<WifiInfo> AvailableNetworks { get; } = new();
    public ObservableCollection<BluetoothDeviceInfo> BluetoothDevices { get; } = new();

    [ObservableProperty]
    private BatteryInfo _battery = new();

    [ObservableProperty]
    private bool _isMicrophoneActive;

    [ObservableProperty]
    private bool _isCameraActive;

    [ObservableProperty]
    private bool _isVpnConnected;

    [ObservableProperty]
    private string _vpnName = string.Empty;

    // ── Brightness ──
    [ObservableProperty]
    private int _brightnessLevel = 100;

    [ObservableProperty]
    private bool _hasBrightnessControl;

    // ── Settings ──
    [ObservableProperty]
    private BarSettings _settings = SettingsService.Current;

    [ObservableProperty]
    private string _barBackground = SettingsService.Current.BarColor;

    [ObservableProperty]
    private double _barOpacity = SettingsService.Current.BarOpacity;

    [ObservableProperty]
    private string _foregroundColor = "#FFFFFF"; // auto from wallpaper

    private DispatcherTimer _clockTimer;

    public BarViewModel()
    {
        // 1s Clock Loop
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateTime();
        _clockTimer.Start();
        UpdateTime();

        // Start hardware background tasks
        _ = UpdateWifiLoopAsync();
        InitBluetoothWatcher();
        _ = InitMediaControlsAsync();
        _ = UpdateSystemStatusLoopAsync();

        // Brightness + Wallpaper
        InitBrightness();
        _ = DetectWallpaperColorAsync();
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        CurrentTime = Settings.Use24HourClock ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
        CurrentDate = now.ToString("dd MMM ddd");
    }

    // ────────────────────────────── Brightness ──────────────────────────────

    private void InitBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                BrightnessLevel = Convert.ToInt32(obj["CurrentBrightness"]);
                HasBrightnessControl = true;
                break;
            }
        }
        catch { HasBrightnessControl = false; }
    }

    [RelayCommand]
    private void SetBrightness(int level)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)level });
                BrightnessLevel = level;
                break;
            }
        }
        catch { }
    }

    // ────────────────────────────── Wallpaper Color Detection ───────────────

    private async Task DetectWallpaperColorAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                string? wallPath = key?.GetValue("Wallpaper")?.ToString();
                if (string.IsNullOrEmpty(wallPath) || !System.IO.File.Exists(wallPath)) return;

                var uri = new Uri(wallPath);
                App.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bmp = new BitmapImage(uri);
                        // Sample center region average brightness
                        var pixels = new byte[4];
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));

                        double avgBrightness = SampleWallpaperBrightness(wallPath);
                        // Dark wallpaper (< 0.4) → white icons/text
                        // Light wallpaper (> 0.6) → dark bar + dark icons
                        if (Settings.Theme == "Auto")
                        {
                            if (avgBrightness > 0.55)
                            {
                                BarBackground = "#E8E8E8";
                                ForegroundColor = "#1A1A1A";
                                BarOpacity = 0.88;
                            }
                            else
                            {
                                BarBackground = "#1A1A1A";
                                ForegroundColor = "#FFFFFF";
                                BarOpacity = 0.92;
                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        });
    }

    private static double SampleWallpaperBrightness(string path)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream,
                BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            int w = Math.Min(frame.PixelWidth, 100);
            int h = Math.Min(frame.PixelHeight, 100);
            var scaled = new TransformedBitmap(frame, new ScaleTransform(
                (double)w / frame.PixelWidth, (double)h / frame.PixelHeight));

            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            scaled.CopyPixels(pixels, stride, 0);

            double totalBrightness = 0;
            int count = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double r = pixels[i + 2] / 255.0;
                double g = pixels[i + 1] / 255.0;
                double b = pixels[i + 0] / 255.0;
                totalBrightness += 0.299 * r + 0.587 * g + 0.114 * b;
                count++;
            }
            return count > 0 ? totalBrightness / count : 0.5;
        }
        catch { return 0.5; }
    }

    // ────────────────────────────── Settings Commands ───────────────────────

    [RelayCommand]
    private void ApplyTheme(string themeName)
    {
        if (themeName == "Auto")
        {
            Settings.Theme = "Auto";
            _ = DetectWallpaperColorAsync();
        }
        else if (themeName == "Custom" && !string.IsNullOrWhiteSpace(Settings.CustomHexColor))
        {
            Settings.Theme = "Custom";
            BarBackground = Settings.CustomHexColor;
            ForegroundColor = "#FFFFFF";
        }
        else
        {
            var theme = SettingsService.Themes.FirstOrDefault(t => t.Name == themeName);
            if (theme != default)
            {
                Settings.Theme = themeName;
                BarBackground = theme.Color;
                BarOpacity = theme.Opacity;
                ForegroundColor = "#FFFFFF";
            }
        }
        SaveSettings();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        Settings.BarColor = BarBackground;
        Settings.BarOpacity = BarOpacity;
        SettingsService.Save();
    }

    [RelayCommand]
    private void ApplyStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
                key?.SetValue("Windows10MenuBar", $"\"{exe}\"");
            }
            else
            {
                key?.DeleteValue("Windows10MenuBar", throwOnMissingValue: false);
            }
            Settings.StartWithWindows = enable;
            SaveSettings();
        }
        catch { }
    }

    private async Task UpdateSystemStatusLoopAsync()
    {
        while (true)
        {
            try
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    // ── Battery ──
                    var battReport = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
                    bool hasBat = battReport.Status != Windows.System.Power.BatteryStatus.NotPresent
                               && battReport.FullChargeCapacityInMilliwattHours != null;
                    Battery.HasBattery = hasBat;
                    if (hasBat)
                    {
                        float pct = (float)battReport.RemainingCapacityInMilliwattHours!.Value
                                  / (float)battReport.FullChargeCapacityInMilliwattHours!.Value;
                        Battery.Level = (int)(pct * 100);
                        Battery.IsCharging = battReport.Status == Windows.System.Power.BatteryStatus.Charging
                                          || battReport.Status == Windows.System.Power.BatteryStatus.Idle;
                        Battery.Icon = Battery.IsCharging ? Wpf.Ui.Common.SymbolRegular.BatteryCharge24
                                     : Battery.Level > 80 ? Wpf.Ui.Common.SymbolRegular.Battery1024
                                     : Battery.Level > 60 ? Wpf.Ui.Common.SymbolRegular.Battery824
                                     : Battery.Level > 40 ? Wpf.Ui.Common.SymbolRegular.Battery624
                                     : Battery.Level > 20 ? Wpf.Ui.Common.SymbolRegular.Battery424
                                     : Wpf.Ui.Common.SymbolRegular.Battery224;
                    }

                    // ── Microphone (registry) ──
                    bool micActive = false;
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged");
                        if (key != null)
                        {
                            foreach (var sub in key.GetSubKeyNames())
                            {
                                using var subKey = key.OpenSubKey(sub);
                                var lastUsed = subKey?.GetValue("LastUsedTimeStop");
                                if (lastUsed is long ticks && ticks == 0) { micActive = true; break; }
                            }
                        }
                    }
                    catch { }
                    IsMicrophoneActive = micActive;

                    // ── Camera (registry) ──
                    bool camActive = false;
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\NonPackaged");
                        if (key != null)
                        {
                            foreach (var sub in key.GetSubKeyNames())
                            {
                                using var subKey = key.OpenSubKey(sub);
                                var lastUsed = subKey?.GetValue("LastUsedTimeStop");
                                if (lastUsed is long ticks && ticks == 0) { camActive = true; break; }
                            }
                        }
                    }
                    catch { }
                    IsCameraActive = camActive;

                    // ── VPN ──
                    bool vpnFound = false;
                    string vpnName = string.Empty;
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if ((ni.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                             ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
                             ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase) ||
                             ni.Description.Contains("TUN", StringComparison.OrdinalIgnoreCase) ||
                             ni.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)) &&
                            ni.OperationalStatus == OperationalStatus.Up)
                        {
                            vpnFound = true;
                            vpnName = ni.Name;
                            break;
                        }
                    }
                    IsVpnConnected = vpnFound;
                    VpnName = vpnName;
                });
            }
            catch { }

            await Task.Delay(3000);
        }
    }

    [RelayCommand]
    private void OpenTaskView()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenVirtualDesktops()
    {
        NativeMethods.SimulateWinTab();
    }

    [RelayCommand]
    private void TakeScreenshot()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "snippingtool",
                Arguments = "/clip",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private async Task UpdateWifiLoopAsync()
    {
        try
        {
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed) return;

            while (true)
            {
                var adapters = await WiFiAdapter.FindAllAdaptersAsync();
                if (adapters.Count == 0)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        IsWifiConnected = false;
                        AvailableNetworks.Clear();
                        bool hasNetwork = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                        CurrentNetworkIcon = hasNetwork ? Wpf.Ui.Common.SymbolRegular.Desktop24 : Wpf.Ui.Common.SymbolRegular.Globe24;
                    });
                    await Task.Delay(5000);
                    continue;
                }

                var adapter = adapters[0];
                try
                {
                    await adapter.ScanAsync();
                    var networks = adapter.NetworkReport.AvailableNetworks;
                    var connectedProfile = await adapter.NetworkAdapter.GetConnectedProfileAsync();
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        IsWifiConnected = connectedProfile != null;

                        // Update Icon dynamically based on signal strength
                        bool hasNetwork = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                        if (!hasNetwork)
                        {
                            CurrentNetworkIcon = Wpf.Ui.Common.SymbolRegular.WifiOff24;
                        }
                        else if (IsWifiConnected)
                        {
                            var connNet = networks.FirstOrDefault(n => n.Ssid == connectedProfile?.ProfileName);
                            int bars = connNet?.SignalBars ?? 4;
                            CurrentNetworkIcon = bars >= 4 ? Wpf.Ui.Common.SymbolRegular.Wifi424
                                              : bars == 3 ? Wpf.Ui.Common.SymbolRegular.Wifi324
                                              : bars == 2 ? Wpf.Ui.Common.SymbolRegular.Wifi224
                                              : Wpf.Ui.Common.SymbolRegular.Wifi124;
                        }
                        else
                        {
                            CurrentNetworkIcon = Wpf.Ui.Common.SymbolRegular.WifiOff24;
                        }

                        var incoming = networks.OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts).ToList();
                        
                        // Remove networks no longer available
                        var toRemove = AvailableNetworks.Where(n => !incoming.Any(i => i.Ssid == n.Ssid)).ToList();
                        foreach (var net in toRemove) AvailableNetworks.Remove(net);

                        // Update or Add
                        foreach (var net in incoming)
                        {
                            var existing = AvailableNetworks.FirstOrDefault(n => n.Ssid == net.Ssid);
                            bool isConnected = connectedProfile?.ProfileName == net.Ssid;
                            if (existing != null)
                            {
                                existing.SignalStrength = net.SignalBars;
                                existing.IsConnected = isConnected;
                                if (isConnected) existing.StatusText = "Bağlı";
                                else if (existing.StatusText == "Bağlı") existing.StatusText = "";
                            }
                            else
                            {
                                AvailableNetworks.Add(new WifiInfo
                                {
                                    Ssid = net.Ssid,
                                    SignalStrength = net.SignalBars,
                                    IsConnected = isConnected,
                                    StatusText = isConnected ? "Bağlı" : ""
                                });
                            }
                        }
                    });
                }
                catch { }

                await Task.Delay(5000); // 5s Loop
            }
        }
        catch { /* Handle permissions/unavailable */ }
    }

    private void InitBluetoothWatcher()
    {
        try
        {
            // Watch for Bluetooth devices
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var watcher = DeviceInformation.CreateWatcher(selector);
            
            watcher.Added += (s, a) => App.Current.Dispatcher.Invoke(() => 
            {
                if(!BluetoothDevices.Any(d => d.Id == a.Id))
                {
                    BluetoothDevices.Add(new BluetoothDeviceInfo { Name = a.Name, Id = a.Id, IsConnected = false, DeviceInfo = a });
                }
            });

            watcher.Updated += (s, a) => App.Current.Dispatcher.Invoke(() => 
            {
                var device = BluetoothDevices.FirstOrDefault(d => d.Id == a.Id);
                if (device != null) device.DeviceInfo?.Update(a);
            });

            watcher.Removed += (s, a) => App.Current.Dispatcher.Invoke(() => 
            {
                var device = BluetoothDevices.FirstOrDefault(d => d.Id == a.Id);
                if (device != null) BluetoothDevices.Remove(device);
            });

            watcher.Start();
            IsBluetoothOn = true; // Simplified
        }
        catch { /* Handle permissions/unavailable */ }
    }

    private GlobalSystemMediaTransportControlsSession? _currentSession;

    private async Task InitMediaControlsAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager != null)
            {
                manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                UpdateMediaInfo(manager.GetCurrentSession());
            }
        }
        catch { }
    }

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateMediaInfo(sender.GetCurrentSession());
    }

    private void UpdateMediaInfo(GlobalSystemMediaTransportControlsSession session)
    {
        if (_currentSession != null)
        {
            try {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
            } catch { }
        }

        _currentSession = session;

        if (session == null)
        {
            App.Current.Dispatcher.Invoke(() => CurrentMedia = new MediaInfo());
            return;
        }

        try
        {
            session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
            
            // Force first update
            Session_MediaPropertiesChanged(session, null);
        }
        catch { }
    }

    private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args)
    {
        try {
            var props = await sender.TryGetMediaPropertiesAsync();
            var pInfo = sender.GetPlaybackInfo();
            App.Current.Dispatcher.Invoke(() => 
            {
                CurrentMedia = new MediaInfo
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    IsPlaying = pInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                };
            });
        } catch { }
    }

    private async void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs? args)
    {
        try {
            var props = await sender.TryGetMediaPropertiesAsync();
            var pInfo = sender.GetPlaybackInfo();
            App.Current.Dispatcher.Invoke(() => 
            {
                CurrentMedia = new MediaInfo
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    IsPlaying = pInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                };
            });
        } catch { }
    }

    [RelayCommand]
    private async Task PreviousMediaAsync() { if (_currentSession != null) await _currentSession.TrySkipPreviousAsync(); }

    [RelayCommand]
    private async Task PlayPauseMediaAsync() { if (_currentSession != null) await _currentSession.TryTogglePlayPauseAsync(); }

    [RelayCommand]
    private async Task NextMediaAsync() { if (_currentSession != null) await _currentSession.TrySkipNextAsync(); }

    [RelayCommand]
    private async Task SelectWifiAsync(WifiInfo info)
    {
        if (info == null || info.IsConnected) return; // Do nothing if already connected
        
        // Toggle visibility
        if (info.IsPasswordPromptVisible)
        {
            info.IsPasswordPromptVisible = false;
            info.StatusText = "";
            return;
        }

        // Hide others
        foreach (var net in AvailableNetworks)
        {
            net.IsPasswordPromptVisible = false;
            if (net.StatusText == "Şifre Gerekli")
            {
                net.StatusText = "";
            }
        }

        try {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count > 0)
            {
                var network = adapters[0].NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == info.Ssid);
                if (network != null)
                {
                    info.StatusText = "Bağlanıyor...";
                    var result = await adapters[0].ConnectAsync(network, WiFiReconnectionKind.Automatic);
                    if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                    {
                        info.StatusText = "Bağlı";
                        foreach(var net in AvailableNetworks) net.IsPasswordPromptVisible = false;
                        return;
                    }
                }
            }
        } catch {}

        foreach (var net in AvailableNetworks)
        {
            net.IsPasswordPromptVisible = false;
        }
        info.IsPasswordPromptVisible = true;
        info.StatusText = "Şifre Gerekli";
    }

    [RelayCommand]
    private async Task ConnectWifiAsync(System.Windows.Controls.PasswordBox pb)
    {
        var info = AvailableNetworks.FirstOrDefault(n => n.IsPasswordPromptVisible);
        if (info == null || string.IsNullOrWhiteSpace(pb.Password)) return;

        info.StatusText = "Bağlanıyor...";
        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count > 0)
            {
                var network = adapters[0].NetworkReport.AvailableNetworks.FirstOrDefault(n => n.Ssid == info.Ssid);
                if (network != null)
                {
                    var credential = new Windows.Security.Credentials.PasswordCredential() { Password = pb.Password };
                    var result = await adapters[0].ConnectAsync(network, WiFiReconnectionKind.Automatic, credential);
                    if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                    {
                        info.IsPasswordPromptVisible = false;
                        pb.Password = "";
                        info.StatusText = "Bağlı";
                    }
                    else
                    {
                        info.StatusText = "Hatalı Şifre";
                    }
                }
            }
        }
        catch { info.StatusText = "Hata"; }
    }

    [RelayCommand]
    private async Task ForgetWifiAsync(WifiInfo info)
    {
        if (info == null) return;
        try {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = $"wlan delete profile name=\"{info.Ssid}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();

            info.StatusText = "Unutuldu";
            if (info.IsConnected) await DisconnectWifiAsync(info);
            
            // Allow password prompt to disappear if open
            info.IsPasswordPromptVisible = false;
        } catch {}
    }

    [RelayCommand]
    private async Task ToggleBluetoothPairingAsync(BluetoothDeviceInfo info)
    {
        if (info == null) return;
        info.StatusText = "Açılıyor...";
        _ = Task.Run(() => 
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch { }
        });

        // Reset text after 5 seconds
        await Task.Delay(5000);
        info.StatusText = "";
    }

    [RelayCommand]
    private async Task DisconnectWifiAsync(WifiInfo info)
    {
        if (info == null || !info.IsConnected) return;
        
        try
        {
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count > 0)
            {
                adapters[0].Disconnect();
                info.IsConnected = false;
                info.StatusText = "";
                IsWifiConnected = false;
            }
        }
        catch { }
    }
}
