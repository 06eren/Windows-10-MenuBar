using System;
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
    };

    public static string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder Buff = new StringBuilder(nChars);
        IntPtr handle = GetForegroundWindow();

        if (handle == IntPtr.Zero) return "Windows";

        // Try to get process name first (cleaner app name)
        try
        {
            GetWindowThreadProcessId(handle, out uint pid);
            if (pid > 0)
            {
                var process = Process.GetProcessById((int)pid);
                string procName = process.ProcessName;

                if (_friendlyNames.TryGetValue(procName, out string? friendly))
                    return friendly;

                // Use MainModule description if available
                try
                {
                    string? desc = process.MainModule?.FileVersionInfo.FileDescription;
                    if (!string.IsNullOrWhiteSpace(desc) && desc.Length < 40)
                        return desc;
                }
                catch { }

                // Fall back to capitalized process name
                if (!string.IsNullOrWhiteSpace(procName))
                    return char.ToUpper(procName[0]) + procName.Substring(1);
            }
        }
        catch { }

        // Ultimate fallback: window title
        if (GetWindowText(handle, Buff, nChars) > 0)
            return Buff.ToString();

        return "Windows";
    }
}
