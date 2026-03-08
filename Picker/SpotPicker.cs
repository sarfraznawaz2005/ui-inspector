using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UIInspector.Inspection;
using UIInspector.Interop;
using UIInspector.Settings;
using WpfApp        = System.Windows.Application;
using WpfPoint      = System.Windows.Point;
using WpfRect       = System.Windows.Rect;
using WpfRectangle  = System.Windows.Shapes.Rectangle;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfColor      = System.Windows.Media.Color;
using WpfColorConv  = System.Windows.Media.ColorConverter;
using WpfColors     = System.Windows.Media.Colors;

namespace UIInspector.Picker
{
    /// <summary>
    /// The result of a completed spot-picking session.
    /// </summary>
    public sealed class SpotResult
    {
        /// <summary>The rectangle the user drew, in physical screen pixel coordinates.</summary>
        public WpfRect DrawnBounds { get; init; }

        /// <summary>
        /// The UI element detected at the centre of the drawn rectangle, or
        /// <see langword="null"/> if nothing was found there.
        /// </summary>
        public ElementInfo? DetectedElement { get; init; }
    }

    /// <summary>
    /// Orchestrates a spot-picking session in which the user draws a rectangular
    /// selection over any area of the screen.
    ///
    /// Uses the same WH_MOUSE_LL hook technique as <see cref="ElementPicker"/> so
    /// that the overlay can remain fully transparent — WPF native mouse events are
    /// never used, because layered windows with alpha=0 pixels are click-through
    /// at the Windows compositor level regardless of WS_EX_TRANSPARENT.
    /// </summary>
    public sealed class SpotPicker : IDisposable
    {
        // =====================================================================
        // Constants
        // =====================================================================

        private const int    VK_ESCAPE    = 0x1B;
        private const double MinDragPixels = 4.0;

        // =====================================================================
        // Fields
        // =====================================================================

        private volatile bool _sessionActive;
        private int           _disposed;

        private TaskCompletionSource<SpotResult?>? _tcs;

        // WPF overlay (accessed only on WPF dispatcher thread)
        private OverlayWindow? _overlay;
        private WpfRectangle?  _selectionRect;

        // Drag state (physical pixel coordinates; written by hook, read by hook)
        private POINT        _dragStartPx;
        private volatile bool _isDragging;

        // Hook handles — must be kept as fields to prevent GC while hooks are alive.
        private IntPtr _mouseHookHandle    = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;

        private LowLevelMouseProc?    _mouseProcDelegate;
        private LowLevelKeyboardProc? _keyboardProcDelegate;

        private AppSettings _settingsSnapshot = new();

        // =====================================================================
        // Constructor / Dispose
        // =====================================================================

        public SpotPicker() { }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                CleanupSession();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        public async Task<SpotResult?> PickAsync(AppSettings settings)
        {
            if (_sessionActive)
                throw new InvalidOperationException("A spot session is already in progress.");

            _sessionActive    = true;
            _isDragging       = false;
            _settingsSnapshot = settings;
            _tcs = new TaskCompletionSource<SpotResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                await EnsureWpfDispatcher();

                WpfApp.Current.Dispatcher.Invoke(() =>
                {
                    _overlay       = new OverlayWindow();
                    _selectionRect = CreateSelectionRect(settings);
                    _overlay.Canvas.Children.Add(_selectionRect);
                    _overlay.ShowOverlay();
                    // WS_EX_TRANSPARENT is intentionally kept: the overlay stays
                    // transparent and click-through; the low-level mouse hook below
                    // captures all mouse events at the system level instead.
                });

                InstallHooks();

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

        private static async Task EnsureWpfDispatcher()
        {
            if (WpfApp.Current != null)
                return;

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
        // Hook installation
        // =====================================================================

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
                Debug.WriteLine($"[SpotPicker] SetWindowsHookEx(mouse) failed: {Marshal.GetLastWin32Error()}");

            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _keyboardProcDelegate,
                moduleHandle,
                dwThreadId: 0);

            if (_keyboardHookHandle == IntPtr.Zero)
                Debug.WriteLine($"[SpotPicker] SetWindowsHookEx(keyboard) failed: {Marshal.GetLastWin32Error()}");
        }

        // =====================================================================
        // Hook callbacks
        // =====================================================================

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _sessionActive)
            {
                int msg = (int)wParam;
                var hs  = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                // Force crosshair cursor while session is active.
                NativeMethods.SetCursor(NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_CROSS));

