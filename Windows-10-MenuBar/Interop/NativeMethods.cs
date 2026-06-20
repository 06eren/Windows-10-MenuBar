using System;
using System.Runtime.InteropServices;

namespace Windows_10_MenuBar.Interop;

public static class NativeMethods
{
    private const byte VK_LWIN = 0x5B;
    private const byte VK_TAB = 0x09;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>Simulates Win+Tab to open Task View / virtual desktops</summary>
    public static void SimulateWinTab()
    {
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB,  0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    // ── Fullscreen detection ─────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Returns true if a fullscreen application is running on the primary monitor.
    /// Excludes the shell (explorer) and the bar's own HWND.
    /// </summary>
    public static bool IsFullscreenAppRunning(IntPtr barHwnd)
    {
        try
        {
            IntPtr fgWnd = GetForegroundWindow();
            if (fgWnd == IntPtr.Zero || fgWnd == barHwnd)
                return false;

            // Get foreground window rect
            if (!GetWindowRect(fgWnd, out RECT wndRect))
                return false;

            // Get the monitor the foreground window is on
            IntPtr monitor = MonitorFromWindow(fgWnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(monitor, ref mi))
                return false;

            // A true fullscreen app exactly covers the monitor RECT.
            // Maximized windows have invisible borders so their RECT is slightly larger (e.g. Left=-8, Top=-8)
            // Or if an AppBar is registered, a maximized window's Top will be at the AppBar's bottom.
            var monRect = mi.rcMonitor;
            bool isFullscreen = wndRect.Left == monRect.Left
                             && wndRect.Top == monRect.Top
                             && wndRect.Right == monRect.Right
                             && wndRect.Bottom == monRect.Bottom;

            if (!isFullscreen) return false;

            // Exclude explorer/shell windows
            var classNameBuilder = new System.Text.StringBuilder(256);
            GetClassName(fgWnd, classNameBuilder, classNameBuilder.Capacity);
            string className = classNameBuilder.ToString();

            if (className == "Progman" || className == "WorkerW")
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
}
