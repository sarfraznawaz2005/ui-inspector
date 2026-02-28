using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UIInspector.Settings;
using WpfColor     = System.Windows.Media.Color;
using WpfPoint     = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace UIInspector.Picker
{
    /// <summary>
    /// Draws a highlight rectangle on the <see cref="OverlayWindow"/>'s canvas
    /// around the UI element that is currently under the cursor.
    ///
    /// A single <see cref="Rectangle"/> instance is reused on every call to
    /// <see cref="Highlight"/> to avoid per-frame object allocation and the
    /// associated GC pressure during the picking session.
    /// </summary>
    public sealed class ElementHighlighter
    {
        // =====================================================================
        // Fields
        // =====================================================================

        private readonly Canvas _canvas;

        // Single reusable rectangle — created lazily on first Highlight() call.
        private WpfRectangle? _rect;

        // =====================================================================
        // Constructor
        // =====================================================================

        /// <summary>
        /// Creates an <see cref="ElementHighlighter"/> that draws on
        /// <paramref name="canvas"/>.
        /// </summary>
        public ElementHighlighter(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Moves/resizes the highlight rectangle to cover <paramref name="screenBounds"/>.
        ///
        /// <paramref name="screenBounds"/> is in physical pixel coordinates (from UIA).
        /// The overlay canvas uses WPF device-independent pixels (DIPs). This method
        /// converts physical → DIP by dividing by the DPI scale factor, then subtracts
        /// the virtual screen origin (also in DIPs) so the rectangle aligns correctly
        /// on all monitors and DPI settings.
        /// </summary>
        public void Highlight(Rect screenBounds, AppSettings settings)
        {
            if (_rect == null)
            {
                _rect = CreateRectangle(settings);
                _canvas.Children.Add(_rect);
            }
            else
            {
                // Refresh colors in case settings changed mid-session.
                ApplyColors(_rect, settings);
            }

            // Get the DPI scale factor from the canvas's presentation source.
            // On a 150% display this returns 1.5, on 100% it returns 1.0.
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            var source = PresentationSource.FromVisual(_canvas);
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Convert physical pixel coordinates → DIPs.
            double dipLeft   = screenBounds.Left   / dpiScaleX;
            double dipTop    = screenBounds.Top    / dpiScaleY;
            double dipWidth  = screenBounds.Width  / dpiScaleX;
            double dipHeight = screenBounds.Height / dpiScaleY;

            // Subtract the virtual screen origin (already in DIPs) to get
            // canvas-relative coordinates.
            double canvasLeft = dipLeft - SystemParameters.VirtualScreenLeft;
            double canvasTop  = dipTop  - SystemParameters.VirtualScreenTop;

            Canvas.SetLeft(_rect, canvasLeft);
            Canvas.SetTop(_rect, canvasTop);

            _rect.Width  = Math.Max(dipWidth,  1);
            _rect.Height = Math.Max(dipHeight, 1);
            _rect.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the highlight rectangle without removing it from the canvas,
        /// so it can be immediately shown again on the next <see cref="Highlight"/> call.
        /// </summary>
        public void Clear()
        {
            if (_rect != null)
                _rect.Visibility = Visibility.Hidden;
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>
        /// Creates a new <see cref="Rectangle"/> styled with the current settings.
        /// </summary>
        private static WpfRectangle CreateRectangle(AppSettings settings)
        {
            var rect = new WpfRectangle
            {
                IsHitTestVisible = false,   // must not interfere with click-through
                Visibility       = Visibility.Hidden,
                StrokeThickness  = 2,
            };
            ApplyColors(rect, settings);
            return rect;
        }

        /// <summary>
        /// Parses <see cref="AppSettings.HighlightColor"/> and
        /// <see cref="AppSettings.HighlightOpacity"/> and applies them to
        /// <paramref name="rect"/>.
        /// </summary>
        private static void ApplyColors(WpfRectangle rect, AppSettings settings)
        {
            WpfColor baseColor = ParseColor(settings.HighlightColor);

            // Stroke: fully opaque version of the highlight color.
            rect.Stroke = new SolidColorBrush(baseColor);

            // Fill: same color with configurable alpha.
            byte alpha = (byte)Math.Clamp(settings.HighlightOpacity * 255.0, 0, 255);
            WpfColor fillColor = WpfColor.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
            rect.Fill = new SolidColorBrush(fillColor);
        }

        /// <summary>
        /// Parses a hex color string such as "#4488FF" or "4488FF".
        /// Falls back to a default blue if parsing fails.
        /// </summary>
        private static WpfColor ParseColor(string hex)
        {
            try
            {
                // ColorConverter accepts "#RRGGBB", "#AARRGGBB", named colors, etc.
                object? converted = System.Windows.Media.ColorConverter.ConvertFromString(hex);
                if (converted is WpfColor c)
                    return c;
            }
            catch
            {
                // Fall through to default.
            }

            return WpfColor.FromRgb(0x44, 0x88, 0xFF);
        }
    }
}
