using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UIInspector.Tray
{
    /// <summary>
    /// Generates tray icons programmatically at runtime, eliminating the need
    /// for embedded .ico file resources during early development.
    ///
    /// Icon design:
    ///   Idle   — dark-gray background, white magnifying glass, "UI" label
    ///   Active — indigo background, white magnifying glass, small green status dot
    /// </summary>
    internal static class IconGenerator
    {
        // Size of the icon surface in pixels (matches Windows tray icon dimensions).
        private const int IconSize = 16;

        // =====================================================================
        // Public factory methods
        // =====================================================================

        /// <summary>
        /// Creates the idle tray icon (dark-gray / blue-gray colour scheme).
        /// The caller is responsible for disposing the returned <see cref="Icon"/>.
        /// </summary>
        public static Icon CreateIdleIcon() =>
            CreateIcon(
                backgroundColorTop:    Color.FromArgb(255, 55,  65,  81),   // slate-700
                backgroundColorBottom: Color.FromArgb(255, 31,  41,  55),   // slate-800
                glassColor:            Color.FromArgb(200, 100, 149, 237),   // cornflower-blue
                handleColor:           Color.FromArgb(255, 148, 163, 184),   // slate-400
                showActiveDot:         false);

        /// <summary>
        /// Creates the active tray icon (indigo background with green status dot).
        /// The caller is responsible for disposing the returned <see cref="Icon"/>.
        /// </summary>
        public static Icon CreateActiveIcon() =>
            CreateIcon(
                backgroundColorTop:    Color.FromArgb(255, 79,  70, 229),   // indigo-600
                backgroundColorBottom: Color.FromArgb(255, 55,  48, 163),   // indigo-800
                glassColor:            Color.FromArgb(220, 255, 255, 255),   // white (semi)
                handleColor:           Color.FromArgb(255, 199, 210, 254),   // indigo-200
                showActiveDot:         true);

        /// <summary>
        /// Builds a tray icon that shows the captured-element <paramref name="count"/>
        /// as a large centred number, with the <paramref name="baseIcon"/> shrunk to a
        /// small mark in the top-left corner. The big number is what stays legible at
        /// the 16px tray size; the logo is kept only for brand recognition.
        ///
        /// The digits are rendered as a stroked glyph path (dark outline + white fill)
        /// so they read clearly over the logo and any background. Renders at 32×32 —
        /// Windows downscales for the tray. The caller disposes the returned icon.
        /// </summary>
        public static Icon CreateBadgedIcon(Icon baseIcon, int count)
        {
            // Render surface larger than the 16px tray slot so the number stays
            // crisp after the OS scales it down.
            const int size = 32;

            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // -----------------------------------------------------------------
            // Logo — shrunk to a small mark in the top-left corner
            // -----------------------------------------------------------------
            using (var baseBitmap = baseIcon.ToBitmap())
            {
                g.DrawImage(baseBitmap, new Rectangle(0, 0, 13, 13));
            }

            // -----------------------------------------------------------------
            // Count — large, centred, drawn as an outlined glyph path
            // -----------------------------------------------------------------
            string text = count > 99 ? "99+" : count.ToString();

            // Shrink the em size as digit count grows so it always fits the tile.
            float emSize = text.Length switch
            {
                1 => 30f,
                2 => 22f,
                _ => 16f,
            };

            using var fontFamily = new FontFamily("Segoe UI");
            using var path = new GraphicsPath();

            // Emit the glyphs at the origin (no layout rectangle — a rectangle plus
            // GenericTypographic's LineLimit flag silently drops lines taller than
            // the box), then centre by the path's actual bounds.
            path.AddString(
                text,
                fontFamily,
                (int)FontStyle.Bold,
                emSize,
                PointF.Empty,
                StringFormat.GenericTypographic);

            RectangleF bounds = path.GetBounds();
            using (var center = new Matrix())
            {
                center.Translate(
                    (size - bounds.Width)  / 2f - bounds.X,
                    (size - bounds.Height) / 2f - bounds.Y);
                path.Transform(center);
            }

            // White outline first (rounded joins avoid spiky corners) so the blue
            // digits stay readable on a light taskbar, blue fill on top.
            using (var outline = new Pen(Color.FromArgb(235, 255, 255, 255), 3.5f) { LineJoin = LineJoin.Round })
                g.DrawPath(outline, path);
            using (var fill = new SolidBrush(Color.FromArgb(255, 37, 99, 235)))   // blue-600
                g.FillPath(fill, path);

            // -----------------------------------------------------------------
            // Convert Bitmap → Icon via HICON handle
            // -----------------------------------------------------------------
            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            NativeMethods.DestroyIcon(hIcon);

            return icon;
        }

        // =====================================================================
        // Core rendering
        // =====================================================================

        private static Icon CreateIcon(
            Color backgroundColorTop,
            Color backgroundColorBottom,
            Color glassColor,
            Color handleColor,
            bool  showActiveDot)
        {
            using var bitmap = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

            // -----------------------------------------------------------------
            // Background — rounded rectangle with vertical gradient
            // -----------------------------------------------------------------
            using (var bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, IconSize, IconSize),
                backgroundColorTop,
                backgroundColorBottom,
                LinearGradientMode.Vertical))
            {
                using var path = RoundedRect(new RectangleF(0.5f, 0.5f, IconSize - 1, IconSize - 1), 2.5f);
                g.FillPath(bgBrush, path);
            }

            // -----------------------------------------------------------------
            // Magnifying glass — circular lens
            // -----------------------------------------------------------------
            // Lens circle: centred in the upper-left quadrant of the icon.
            var lensRect = new RectangleF(2.0f, 2.0f, 8.0f, 8.0f);

            using (var lensPen = new Pen(glassColor, 1.5f))
            {
                g.DrawEllipse(lensPen, lensRect);
            }

            // Lens interior highlight (semi-transparent white to simulate glass)
            using (var lensBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            {
                g.FillEllipse(lensBrush, lensRect);
            }

            // -----------------------------------------------------------------
            // Magnifying glass — handle (diagonal line from bottom-right of lens)
            // -----------------------------------------------------------------
            using var handlePen = new Pen(handleColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(handlePen, 9.5f, 9.5f, 13.0f, 13.0f);

            // -----------------------------------------------------------------
            // Active status dot (green, bottom-right corner)
            // -----------------------------------------------------------------
            if (showActiveDot)
            {
                var dotRect = new RectangleF(10.0f, 10.0f, 5.0f, 5.0f);

                // Dark outline for contrast against any background
                using (var outlineBrush = new SolidBrush(Color.FromArgb(255, 15, 30, 15)))
                    g.FillEllipse(outlineBrush, new RectangleF(9.5f, 9.5f, 6.0f, 6.0f));

                using (var dotBrush = new LinearGradientBrush(
                    dotRect,
                    Color.FromArgb(255, 134, 239, 172),  // green-300
                    Color.FromArgb(255,  34, 197,  94),  // green-500
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(dotBrush, dotRect);
                }
            }

            // -----------------------------------------------------------------
            // Convert Bitmap → Icon via HICON handle
            // -----------------------------------------------------------------
            IntPtr hIcon = bitmap.GetHicon();
            // Clone immediately — GetHicon transfers ownership, so we need our own copy.
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            NativeMethods.DestroyIcon(hIcon);

            return icon;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Builds a <see cref="GraphicsPath"/> for a rectangle with uniformly
        /// rounded corners of the given <paramref name="radius"/>.
        /// </summary>
        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            var path = new GraphicsPath();

            // Top-left
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            // Top-right
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            // Bottom-right
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            // Bottom-left
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);

            path.CloseFigure();
            return path;
        }

        // =====================================================================
        // Minimal P/Invoke to free HICON produced by GetHicon()
        // =====================================================================
        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool DestroyIcon(IntPtr handle);
        }
    }
}