                if (msg == NativeMethods.WM_LBUTTONDOWN)
                {
                    _dragStartPx = hs.pt;
                    _isDragging  = true;
                    UpdateSelectionRect(hs.pt, hs.pt);
                    // Suppress: don't let the click reach the window under the cursor.
                    return new IntPtr(1);
                }
                else if (msg == NativeMethods.WM_MOUSEMOVE && _isDragging)
                {
                    UpdateSelectionRect(_dragStartPx, hs.pt);
                    // Suppress mouse-move events while dragging to prevent hover/selection effects.
                    return new IntPtr(1);
                }
                else if (msg == NativeMethods.WM_LBUTTONUP && _isDragging)
                {
                    _isDragging = false;
                    POINT end = hs.pt;

                    double dx = Math.Abs(end.X - _dragStartPx.X);
                    double dy = Math.Abs(end.Y - _dragStartPx.Y);

                    if (dx < MinDragPixels || dy < MinDragPixels)
                    {
                        // Accidental click — hide rect and let user try again.
                        HideSelectionRect();
                    }
                    else
                    {
                        double left = Math.Min(_dragStartPx.X, end.X);
                        double top  = Math.Min(_dragStartPx.Y, end.Y);
                        var drawnBounds = new WpfRect(left, top, dx, dy);

                        // Inspect + resolve on background thread so we don't
                        // block the hook callback.
                        _ = Task.Run(async () =>
                        {
                            var center = new WpfPoint(
                                drawnBounds.X + drawnBounds.Width  / 2.0,
                                drawnBounds.Y + drawnBounds.Height / 2.0);

                            ElementInfo? detected = null;
                            try
                            {
                                detected = await AutomationInspector.InspectAtPoint(center);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[SpotPicker] InspectAtPoint failed: {ex.Message}");
                            }

                            _tcs?.TrySetResult(new SpotResult
                            {
                                DrawnBounds     = drawnBounds,
                                DetectedElement = detected,
                            });
                        });
                    }
                    // Suppress: don't let the button-up reach the window under the cursor.
                    return new IntPtr(1);
                }
                else if (msg == NativeMethods.WM_RBUTTONDOWN)
                {
                    _isDragging = false;
                    _tcs?.TrySetResult(null);
                    // Suppress to avoid triggering a context menu.
                    return new IntPtr(1);
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _sessionActive && (int)wParam == NativeMethods.WM_KEYDOWN)
            {
                var ks = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (ks.vkCode == VK_ESCAPE)
                {
                    _isDragging = false;
                    _tcs?.TrySetResult(null);
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        // =====================================================================
        // Overlay drawing — rubber-band rectangle
        // =====================================================================

        /// <summary>
        /// Converts a pair of physical-pixel screen points to canvas-local DIP
        /// coordinates and updates the selection rectangle on the WPF dispatcher.
        /// </summary>
        private void UpdateSelectionRect(POINT start, POINT current)
        {
            if (WpfApp.Current == null) return;

            WpfApp.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_sessionActive || _selectionRect == null || _overlay == null) return;

                // Get DPI scale so we can convert physical pixels → DIPs.
                double scaleX = 1.0, scaleY = 1.0;
                try
                {
                    var src = PresentationSource.FromVisual(_overlay.Canvas);
                    if (src?.CompositionTarget != null)
                    {
                        scaleX = src.CompositionTarget.TransformToDevice.M11;
                        scaleY = src.CompositionTarget.TransformToDevice.M22;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SpotPicker] DPI lookup failed: {ex.Message}");
                }

                // The canvas origin = virtual screen origin (VirtualScreenLeft, VirtualScreenTop)
                // in DIPs.  Subtract those to get canvas-local coordinates.
                double vsLeft = SystemParameters.VirtualScreenLeft;
                double vsTop  = SystemParameters.VirtualScreenTop;

                double x1 = start.X   / scaleX - vsLeft;
                double y1 = start.Y   / scaleY - vsTop;
                double x2 = current.X / scaleX - vsLeft;
                double y2 = current.Y / scaleY - vsTop;

                double left = Math.Min(x1, x2);
                double top  = Math.Min(y1, y2);
                double w    = Math.Abs(x2 - x1);
                double h    = Math.Abs(y2 - y1);

                System.Windows.Controls.Canvas.SetLeft(_selectionRect, left);
                System.Windows.Controls.Canvas.SetTop(_selectionRect,  top);
                _selectionRect.Width      = Math.Max(w, 1);
                _selectionRect.Height     = Math.Max(h, 1);
                _selectionRect.Visibility = Visibility.Visible;
            }));
        }

        private void HideSelectionRect()
        {
            if (WpfApp.Current == null) return;
            WpfApp.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_selectionRect != null)
                    _selectionRect.Visibility = Visibility.Collapsed;
            }));
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        private void CleanupSession()
        {
            _sessionActive = false;
            _isDragging    = false;

            if (_mouseHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            if (_keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }

            _mouseProcDelegate    = null;
            _keyboardProcDelegate = null;

            if (WpfApp.Current != null)
            {
                try
                {
                    WpfApp.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _selectionRect = null;
                        _overlay?.Close();
                        _overlay = null;
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SpotPicker] CleanupSession dispatcher invoke failed: {ex.Message}");
                }
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static WpfRectangle CreateSelectionRect(AppSettings settings)
        {
            WpfColor strokeColor = WpfColors.DodgerBlue;
            if (!string.IsNullOrWhiteSpace(settings.HighlightColor))
            {
                try
                {
                    strokeColor = (WpfColor)WpfColorConv.ConvertFromString(
                        settings.HighlightColor.StartsWith('#')
                            ? settings.HighlightColor
                            : "#" + settings.HighlightColor)!;
                }
                catch { }
            }

            byte alpha = (byte)Math.Round(settings.HighlightOpacity * 255);

            return new WpfRectangle
            {
                Stroke           = new WpfSolidBrush(strokeColor),
                StrokeThickness  = 2,
                Fill             = new WpfSolidBrush(WpfColor.FromArgb(alpha,
                                        strokeColor.R, strokeColor.G, strokeColor.B)),
                IsHitTestVisible = false,
                Visibility       = Visibility.Collapsed,
            };
        }
    }
}
