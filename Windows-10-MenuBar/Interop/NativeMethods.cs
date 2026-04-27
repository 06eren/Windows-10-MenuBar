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
}
