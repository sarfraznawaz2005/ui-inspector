using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DrawingColor     = System.Drawing.Color;
using DrawingGraphics  = System.Drawing.Graphics;
using DrawingBitmap    = System.Drawing.Bitmap;
using DrawingPen       = System.Drawing.Pen;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfRect          = System.Windows.Rect;

namespace UIInspector.Inspection
{
    /// <summary>
    /// Captures a screenshot of a UI element's bounding rectangle and saves it as a PNG.
    ///
    /// Design notes:
    ///   - Uses GDI+ (System.Drawing) to copy pixels from the screen — no extra dependencies.
    ///   - Adds 8 px padding around the element rect so surrounding context is visible.
    ///   - Draws a 2 px colored border inside the padded image at the boundary of the element.
    ///   - Filenames are guaranteed unique within a process run via a static sequence counter.
    /// </summary>
    public static class ScreenshotCapture
    {
        // =====================================================================
        // Constants
        // =====================================================================

        private const int PaddingPx     = 8;
        private const int BorderWidthPx = 2;
        private const int MinSizePx     = 10;

        // =====================================================================
        // Sequence counter
        // =====================================================================

        /// <summary>
        /// Monotonically increasing counter used to disambiguate files captured
        /// within the same second. Reset to 0 at each new timestamp prefix.
        /// </summary>
        private static int         _seqCounter;
        private static string      _lastTimestampPrefix = string.Empty;
        private static readonly object _seqLock = new();

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Captures the area of the screen described by <paramref name="bounds"/>,
        /// draws a highlight border, saves the image as a PNG, and returns the
        /// absolute path of the saved file.
        /// </summary>
        /// <param name="bounds">The element's bounding rectangle in screen coordinates.</param>
        /// <param name="screenshotFolder">Directory where the PNG file will be written.</param>
        /// <param name="highlightColor">
        /// Optional HTML hex color string for the border (e.g. "#4488FF").
        /// Defaults to "#4488FF" when null or invalid.
        /// </param>
        /// <returns>Absolute path of the saved PNG file.</returns>
        public static string CaptureElement(
            WpfRect bounds,
            string  screenshotFolder,
            string? highlightColor = null)
        {
            // ------------------------------------------------------------------
            // 1. Resolve the capture rectangle (padded, clipped to virtual screen)
            // ------------------------------------------------------------------
            int screenLeft   = SystemInformation_VirtualScreenLeft();
            int screenTop    = SystemInformation_VirtualScreenTop();
            int screenWidth  = SystemInformation_VirtualScreenWidth();
            int screenRight  = screenLeft + screenWidth;
            int screenHeight = SystemInformation_VirtualScreenHeight();
            int screenBottom = screenTop  + screenHeight;

            // Raw element rect (as integer pixels).
            int elemLeft   = (int)Math.Round(bounds.Left);
            int elemTop    = (int)Math.Round(bounds.Top);
            int elemWidth  = Math.Max((int)Math.Round(bounds.Width),  MinSizePx);
            int elemHeight = Math.Max((int)Math.Round(bounds.Height), MinSizePx);

            // Padded capture rect.
            int captureLeft   = elemLeft - PaddingPx;
            int captureTop    = elemTop  - PaddingPx;
            int captureRight  = elemLeft + elemWidth  + PaddingPx;
            int captureBottom = elemTop  + elemHeight + PaddingPx;

            // Clip to virtual screen.
            captureLeft   = Math.Max(captureLeft,   screenLeft);
            captureTop    = Math.Max(captureTop,    screenTop);
            captureRight  = Math.Min(captureRight,  screenRight);
            captureBottom = Math.Min(captureBottom, screenBottom);

            int captureWidth  = Math.Max(captureRight  - captureLeft, MinSizePx);
            int captureHeight = Math.Max(captureBottom - captureTop,  MinSizePx);

            // ------------------------------------------------------------------
            // 2. Capture from screen using GDI+
            // ------------------------------------------------------------------
            using var bitmap = new DrawingBitmap(captureWidth, captureHeight);
            using (DrawingGraphics g = DrawingGraphics.FromImage(bitmap))
            {
                g.CopyFromScreen(captureLeft, captureTop, 0, 0,
                    new System.Drawing.Size(captureWidth, captureHeight));
            }

            // ------------------------------------------------------------------
            // 3. Draw the 2 px highlight border around the element area
            //    within the captured image coordinate space.
            // ------------------------------------------------------------------
            DrawingColor borderColor = ParseColor(highlightColor, DrawingColor.FromArgb(0x44, 0x88, 0xFF));

            // Element rect in capture-local coordinates.
            int localElemLeft = elemLeft - captureLeft;
            int localElemTop  = elemTop  - captureTop;

            // Clamp so the border fits entirely within the bitmap.
            int borderLeft   = Math.Max(localElemLeft,                         0);
            int borderTop    = Math.Max(localElemTop,                          0);
            int borderRight  = Math.Min(localElemLeft + elemWidth  - 1, captureWidth  - 1);
            int borderBottom = Math.Min(localElemTop  + elemHeight - 1, captureHeight - 1);
            int borderWidth  = Math.Max(borderRight  - borderLeft + 1, 1);
            int borderHeight = Math.Max(borderBottom - borderTop  + 1, 1);

            using (DrawingGraphics g = DrawingGraphics.FromImage(bitmap))
            using (var pen = new DrawingPen(borderColor, BorderWidthPx))
            {
                // DrawRectangle draws at the outer edge of the pen width; nudge
                // inward by half the pen width so all pixels fall inside the bitmap.
                int inset = BorderWidthPx / 2;
                g.DrawRectangle(pen,
                    new DrawingRectangle(
                        borderLeft   + inset,
                        borderTop    + inset,
                        Math.Max(borderWidth  - BorderWidthPx, 1),
                        Math.Max(borderHeight - BorderWidthPx, 1)));
            }

            // ------------------------------------------------------------------
            // 4. Save as PNG
            // ------------------------------------------------------------------
            EnsureFolder(screenshotFolder);

            string filename = BuildFilename();
            string fullPath = Path.Combine(screenshotFolder, filename);

            bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);

