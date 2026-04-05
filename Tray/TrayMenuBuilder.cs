using System;
using System.Drawing;
using System.Windows.Forms;
using UIInspector.Session;

namespace UIInspector.Tray
{
    /// <summary>
    /// Responsible for constructing the <see cref="ContextMenuStrip"/> that appears
    /// when the user right-clicks (or left-clicks) the system tray icon.
    ///
    /// When a session is provided and has captured elements, each element appears
    /// as a top-level menu item with a submenu containing:
    ///   - A grayed-out query preview
    ///   - Edit Query
    ///   - View Screenshot
    ///   - Remove
    /// </summary>
    internal static class TrayMenuBuilder
    {
        // Maximum characters to show for an element name in the menu.
        private const int MaxNameLength = 30;

        // =====================================================================
        // Public factory
        // =====================================================================

        /// <summary>
        /// Builds and returns a new <see cref="ContextMenuStrip"/>.
        /// </summary>
        /// <param name="session">
        /// The current inspection session. When non-null and non-empty, each captured
        /// element is listed with a management submenu.
        /// </param>
        /// <param name="onPickElement">Callback for "Pick Element".</param>
        /// <param name="onPickSpot">Callback for "Pick Spot".</param>
        /// <param name="onCopyAll">Callback for "Copy All".</param>
        /// <param name="onClearAll">Callback for "Clear All".</param>
        /// <param name="onSettings">Callback for "Settings".</param>
        /// <param name="onEditQuery">
        /// Callback invoked when the user clicks "Edit Query" in an element submenu.
        /// Receives the element <c>Index</c>.
        /// </param>
        /// <param name="onViewShot">
        /// Callback invoked when the user clicks "View Screenshot".
        /// Receives the element <c>Index</c>.
        /// </param>
        /// <param name="onRemove">
        /// Callback invoked when the user clicks "Remove".
        /// Receives the element <c>Index</c>.
        /// </param>
        public static ContextMenuStrip Build(
            InspectionSession? session,
            EventHandler?      onPickElement  = null,
            EventHandler?      onPickSpot     = null,
            EventHandler?      onCopyAll      = null,
            EventHandler?      onClearAll     = null,
            EventHandler?      onSettings     = null,
            Action<int>?       onCopySingle   = null,
            Action<int>?       onEditQuery    = null,
            Action<int>?       onViewShot     = null,
            Action<int>?       onRemove       = null)
        {
            int  elementCount = session?.Count ?? 0;
            bool hasElements  = elementCount > 0;

            var menu = new ContextMenuStrip
            {
                RenderMode = ToolStripRenderMode.System,
            };

            // -----------------------------------------------------------------
            // "Pick Element    Ctrl+Shift+I"
            // -----------------------------------------------------------------
            Font? baseFont = SystemFonts.MenuFont ?? SystemFonts.DefaultFont;
            var pickItem = new ToolStripMenuItem("Pick Element")
            {
                ShortcutKeyDisplayString = "Ctrl+Shift+I",
                ShowShortcutKeys         = true,
                Enabled                  = true,
                Font                     = new Font(baseFont, FontStyle.Bold),
            };

            if (onPickElement is not null)
                pickItem.Click += onPickElement;

            menu.Items.Add(pickItem);

            // -----------------------------------------------------------------
            // "Pick Spot    Ctrl+Shift+S"
            // -----------------------------------------------------------------
            var pickSpotItem = new ToolStripMenuItem("Pick Spot")
            {
                ShortcutKeyDisplayString = "Ctrl+Shift+S",
                ShowShortcutKeys         = true,
                Enabled                  = true,
                Font                     = new Font(baseFont, FontStyle.Bold),
            };

            if (onPickSpot is not null)
                pickSpotItem.Click += onPickSpot;

            menu.Items.Add(pickSpotItem);

            // -----------------------------------------------------------------
            // Separator
            // -----------------------------------------------------------------
            menu.Items.Add(new ToolStripSeparator());

            // -----------------------------------------------------------------
            // Captured elements section
            // -----------------------------------------------------------------
            if (!hasElements)
            {
                var noItemsLabel = new ToolStripMenuItem("No elements captured")
                {
                    Enabled   = false,
                    ForeColor = SystemColors.GrayText,
                };
                menu.Items.Add(noItemsLabel);
            }
            else
            {
                // List every captured element with a management submenu.
                foreach (CapturedElement element in session!.Elements)
                    menu.Items.Add(BuildElementMenuItem(element, onCopySingle, onEditQuery, onViewShot, onRemove));
            }

            // -----------------------------------------------------------------
            // Separator
            // -----------------------------------------------------------------
            menu.Items.Add(new ToolStripSeparator());

            // -----------------------------------------------------------------
            // "Copy All (n)    Ctrl+Shift+C"
            // -----------------------------------------------------------------
            var copyAllItem = new ToolStripMenuItem($"Copy All ({elementCount})")
            {
                ShortcutKeyDisplayString = "Ctrl+Shift+C",
                ShowShortcutKeys         = true,
                Enabled                  = hasElements,
            };

            if (hasElements && onCopyAll is not null)
                copyAllItem.Click += onCopyAll;

            menu.Items.Add(copyAllItem);

            // -----------------------------------------------------------------
            // "Clear All"
            // -----------------------------------------------------------------
            var clearAllItem = new ToolStripMenuItem("Clear All")
            {
                Enabled   = hasElements,
                ForeColor = hasElements ? Color.FromArgb(200, 0, 0) : SystemColors.GrayText,
            };

            if (hasElements && onClearAll is not null)
                clearAllItem.Click += onClearAll;

            menu.Items.Add(clearAllItem);

            // -----------------------------------------------------------------
            // Separator
            // -----------------------------------------------------------------
            menu.Items.Add(new ToolStripSeparator());

            // -----------------------------------------------------------------
            // "Settings"
            // -----------------------------------------------------------------
            var settingsItem = new ToolStripMenuItem("Settings");

            if (onSettings is not null)
                settingsItem.Click += onSettings;
            else
                settingsItem.Enabled = false;

            menu.Items.Add(settingsItem);

            // -----------------------------------------------------------------
            // "Exit"
            // -----------------------------------------------------------------
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExitClicked;
            menu.Items.Add(exitItem);

            return menu;
        }

