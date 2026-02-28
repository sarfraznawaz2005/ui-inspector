using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UIInspector.Inspection;
using UIInspector.Interop;
using UIInspector.Settings;
using WpfApp   = System.Windows.Application;
using WpfPoint = System.Windows.Point;

namespace UIInspector.Picker
{
    /// <summary>
    /// Orchestrates a single element-picking session.
    ///
    /// Call <see cref="PickAsync"/> to enter picking mode. The method:
    /// <list type="number">
    ///   <item>Shows a full-virtual-screen transparent overlay.</item>
    ///   <item>Installs low-level mouse and keyboard hooks.</item>
    ///   <item>Polls the cursor position at 50 ms intervals and highlights the
    ///         element under it using UI Automation.</item>
    ///   <item>Returns the inspected <see cref="ElementInfo"/> when the user
    ///         left-clicks, or <see langword="null"/> when they press Escape or
    ///         right-click to cancel.</item>
    /// </list>
    ///
    /// IMPORTANT: All cleanup (unhook, hide overlay, stop timer) happens inside
    /// <see cref="CleanupSession"/> which is called unconditionally on both the
    /// happy path and the cancellation path.
    /// </summary>
    public sealed class ElementPicker : IDisposable
    {
        // =====================================================================
        // Constants
        // =====================================================================

        private const int VK_ESCAPE = 0x1B;

        /// <summary>
        /// Minimum cursor displacement (pixels) before we fire a new UIA inspection.
        /// Keeps CPU load low when the cursor is stationary.
        /// </summary>
        private const double MovementThresholdPx = 5.0;

        // =====================================================================
        // Fields — session state
        // =====================================================================

        private OverlayWindow?      _overlay;
        private ElementHighlighter? _highlighter;

        // Hook handles — stored as fields so they are not GC-collected while the
        // unmanaged hook is active (GC would collect the delegate, causing a crash).
        private IntPtr _mouseHookHandle   = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;

        // Delegate references must be kept alive for the same reason.
        private LowLevelMouseProc?    _mouseProcDelegate;
        private LowLevelKeyboardProc? _keyboardProcDelegate;

        private DispatcherTimer? _pollTimer;

        // Completion source resolved by the hook callbacks.
        private TaskCompletionSource<ElementInfo?>? _tcs;

        // Last known cursor position — used for movement throttling.
        private POINT _lastCursorPos;

        // The most recently inspected element (updated on background thread,
        // read on the UI thread for the click handler).
        private volatile ElementInfo? _currentElement;

        // Guard against re-entrant or double-cleanup.
        private volatile bool _sessionActive;
        private int           _disposed;

        // =====================================================================
        // Constructor / Dispose
        // =====================================================================

        public ElementPicker() { }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                CleanupSession();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Enters element-picking mode and asynchronously waits for the user to
        /// select an element or cancel.
        /// </summary>
        /// <param name="settings">User preferences (highlight color/opacity).</param>
        /// <returns>
        /// The <see cref="ElementInfo"/> for the element the user clicked on,
        /// or <see langword="null"/> if the session was cancelled.
        /// </returns>
        public async Task<ElementInfo?> PickAsync(AppSettings settings)
        {
            if (_sessionActive)
                throw new InvalidOperationException("A pick session is already in progress.");

            _sessionActive = true;
            _currentSettingsSnapshot = settings;
            _tcs = new TaskCompletionSource<ElementInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                // All WPF operations must run on a WPF dispatcher thread.
                // Since TrayApplication drives a WinForms message loop, we create
                // a WPF Application/dispatcher if one is not already running.
                await EnsureWpfDispatcher();

                // Marshal overlay creation onto the WPF dispatcher thread.
                WpfApp.Current.Dispatcher.Invoke(() =>
                {
                    _overlay     = new OverlayWindow();
                    _highlighter = new ElementHighlighter(_overlay.Canvas);
                    _overlay.ShowOverlay();
                });

                // Install global hooks (must be done from a thread with a message loop).
                // The WinForms STA thread that runs TrayApplication satisfies this.
                InstallHooks();

                // Start the polling timer on the WPF dispatcher.
                WpfApp.Current.Dispatcher.Invoke(() =>
                {
                    _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
                    {
                        Interval = TimeSpan.FromMilliseconds(50)
                    };
                    _pollTimer.Tick += OnPollTimerTick;
                    _pollTimer.Start();
                });

                // Await user action (click or cancel).
                return await _tcs.Task;
            }
            finally
            {
                CleanupSession();
            }
        }

        // =====================================================================
        // WPF Dispatcher bootstrap
        // =====================================================================

        /// <summary>
        /// Ensures a WPF <see cref="Application"/> instance exists so we can
        /// use <see cref="Dispatcher"/> and WPF windows from a WinForms host.
        ///
        /// If a WPF Application is already running (e.g., from a previous pick
        /// session) this is a no-op.
        /// </summary>
        private static async Task EnsureWpfDispatcher()
        {
            if (WpfApp.Current != null)
                return;

            // Create the WPF Application on a dedicated STA thread so that WPF
            // has its own dispatcher. The thread stays alive for the application's
            // lifetime because we start its dispatcher without an explicit
            // shutdown — it shuts down with the process.
            var wpfReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var wpfThread = new Thread(() =>
            {
                var app = new WpfApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Startup += (_, _) => wpfReady.TrySetResult(true);
                app.Run();
            });

            wpfThread.SetApartmentState(ApartmentState.STA);
            wpfThread.IsBackground = true;
            wpfThread.Name = "WpfDispatcherThread";
            wpfThread.Start();

            await wpfReady.Task;
        }

