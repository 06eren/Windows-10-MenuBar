using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Windows_10_MenuBar.Interop;

public static class WindowHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Friendly process name display map
    private static readonly Dictionary<string, string> _friendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"]          = "Google Chrome",
        ["msedge"]          = "Microsoft Edge",
        ["firefox"]         = "Firefox",
        ["opera"]           = "Opera",
        ["brave"]           = "Brave",
        ["vivaldi"]         = "Vivaldi",
        ["Code"]            = "VS Code",
        ["devenv"]          = "Visual Studio",
        ["spotify"]         = "Spotify",
        ["discord"]         = "Discord",
        ["slack"]           = "Slack",
        ["teams"]           = "Microsoft Teams",
        ["explorer"]        = "Dosya Gezgini",
        ["notepad"]         = "Not Defteri",
        ["notepad++"]       = "Notepad++",
        ["winword"]         = "Word",
        ["excel"]           = "Excel",
        ["powerpnt"]        = "PowerPoint",
        ["mspaint"]         = "Paint",
        ["calc"]            = "Hesap Makinesi",
        ["cmd"]             = "Komut İstemi",
        ["powershell"]      = "PowerShell",
        ["WindowsTerminal"] = "Terminal",
        ["taskmgr"]         = "Görev Yöneticisi",
        ["regedit"]         = "Kayıt Defteri",
        ["mmc"]             = "Sistem Yönetimi",
        ["vlc"]             = "VLC",
        ["obs64"]           = "OBS Studio",
        ["obs32"]           = "OBS Studio",
        ["Steam"]           = "Steam",
        ["epicgameslauncher"] = "Epic Games",
        ["msteams"]         = "Microsoft Teams",
        ["outlook"]         = "Outlook",
        ["onenote"]         = "OneNote",
        ["acrobat"]         = "Adobe Acrobat",
        ["photoshop"]       = "Photoshop",
        ["premiere"]        = "Premiere Pro",
        ["afterfx"]         = "After Effects",
        ["audacity"]        = "Audacity",
    };

    /// <summary>
    /// Gets the name of the currently active (foreground) application.
    /// Filters out the bar's own window so it never shows its own name.
    /// </summary>
    /// <param name="ownHwnd">The HWND of the menu bar window to exclude.</param>
    public static string GetActiveWindowTitle(IntPtr ownHwnd = default)
    {
        const int nChars = 256;
        IntPtr handle = GetForegroundWindow();

        if (handle == IntPtr.Zero) return string.Empty;

        // Filter out our own window — do not show "MenuBar" or our process name
        if (ownHwnd != default && handle == ownHwnd) return string.Empty;

        // Try to get process name first (cleaner app name)
        try
        {
            GetWindowThreadProcessId(handle, out uint pid);
            if (pid > 0)
            {
                var process = Process.GetProcessById((int)pid);
                string procName = process.ProcessName;

                // Skip if it's our own process
                if (string.Equals(procName, "Windows-10-MenuBar", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(procName, "Windows_10_MenuBar", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                if (_friendlyNames.TryGetValue(procName, out string? friendly))
                    return friendly;

                // Use MainModule description if available and short enough
                try
                {
                    string? desc = process.MainModule?.FileVersionInfo.FileDescription;
                    if (!string.IsNullOrWhiteSpace(desc) && desc.Length < 50)
                        return desc.Trim();
                }
                catch { }

                // Fall back to window title text
                var buff = new StringBuilder(nChars);
                if (GetWindowText(handle, buff, nChars) > 0)
                {
                    string title = buff.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(title))
                        return title;
                }

                // Last resort: capitalize process name
                if (!string.IsNullOrWhiteSpace(procName))
                    return char.ToUpper(procName[0]) + procName[1..];
            }
        }
        catch { }

        // Ultimate fallback: window title
        var fallbackBuff = new StringBuilder(nChars);
        if (GetWindowText(handle, fallbackBuff, nChars) > 0)
            return fallbackBuff.ToString().Trim();

        return string.Empty;
    }
}
