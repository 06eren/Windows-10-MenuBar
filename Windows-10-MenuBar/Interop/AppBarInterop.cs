using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Windows_10_MenuBar.Interop;

public static class AppBarInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    private const int ABM_NEW      = 0x00000000;
    private const int ABM_REMOVE   = 0x00000001;
    private const int ABM_QUERYPOS = 0x00000002;
    private const int ABM_SETPOS   = 0x00000003;
    private const int ABE_TOP      = 1;

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    // Track registered HWNDs to prevent duplicate ABM_NEW
    private static readonly System.Collections.Generic.HashSet<IntPtr> _registered = new();

    public static void RegisterBar(Window window, double height)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero) return;

        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;

        // Only send ABM_NEW if not already registered
        if (!_registered.Contains(hwnd))
        {
            SHAppBarMessage(ABM_NEW, ref abd);
            _registered.Add(hwnd);
        }

        // Get DPI Scaling
        double dpiX = 1.0;
        double dpiY = 1.0;
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
        }

        int screenWidthPx  = (int)(SystemParameters.PrimaryScreenWidth  * dpiX);
        int barHeightPx    = (int)(height * dpiY);

        // Set position at top using physical pixels
        abd.uEdge    = ABE_TOP;
        abd.rc.left  = 0;
        abd.rc.right = screenWidthPx;
        abd.rc.top   = 0;
        abd.rc.bottom = barHeightPx;

        SHAppBarMessage(ABM_QUERYPOS, ref abd);
        SHAppBarMessage(ABM_SETPOS,   ref abd);

        // Force window to correct position and size in WPF DIPs
        window.Dispatcher.BeginInvoke(() =>
        {
            window.Left   = 0;
            window.Top    = 0;
            window.Width  = SystemParameters.PrimaryScreenWidth;
            window.Height = height;
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    public static void UnregisterBar(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero) return;

        APPBARDATA abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = hwnd;

        SHAppBarMessage(ABM_REMOVE, ref abd);
        _registered.Remove(hwnd);

        // Broadcast to all windows so taskbar & other apps reclaim the freed space
        SendMessageTimeout(
            new IntPtr(0xFFFF), // HWND_BROADCAST
            0x001A,             // WM_SETTINGCHANGE
            IntPtr.Zero,
            IntPtr.Zero,
            0x0002,             // SMTO_ABORTIFHUNG
            1000,
            out _);
    }
}