        // =====================================================================
        // Polling timer — cursor tracking & highlighting
        // =====================================================================

        /// <summary>
        /// Fires every 50 ms on the WPF dispatcher thread.
        /// Gets the cursor position; if it has moved more than the threshold,
        /// triggers a UIA inspection on a background thread.
        /// </summary>
        private async void OnPollTimerTick(object? sender, EventArgs e)
        {
            if (!_sessionActive) return;

            if (!NativeMethods.GetCursorPos(out POINT cursor))
                return;

            double dx = cursor.X - _lastCursorPos.X;
            double dy = cursor.Y - _lastCursorPos.Y;

            if (Math.Sqrt(dx * dx + dy * dy) < MovementThresholdPx)
                return;

            _lastCursorPos = cursor;

            // Run UIA on a background thread; update highlighter on dispatcher.
            var screenPoint = new WpfPoint(cursor.X, cursor.Y);
            ElementInfo? info = await AutomationInspector.InspectAtPoint(screenPoint);

            // Store the element so the mouse hook can read it on click.
            _currentElement = info;

            // Update the highlight rectangle back on the dispatcher (we already
            // are here because this is an async-void event handler that resumed
            // on the SynchronizationContext captured at the await point, which
            // for DispatcherTimer is the Dispatcher itself).
            if (!_sessionActive) return;

            if (info != null && !info.BoundingRectangle.IsEmpty)
                _highlighter?.Highlight(info.BoundingRectangle, _currentSettingsSnapshot);
            else
                _highlighter?.Clear();
        }

        // We snapshot the settings reference so the async timer callback does not
        // need to capture the full AppSettings reference across a context switch.
        private AppSettings _currentSettingsSnapshot = new();

        // =====================================================================
        // Global hook installation
        // =====================================================================

        /// <summary>
        /// Installs WH_MOUSE_LL and WH_KEYBOARD_LL hooks.
        ///
        /// Delegate references are stored in fields to prevent GC collection.
        /// </summary>
        private void InstallHooks()
        {
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);

            _mouseProcDelegate    = MouseHookCallback;
            _keyboardProcDelegate = KeyboardHookCallback;

            _mouseHookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _mouseProcDelegate,
                moduleHandle,
                dwThreadId: 0);

            if (_mouseHookHandle == IntPtr.Zero)
                Debug.WriteLine($"[ElementPicker] SetWindowsHookEx(mouse) failed: {Marshal.GetLastWin32Error()}");

            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _keyboardProcDelegate,
                moduleHandle,
                dwThreadId: 0);

            if (_keyboardHookHandle == IntPtr.Zero)
                Debug.WriteLine($"[ElementPicker] SetWindowsHookEx(keyboard) failed: {Marshal.GetLastWin32Error()}");
        }

        // =====================================================================
        // Hook callbacks
        // =====================================================================

        /// <summary>
        /// Low-level mouse hook callback.
        /// Always calls CallNextHookEx so events pass through to target windows.
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _sessionActive)
            {
                int message = (int)wParam;

                if (message == NativeMethods.WM_LBUTTONDOWN)
                {
                    // Capture the element that was being highlighted at click time.
                    ElementInfo? selected = _currentElement;
                    _tcs?.TrySetResult(selected);
                }
                else if (message == NativeMethods.WM_RBUTTONDOWN)
                {
                    // Right-click cancels the session.
                    _tcs?.TrySetResult(null);
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Low-level keyboard hook callback.
        /// Escape cancels the session. All other keys pass through unchanged.
        /// </summary>
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _sessionActive)
            {
                if ((int)wParam == NativeMethods.WM_KEYDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (hookStruct.vkCode == VK_ESCAPE)
                        _tcs?.TrySetResult(null);
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        /// <summary>
        /// Removes hooks, stops the timer, hides the overlay, and resets session state.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        private void CleanupSession()
        {
            _sessionActive = false;

            // Unhook mouse.
            if (_mouseHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            // Unhook keyboard.
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }

            // Release delegate references now that the hooks are removed.
            _mouseProcDelegate    = null;
            _keyboardProcDelegate = null;

            // Stop timer and hide overlay on the WPF dispatcher.
            // Use BeginInvoke (async) to avoid deadlocking when CleanupSession
            // is called from a hook callback that holds a lock while the
            // dispatcher thread is waiting for the same lock.
            if (WpfApp.Current != null)
            {
                try
                {
                    WpfApp.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _pollTimer?.Stop();
                        _pollTimer = null;

                        _highlighter?.Clear();
                        _highlighter = null;

                        // Close the overlay to release its HWND and visual resources
                        // rather than just hiding it — a fresh one is created per session.
                        _overlay?.Close();
                        _overlay = null;
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ElementPicker] CleanupSession dispatcher invoke failed: {ex.Message}");
                }
            }

            _currentElement = null;
        }
    }
}
