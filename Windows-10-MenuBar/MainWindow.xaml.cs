using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;
using Windows_10_MenuBar.ViewModels;

namespace Windows_10_MenuBar;

public partial class MainWindow : Window
{
    private BarViewModel _viewModel = null!;
    private DispatcherTimer? _windowTitleTimer;
    private DispatcherTimer? _fullscreenTimer;
    private DispatcherTimer? _autoHideTimer;
    private IntPtr _ownHwnd;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new BarViewModel();
        DataContext = _viewModel;

        // Apply saved settings (color / opacity)
        ApplyBarSettings();

        this.Width  = SystemParameters.PrimaryScreenWidth;
        this.Top    = 0;
        this.Left   = 0;

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed  += MainWindow_Closed;

        // Watch for settings changes to auto-apply bar visuals
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(_viewModel.BarBackground) or nameof(_viewModel.BarOpacity))
                ApplyBarSettings();
        };
    }

    private void ApplyBarSettings()
    {
        try
        {
            if (BarBrush != null)
            {
                var color = (Color)ColorConverter.ConvertFromString(_viewModel.BarBackground);
                BarBrush.Color   = color;
                BarBrush.Opacity = _viewModel.BarOpacity;
            }
            this.Height = _viewModel.Settings.BarHeight;
            AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
        }
        catch { }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Cache our own HWND for filtering in title/fullscreen checks
        _ownHwnd = new WindowInteropHelper(this).Handle;

        // Hook WM_WINDOWPOSCHANGED so the bar always snaps back to top-left
        var hwndSource = HwndSource.FromHwnd(_ownHwnd);
        hwndSource?.AddHook(WndProc);

        AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);

        // Active window title — 500ms poll
        _windowTitleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _windowTitleTimer.Tick += (s, ev) =>
        {
            var title = WindowHelper.GetActiveWindowTitle(_ownHwnd);
            if (!string.IsNullOrWhiteSpace(title))
                _viewModel.ActiveWindowTitle = title;
        };
        _windowTitleTimer.Start();

        // Fullscreen detection — 750ms poll
        _fullscreenTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _fullscreenTimer.Tick += FullscreenTimer_Tick;
        _fullscreenTimer.Start();

        // Auto-hide setup
        if (_viewModel.Settings.AutoHide)
            SetupAutoHide();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_WINDOWPOSCHANGING = 0x0046;
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
            
            // Eğer pencere hareket ettirilmeye çalışılıyorsa, X ve Y'yi 0'a zorla
            if ((pos.flags & 0x0002) == 0) // 0x0002 is SWP_NOMOVE
            {
                pos.x = 0;
                pos.y = 0;
                System.Runtime.InteropServices.Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    private void FullscreenTimer_Tick(object? sender, EventArgs e)
    {
        bool fullscreen = NativeMethods.IsFullscreenAppRunning(_ownHwnd);
        bool shouldHide = fullscreen && _viewModel.Settings.HideOnFullscreen;

        // Toggle Topmost and visibility
        if (shouldHide)
        {
            if (this.Topmost)
            {
                this.Topmost = false;
                // Unregister AppBar while hidden to avoid interfering with fullscreen
                AppBarInterop.UnregisterBar(this);
            }
        }
        else
        {
            if (!this.Topmost)
            {
                this.Topmost = true;
                AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
            }
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _fullscreenTimer?.Stop();
        _windowTitleTimer?.Stop();
        _autoHideTimer?.Stop();
        AppBarInterop.UnregisterBar(this);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _windowTitleTimer?.Stop();
        _fullscreenTimer?.Stop();
        _autoHideTimer?.Stop();
    }

    // ── Auto-hide ────────────────────────────────────────────────────────────

    private void SetupAutoHide()
    {
        // Stop any existing autohide timer first
        _autoHideTimer?.Stop();
        _autoHideTimer = null;

        this.Opacity = 0;
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _autoHideTimer.Tick += (s, e) =>
        {
            GetCursorPos(out var pt);
            bool nearTop = pt.Y <= _viewModel.Settings.BarHeight + 2;
            this.Opacity = nearTop ? 1 : 0;
        };
        _autoHideTimer.Start();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // ── Context Menu / Right Click ────────────────────────────────────────────

    private void Window_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Handled by ContextMenu automatically
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.PlacementTarget = this;
        SettingsPopup.HorizontalOffset = SystemParameters.PrimaryScreenWidth - 380;
        SettingsPopup.VerticalOffset   = this.Height;
        SettingsPopup.IsOpen = true;
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
    }

    // ── Settings Sliders ─────────────────────────────────────────────────────

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;
        _viewModel.BarOpacity = e.NewValue;
        try { BarBrush.Opacity = e.NewValue; } catch { }
        SettingsService.Save();
    }

    private void HeightSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;
        double newH = Math.Round(e.NewValue / 2) * 2;
        this.Height = newH;
        _viewModel.Settings.BarHeight = newH;
        AppBarInterop.RegisterBar(this, newH);
        SettingsService.Save();
    }

    private void BrightnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;
        _viewModel.SetBrightnessCommand.Execute((int)e.NewValue);
    }

    // ── Settings Toggles ─────────────────────────────────────────────────────

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        SettingsService.Save();
    }

    private void AutoHide_Changed(object sender, RoutedEventArgs e)
    {
        SettingsService.Save();
        if (_viewModel.Settings.AutoHide)
            SetupAutoHide();
        else
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
            this.Opacity = 1;
        }
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyStartWithWindowsCommand.Execute(_viewModel.Settings.StartWithWindows);
    }

    // ── Close ────────────────────────────────────────────────────────────────

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    // ── Wi-Fi Popup Reset ─────────────────────────────────────────────────────

    private void PopupWifi_Closed(object sender, EventArgs e)
    {
        if (_viewModel == null) return;
        foreach (var net in _viewModel.AvailableNetworks)
        {
            net.IsPasswordPromptVisible = false;
            if (net.StatusText == "Şifre Gerekli")
                net.StatusText = "";
        }
    }

    // ── Calendar Popup Reset ──────────────────────────────────────────────────

    private void CalendarPopup_Opened(object sender, EventArgs e)
    {
        _viewModel?.ResetCalendarToToday();
    }

    private void CalendarTodayBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ResetCalendarToToday();
    }
}
