using System.Runtime.InteropServices;
using System.Windows;

namespace ComicSpeechBalloon;

/// <summary>
/// P/Invoke helpers for cursor position, transparent window styling, and system tray icon.
/// </summary>
internal static class NativeMethods
{
    // ── Cursor ──────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Retrieves the cursor position in screen coordinates, returned as a WPF Point.
    /// </summary>
    public static Point GetCursorPosition()
    {
        GetCursorPos(out POINT pt);
        return new Point(pt.X, pt.Y);
    }

    // ── Window styles ───────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// Makes a window click-through by adding WS_EX_TRANSPARENT.
    /// </summary>
    public static void MakeWindowTransparent(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Temporarily restores click detection so popup menus can be dismissed.
    /// </summary>
    public static void MakeWindowOpaque(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    // ── System Tray (Shell_NotifyIcon) ──────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(NIM dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int WM_TRAYICON = 0x8000;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const int IDI_INFORMATION = 32516;

    // 3rd-party app tray command. There exists an ancient Windows bug: 
    // when NOTIFYICONDATA.hWnd is destroyed, the icon may not auto-remove.
    // We use a load-icon + custom message approach to avoid this.

    private static nint s_trayIconHandle;
    private static uint s_trayCallbackMessage;
    private static Action? s_onTrayLeftClick;
    private static Action? s_onTrayRightClick;

    /// <summary>
    /// Creates a system tray icon. 
    /// Calls <paramref name="onLeftClick"/> on left-click (opens settings).
    /// Calls <paramref name="onRightClick"/> on right-click (context menu).
    /// Returns a callback message ID that must be handled in the WndProc of the associated window.
    /// </summary>
    public static uint AddTrayIcon(IntPtr hWnd, string tooltip, Action onLeftClick, Action onRightClick)
    {
        s_onTrayLeftClick = onLeftClick;
        s_onTrayRightClick = onRightClick;
        s_trayCallbackMessage = (uint)(WM_TRAYICON + new Random().Next(100, 999));
        s_trayIconHandle = LoadIcon(IntPtr.Zero, IDI_INFORMATION);

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = s_trayCallbackMessage,
            hIcon = s_trayIconHandle,
            szTip = tooltip
        };

        Shell_NotifyIcon(NIM_ADD, ref nid);
        return s_trayCallbackMessage;
    }

    /// <summary>
    /// Removes the system tray icon.
    /// </summary>
    public static void RemoveTrayIcon(IntPtr hWnd)
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = 1
        };
        Shell_NotifyIcon((NIM)NIM_DELETE, ref nid);
        if (s_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(s_trayIconHandle);
            s_trayIconHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Handles incoming window messages for the tray icon.
    /// Call this from the associated window's WndProc.
    /// Returns true if the message was handled.
    /// </summary>
    public static bool HandleTrayMessage(int msg, int wParam, int lParam)
    {
        if (msg == s_trayCallbackMessage)
        {
            if (lParam == WM_LBUTTONUP)
            {
                s_onTrayLeftClick?.Invoke();
                return true;
            }
            if (lParam == WM_RBUTTONUP)
            {
                s_onTrayRightClick?.Invoke();
                return true;
            }
        }
        return false;
    }

    // ── Structs ─────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private enum NIM : uint { ADD = 0, MODIFY = 1, DELETE = 2 }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }
}
