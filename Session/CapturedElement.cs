using System;
using System.Collections.Generic;
using UIInspector.Inspection;
using WpfRect = System.Windows.Rect;

namespace UIInspector.Session
{
    /// <summary>
    /// A snapshot of a single UI element captured during an inspection session.
    ///
    /// All data is plain CLR properties so the object is easy to serialize,
    /// compare, and display in the tray menu.
    /// </summary>
    public class CapturedElement
    {
        // =====================================================================
        // Identity
        // =====================================================================

        /// <summary>1-based position in the session list (assigned by InspectionSession).</summary>
        public int Index { get; set; }

        // =====================================================================
        // UIA properties
        // =====================================================================

        public string ControlType  { get; set; } = "";
        public string Name         { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ClassName    { get; set; } = "";

        // =====================================================================
        // Selector
        // =====================================================================

        /// <summary>CSS-like selector path built by SelectorBuilder.</summary>
        public string Selector { get; set; } = "";

        // =====================================================================
        // State flags
        // =====================================================================

        public bool IsEnabled   { get; set; }
        public bool IsOffscreen { get; set; }

        // =====================================================================
        // Geometry
        // =====================================================================

        public WpfRect BoundingRectangle { get; set; }

        // =====================================================================
        // Process
        // =====================================================================

        public string      ProcessName { get; set; } = "";
        public ProcessType ProcessType { get; set; }

        // =====================================================================
        // Screenshot
        // =====================================================================

        /// <summary>Absolute path to the PNG file captured for this element.</summary>
        public string ScreenshotPath { get; set; } = "";

        // =====================================================================
        // User-supplied annotation
        // =====================================================================

        /// <summary>
        /// Free-text query or description entered by the user in QueryDialog.
        /// Empty string when the user skipped the dialog.
        /// </summary>
        public string Query { get; set; } = "";

        // =====================================================================
        // Context from the automation tree
        // =====================================================================

        /// <summary>A single-line description of the element's immediate parent.</summary>
        public string ParentDescription { get; set; } = "";

        /// <summary>Single-line descriptions of the element's siblings (up to 10).</summary>
        public List<string> SiblingDescriptions { get; set; } = new();

        // =====================================================================
        // Timing
        // =====================================================================

        /// <summary>UTC timestamp when this element was added to the session.</summary>
        public DateTime CapturedAt { get; set; }
    }
}
