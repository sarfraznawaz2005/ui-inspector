using System;
using System.Runtime.InteropServices;

namespace UIInspector.Interop
{
    // -------------------------------------------------------------------------
    // Delegate types for hook callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Callback delegate for a low-level mouse hook installed via SetWindowsHookEx.
    /// </summary>
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Callback delegate for a low-level keyboard hook installed via SetWindowsHookEx.
    /// </summary>
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Defines the x- and y-coordinates of a point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Contains information about a low-level mouse input event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>
    /// Contains information about a low-level keyboard input event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // -------------------------------------------------------------------------
    // P/Invoke declarations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exposes Win32 native methods required by UI Inspector.
    /// All members are internal to prevent accidental misuse from outside the assembly.
    /// </summary>
    internal static class NativeMethods
    {
        // =====================================================================
        // Constants — Hot-key modifiers
        // =====================================================================

        public const uint MOD_ALT     = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT   = 0x0004;
        public const uint MOD_WIN     = 0x0008;

        // =====================================================================
        // Constants — Window messages
        // =====================================================================

        public const int WM_HOTKEY      = 0x0312;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP   = 0x0202;
        public const int WM_MOUSEMOVE   = 0x0200;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_KEYDOWN     = 0x0100;
        public const int WM_KEYUP       = 0x0101;
        public const int WM_SYSKEYDOWN  = 0x0104;

        // =====================================================================
        // Constants — Window styles / long indices
        // =====================================================================

        public const int GWL_EXSTYLE        = -20;
        public const int WS_EX_TRANSPARENT  = 0x00000020;
        public const int WS_EX_LAYERED      = 0x00080000;
        public const int WS_EX_TOPMOST      = 0x00000008;
        public const int WS_EX_TOOLWINDOW   = 0x00000080;
        public const int WS_EX_NOACTIVATE   = 0x08000000;

        // =====================================================================
        // Constants — Hook types
        // =====================================================================

        public const int WH_MOUSE_LL    = 14;
        public const int WH_KEYBOARD_LL = 13;

        // =====================================================================
        // Constants — Stock cursors
        // =====================================================================

        public static readonly IntPtr IDC_CROSS = new IntPtr(32515);

        // =====================================================================
        // Constants — SetLayeredWindowAttributes flags
        // =====================================================================

        public const uint LWA_ALPHA     = 0x00000002;
        public const uint LWA_COLORKEY  = 0x00000001;

        // =====================================================================
        // Hot-key registration
        // =====================================================================

        /// <summary>
        /// Defines a system-wide hot key.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Frees a hot key previously registered by RegisterHotKey.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // =====================================================================
        // Cursor & Window
        // =====================================================================

        /// <summary>
        /// Retrieves the position of the mouse cursor, in screen coordinates.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Sets the cursor shape. Pass a handle obtained from LoadCursor.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);

        /// <summary>
        /// Loads a predefined system cursor. Pass IntPtr.Zero for hInstance and
        /// one of the IDC_* constants for lpCursorName.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        /// <summary>
        /// Replaces the contents of a system cursor. hCursor must be a copy
        /// (not a shared handle) — the system takes ownership.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetSystemCursor(IntPtr hCursor, uint id);

        /// <summary>
        /// Creates a duplicate of a cursor handle (needed before SetSystemCursor
        /// because SetSystemCursor takes ownership of the handle).
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        /// <summary>
        /// Sets system-wide parameters. Use SPI_SETCURSORS to restore all
        /// system cursors to their defaults after a SetSystemCursor call.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        // System cursor IDs for SetSystemCursor
        public const uint OCR_NORMAL  = 32512;   // standard arrow
        public const uint OCR_IBEAM   = 32513;   // text I-beam
        public const uint OCR_CROSS   = 32515;   // crosshair

        // SystemParametersInfo action to restore all cursors to scheme defaults
        public const uint SPI_SETCURSORS = 0x0057;

        /// <summary>
        /// Returns a handle to the window that contains the specified point.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        /// <summary>
        /// Retrieves the identifier of the thread and process that created the specified window.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Brings the specified window to the foreground.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // =====================================================================
        // Window long / extended styles (overlay transparency)
        // =====================================================================

        /// <summary>
        /// Retrieves information about the specified window (64-bit safe).
        /// Use GWL_EXSTYLE (-20) to read the extended window style.
        /// </summary>
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        /// <summary>
        /// Changes an attribute of the specified window (64-bit safe).
        /// Use GWL_EXSTYLE (-20) to modify the extended window style.
        /// </summary>
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        /// <summary>
        /// Sets the opacity and transparency color key of a layered window.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        // =====================================================================
        // Window hooks (mouse + keyboard)
        // =====================================================================

        /// <summary>
        /// Installs an application-defined hook procedure into a hook chain.
        /// Use WH_MOUSE_LL (14) for low-level mouse events.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelMouseProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        /// <summary>
        /// Installs an application-defined keyboard hook procedure into a hook chain.
        /// Use WH_KEYBOARD_LL (13) for low-level keyboard events.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        /// <summary>
        /// Removes a hook procedure installed in a hook chain by SetWindowsHookEx.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// Passes the hook information to the next hook in the current hook chain.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // =====================================================================
        // Module handle (required when installing global hooks)
        // =====================================================================

        /// <summary>
        /// Retrieves a module handle for the specified module.
        /// Pass null to get the handle of the file used to create the calling process.
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        // =====================================================================
        // Window text / class helpers (useful for future Inspection phase)
        // =====================================================================

        /// <summary>
        /// Copies the text of the specified window's title bar into a buffer.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// Retrieves the name of the class to which the specified window belongs.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        /// <summary>
        /// Retrieves the dimensions of the bounding rectangle of the specified window.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // =====================================================================
        // RECT struct (companion to GetWindowRect)
        // =====================================================================
    }

    /// <summary>
    /// Defines the coordinates of the upper-left and lower-right corners of a rectangle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width  => Right  - Left;
        public int Height => Bottom - Top;
    }
}
