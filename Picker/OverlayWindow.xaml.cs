using System;
using System.Windows;
using System.Windows.Interop;
using UIInspector.Interop;

namespace UIInspector.Picker
{
    /// <summary>
    /// A full-virtual-screen transparent overlay window used during element picking.
    ///
    /// The window is made click-through (WS_EX_TRANSPARENT) so that all mouse events
    /// pass through to the windows beneath it. The global low-level mouse hook in
    /// <see cref="ElementPicker"/> captures those events independently.
    ///
    /// It is also marked WS_EX_TOOLWINDOW so it never appears in the taskbar or
    /// Alt-Tab list even though it spans all monitors.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        // =====================================================================
        // Constructor
        // =====================================================================

        public OverlayWindow()
        {
            InitializeComponent();

            SourceInitialized += OnSourceInitialized;
            Loaded            += OnLoaded;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Exposes the drawing canvas so <see cref="ElementHighlighter"/> can
        /// add and reposition shapes without needing an internal reference.
        /// </summary>
        public System.Windows.Controls.Canvas Canvas => OverlayCanvas;

        /// <summary>
        /// Positions the window over all monitors and makes it visible.
        /// </summary>
        public void ShowOverlay()
        {
            // Reapply virtual-screen bounds every time we show, in case the
            // monitor configuration changed since the last pick session.
            ApplyVirtualScreenBounds();
            Show();
        }

        /// <summary>
        /// Hides the overlay without destroying the window, so it can be
        /// reused for the next pick session without paying the creation cost.
        /// </summary>
        public void HideOverlay()
        {
            Hide();
        }

        /// <summary>
        /// Toggles whether the overlay window receives mouse input.
        ///
        /// Pass <c>true</c> to remove <c>WS_EX_TRANSPARENT</c> so WPF mouse events
        /// are delivered to the canvas (used by <see cref="SpotPicker"/>).
        /// Pass <c>false</c> to restore it so clicks pass through to windows beneath
        /// (used by <see cref="ElementPicker"/>).
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            long exStyle = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);

            if (interactive)
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;   // allow mouse input
            else
                exStyle |= NativeMethods.WS_EX_TRANSPARENT;    // pass through

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        /// <summary>
        /// Called once the HWND has been created but before the window is shown.
        /// This is the only safe moment to call SetWindowLong on a WPF window.
        /// </summary>
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Read the current extended style flags (64-bit safe).
            long exStyle = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);

            // Add:
            //   WS_EX_TRANSPARENT  — all mouse input passes through to windows below
            //   WS_EX_TOOLWINDOW   — excluded from taskbar / Alt-Tab
            //   WS_EX_NOACTIVATE   — never steals focus from the target application
            exStyle |= NativeMethods.WS_EX_TRANSPARENT;
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            exStyle |= NativeMethods.WS_EX_NOACTIVATE;

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        /// <summary>
        /// Called after the window has been laid out for the first time.
        /// Sets position and size to cover all monitors.
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            ApplyVirtualScreenBounds();
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>
        /// Positions and sizes the window to cover the full virtual screen
        /// (the bounding rectangle that spans all connected monitors).
        ///
        /// WindowState.Maximized is intentionally NOT used here because WPF
        /// maximizes to the primary monitor only; setting Left/Top/Width/Height
        /// explicitly is the correct way to span multiple monitors.
        /// </summary>
        private void ApplyVirtualScreenBounds()
        {
            Left   = SystemParameters.VirtualScreenLeft;
            Top    = SystemParameters.VirtualScreenTop;
            Width  = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }
    }
}