        // =====================================================================
        // Element submenu builder
        // =====================================================================

        /// <summary>
        /// Builds a top-level <see cref="ToolStripMenuItem"/> for a single captured
        /// element, with a drop-down submenu for management actions.
        /// </summary>
        private static ToolStripMenuItem BuildElementMenuItem(
            CapturedElement element,
            Action<int>?    onCopySingle,
            Action<int>?    onEditQuery,
            Action<int>?    onViewShot,
            Action<int>?    onRemove)
        {
            // Top-level text: "[1] Button "Submit Order""
            string displayName = TruncateName(element.Name);
            string itemText    = $"[{element.Index}] {element.ControlType} \"{displayName}\"";

            var topItem = new ToolStripMenuItem(itemText);

            // ------------------------------------------------------------------
            // Query preview line (grayed, non-clickable)
            // ------------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(element.Query))
            {
                string preview  = TruncateQuery(element.Query);
                var    queryPreview = new ToolStripMenuItem(preview)
                {
                    Enabled   = false,
                    ForeColor = SystemColors.GrayText,
                };
                topItem.DropDownItems.Add(queryPreview);
                topItem.DropDownItems.Add(new ToolStripSeparator());
            }

            // ------------------------------------------------------------------
            // "Copy"
            // ------------------------------------------------------------------
            int capturedIndex = element.Index;   // Capture for closures.

            var copyItem = new ToolStripMenuItem("Copy");
            copyItem.Click += (_, _) => onCopySingle?.Invoke(capturedIndex);
            topItem.DropDownItems.Add(copyItem);

            // ------------------------------------------------------------------
            // "Edit Query"
            // ------------------------------------------------------------------
            var editItem = new ToolStripMenuItem("Edit Query");
            editItem.Click += (_, _) => onEditQuery?.Invoke(capturedIndex);
            topItem.DropDownItems.Add(editItem);

            // ------------------------------------------------------------------
            // "View Screenshot"
            // ------------------------------------------------------------------
            bool hasShot = !string.IsNullOrWhiteSpace(element.ScreenshotPath);
            var  viewItem = new ToolStripMenuItem("View Screenshot")
            {
                Enabled = hasShot,
            };
            if (hasShot)
                viewItem.Click += (_, _) => onViewShot?.Invoke(capturedIndex);
            topItem.DropDownItems.Add(viewItem);

            // ------------------------------------------------------------------
            // "Remove"
            // ------------------------------------------------------------------
            var removeItem = new ToolStripMenuItem("Remove");
            removeItem.Click += (_, _) => onRemove?.Invoke(capturedIndex);
            topItem.DropDownItems.Add(removeItem);

            return topItem;
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private static void OnExitClicked(object? sender, EventArgs e)
        {
            Application.Exit();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static string TruncateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "(unnamed)";

            // Collapse any whitespace (newlines, tabs, etc.) into single spaces
            // so the menu item never wraps to multiple lines.
            string singleLine = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");

            return singleLine.Length > MaxNameLength ? singleLine[..MaxNameLength] + "..." : singleLine;
        }

        /// <summary>
        /// Returns the first line of <paramref name="query"/>, truncated so it
        /// fits on a single menu item without wrapping.
        /// </summary>
        private static string TruncateQuery(string query)
        {
            const int MaxQueryPreview = 45;

            // Use only the first non-empty line.
            string first = query.Split('\n')[0].Trim();

            return first.Length > MaxQueryPreview
                ? first[..MaxQueryPreview] + "..."
                : first;
        }
    }
}