            Debug.WriteLine($"[ScreenshotCapture] Saved {fullPath}");
            return Path.GetFullPath(fullPath);
        }

        /// <summary>
        /// Deletes PNG files in <paramref name="folder"/> that are older than
        /// <paramref name="maxAgeHours"/> hours.
        /// </summary>
        /// <param name="folder">The folder to clean.</param>
        /// <param name="maxAgeHours">Age threshold in hours.</param>
        public static void CleanupOldScreenshots(string folder, int maxAgeHours)
        {
            if (!Directory.Exists(folder))
                return;

            DateTime cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);

            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "elem-*.png"))
                {
                    try
                    {
                        DateTime lastWrite = File.GetLastWriteTimeUtc(file);
                        if (lastWrite < cutoff)
                        {
                            File.Delete(file);
                            Debug.WriteLine($"[ScreenshotCapture] Deleted old screenshot: {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ScreenshotCapture] Could not delete {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenshotCapture] CleanupOldScreenshots failed: {ex.Message}");
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>
        /// Builds a filename of the form <c>elem-{yyyyMMdd-HHmmss}-{seq:D3}.png</c>.
        /// The sequence counter resets when the timestamp prefix changes (i.e. new second).
        /// </summary>
        private static string BuildFilename()
        {
            string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            int seq;

            lock (_seqLock)
            {
                if (ts != _lastTimestampPrefix)
                {
                    _lastTimestampPrefix = ts;
                    _seqCounter = 0;
                }

                seq = ++_seqCounter;
            }

            return $"elem-{ts}-{seq:D3}.png";
        }

        /// <summary>Creates the folder if it does not already exist.</summary>
        private static void EnsureFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                Debug.WriteLine($"[ScreenshotCapture] Created screenshot folder: {folder}");
            }
        }

        /// <summary>
        /// Parses an HTML hex color string such as "#4488FF" or "4488FF".
        /// Returns <paramref name="fallback"/> when the string is null, empty,
        /// or otherwise unparseable.
        /// </summary>
        private static DrawingColor ParseColor(string? hex, DrawingColor fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            try
            {
                // System.Drawing.ColorTranslator understands "#RRGGBB" and "RRGGBB".
                return System.Drawing.ColorTranslator.FromHtml(
                    hex.StartsWith('#') ? hex : "#" + hex);
            }
            catch
            {
                return fallback;
            }
        }

        // ------------------------------------------------------------------
        // Virtual-screen helpers — thin wrappers around SystemInformation so
        // they can be tested or swapped without PInvoke.
        // ------------------------------------------------------------------

        private static int SystemInformation_VirtualScreenLeft()   => System.Windows.Forms.SystemInformation.VirtualScreen.Left;
        private static int SystemInformation_VirtualScreenTop()    => System.Windows.Forms.SystemInformation.VirtualScreen.Top;
        private static int SystemInformation_VirtualScreenWidth()  => System.Windows.Forms.SystemInformation.VirtualScreen.Width;
        private static int SystemInformation_VirtualScreenHeight() => System.Windows.Forms.SystemInformation.VirtualScreen.Height;
    }
}
