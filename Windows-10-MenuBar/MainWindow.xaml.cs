using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Windows_10_MenuBar.Interop;
using Windows_10_MenuBar.Services;
using Windows_10_MenuBar.ViewModels;

namespace Windows_10_MenuBar;

public partial class MainWindow : Window
{
    private BarViewModel _viewModel;
    private DispatcherTimer _windowTitleTimer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new BarViewModel();
        DataContext = _viewModel;

        // Apply saved settings
        ApplyBarSettings();

        this.Width = SystemParameters.PrimaryScreenWidth;
        this.Top = 0;
        this.Left = 0;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        // Watch for settings changes to auto-apply
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
                BarBrush.Color = color;
                BarBrush.Opacity = _viewModel.BarOpacity;
            }
            this.Height = _viewModel.Settings.BarHeight;
            AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);
        }
        catch { }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppBarInterop.RegisterBar(this, _viewModel.Settings.BarHeight);

        _windowTitleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _windowTitleTimer.Tick += (s, ev) =>
        {
            var title = WindowHelper.GetActiveWindowTitle();
            if (!string.IsNullOrWhiteSpace(title))
                _viewModel.ActiveWindowTitle = title;
        };
        _windowTitleTimer.Start();

        // Auto-hide setup
        if (_viewModel.Settings.AutoHide)
            SetupAutoHide();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        AppBarInterop.UnregisterBar(this);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _windowTitleTimer?.Stop();
    }

    // ── Auto-hide ────────────────────────────────────────────────────────────

    private void SetupAutoHide()
    {
        this.Opacity = 0;
        var showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        showTimer.Tick += (s, e) =>
        {
            GetCursorPos(out var pt);
            bool nearTop = pt.Y <= 4;
            this.Opacity = nearTop ? 1 : 0;
        };
        showTimer.Start();
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
        SettingsPopup.HorizontalOffset = SystemParameters.PrimaryScreenWidth - 375;
        SettingsPopup.VerticalOffset = this.Height;
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
        try
        {
            BarBrush.Opacity = e.NewValue;
        }
        catch { }
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
            this.Opacity = 1;
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyStartWithWindowsCommand.Execute(_viewModel.Settings.StartWithWindows);
    }

    // ── Wi-Fi Popup Reset ─────────────────────────────────────────────────────

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void PopupWifi_Closed(object sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            foreach (var net in _viewModel.AvailableNetworks)
            {
                net.IsPasswordPromptVisible = false;
                if (net.StatusText == "Şifre Gerekli")
                    net.StatusText = "";
            }
        }
    }
}
