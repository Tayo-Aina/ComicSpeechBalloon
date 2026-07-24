using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ComicSpeechBalloon;

/// <summary>
/// Detects the currently active (foreground) window — its process name and optional title.
/// </summary>
public static class ActiveWindowDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Information about the window the user is currently working in.
    /// </summary>
    public readonly record struct AppInfo(string ProcessName, string WindowTitle);

    /// <summary>
    /// Returns the foreground window's process name and title, or null on failure.
    /// </summary>
    public static AppInfo? GetActiveApp()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0)
                return null;

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
            }
            catch (ArgumentException)
            {
                return null; // Process exited between the calls
            }
            catch (Win32Exception)
            {
                return null;
            }

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string title = sb.ToString();

            return new AppInfo(processName, title);
        }
        catch
        {
            return null;
        }
    }
}
