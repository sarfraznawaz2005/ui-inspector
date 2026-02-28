using System.IO;

namespace UIInspector.Settings
{
    /// <summary>
    /// Holds all user-configurable settings for UI Inspector.
    /// Defaults are defined here and are used when no settings file exists yet.
    /// </summary>
    public class AppSettings
    {
        // =====================================================================
        // Hot-key bindings
        // =====================================================================

        /// <summary>
        /// Hot-key string that triggers the element-picker mode.
        /// </summary>
        public string PickHotkey { get; set; } = "Ctrl+Shift+I";

        /// <summary>
        /// Hot-key string that copies all captured elements to the clipboard.
        /// </summary>
        public string CopyHotkey { get; set; } = "Ctrl+Shift+C";

        /// <summary>
        /// When true, element details are automatically copied to the clipboard
        /// each time an element is picked.
        /// </summary>
        public bool AutoCopy { get; set; } = true;

        /// <summary>
        /// When true and <see cref="AutoCopy"/> is also true, previous session
        /// entries are cleared before the newly picked element is copied.
        /// </summary>
        public bool AutoClearBeforeCopy { get; set; } = false;

        // =====================================================================
        // Screenshot storage
        // =====================================================================

        /// <summary>
        /// Folder where element screenshot images are written.
        /// Defaults to a subdirectory inside the system temp folder.
        /// </summary>
        public string ScreenshotFolder { get; set; } =
            Path.Combine(Path.GetTempPath(), "ui-inspector");

        /// <summary>
        /// When true, screenshots older than <see cref="CleanAfterHours"/> are
        /// automatically deleted on startup.
        /// </summary>
        public bool AutoCleanScreenshots { get; set; } = true;

        /// <summary>
        /// Age threshold (in hours) used by the auto-clean routine.
        /// Screenshots older than this value are removed.
        /// </summary>
        public int CleanAfterHours { get; set; } = 24;

        // =====================================================================
        // Startup behaviour
        // =====================================================================

        /// <summary>
        /// When true, a registry run-key is created so UI Inspector launches
        /// automatically when the user logs in to Windows.
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        // =====================================================================
        // Visual / overlay options
        // =====================================================================

        /// <summary>
        /// HTML hex color used for the highlight overlay drawn around the
        /// element currently under the cursor (e.g. "#4488FF").
        /// </summary>
        public string HighlightColor { get; set; } = "#4488FF";

        /// <summary>
        /// Opacity (0.0 – 1.0) of the highlight overlay fill.
        /// The border is always fully opaque.
        /// </summary>
        public double HighlightOpacity { get; set; } = 0.3;
    }
}
