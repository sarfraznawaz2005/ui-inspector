using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UIInspector.Session
{
    /// <summary>
    /// Builds a markdown report from elements in an <see cref="InspectionSession"/>
    /// and copies it to the system clipboard.
    /// </summary>
    public static class ClipboardExporter
    {
        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Builds a markdown report from all elements in the session and copies
        /// it to the clipboard.
        /// </summary>
        public static int ExportToClipboard(InspectionSession session)
        {
            if (session.Count == 0) return 0;

            var sb = new StringBuilder();

            string elemWord = session.Count == 1 ? "element" : "elements";
            sb.AppendLine($"# UI Details ({session.Count} {elemWord})");

            string processNames = string.Join(
                ", ",
                session.Elements.Select(e => e.ProcessName).Distinct());

            sb.AppendLine(
                $"**App**: {processNames} | **Captured**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var elem in session.Elements)
                AppendElement(sb, elem);

            SetClipboardWithRetry(sb.ToString());
            return session.Count;
        }

        /// <summary>
        /// Builds a markdown report for a single element and copies it to the clipboard.
        /// </summary>
        public static void ExportSingleToClipboard(CapturedElement element)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# UI Details (1 element)");
            sb.AppendLine(
                $"**App**: {element.ProcessName} | **Captured**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            AppendElement(sb, element);

            SetClipboardWithRetry(sb.ToString());
        }

        // =====================================================================
        // Private — element formatting
        // =====================================================================

        private static void AppendElement(StringBuilder sb, CapturedElement elem)
        {
            string displayName = TruncateName(elem.Name);

            sb.AppendLine(
                $"## Element {elem.Index}: {elem.ControlType} \"{displayName}\"");

            sb.AppendLine($"- **Type**: {elem.ControlType}");
            sb.AppendLine($"- **Name**: \"{displayName}\"");
            sb.AppendLine(
                $"- **AutomationId**: {(string.IsNullOrEmpty(elem.AutomationId) ? "(none)" : elem.AutomationId)}");
            sb.AppendLine(
                $"- **ClassName**: {(string.IsNullOrEmpty(elem.ClassName) ? "(none)" : elem.ClassName)}");
            sb.AppendLine($"- **Selector**: `{elem.Selector}`");

            string enabled = elem.IsEnabled ? "true" : "false";
            string visible = elem.IsOffscreen ? "false" : "true";
            sb.AppendLine($"- **State**: Enabled={enabled}, Visible={visible}");

            sb.AppendLine(
                $"- **Bounds**: x:{elem.BoundingRectangle.X:F0} y:{elem.BoundingRectangle.Y:F0} " +
                $"w:{elem.BoundingRectangle.Width:F0} h:{elem.BoundingRectangle.Height:F0}");
            sb.AppendLine($"- **Process**: {elem.ProcessName} ({elem.ProcessType})");
            sb.AppendLine($"- **Screenshot**: `{elem.ScreenshotPath}`");

            if (!string.IsNullOrEmpty(elem.ParentDescription))
                sb.AppendLine($"- **Parent**: {elem.ParentDescription}");

            if (elem.SiblingDescriptions.Count > 0)
            {
                string sibs = string.Join(", ", elem.SiblingDescriptions.Take(5));
                sb.AppendLine(
                    $"- **Siblings**: {elem.SiblingDescriptions.Count} ({sibs})");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(elem.Query))
                sb.AppendLine($"User Query: See screenshot `{elem.ScreenshotPath}` - {elem.Query}");
            else
                sb.AppendLine($"User Query: See screenshot `{elem.ScreenshotPath}`");

            sb.AppendLine();
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private static void SetClipboardWithRetry(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                Debug.WriteLine(
                    $"[ClipboardExporter] Clipboard locked, retrying in 500 ms: {ex.Message}");
                Thread.Sleep(500);
                Clipboard.SetText(text);
            }
        }

        private static string TruncateName(string name)
        {
            const int MaxLen = 60;
            if (string.IsNullOrWhiteSpace(name)) return "(unnamed)";
            return name.Length > MaxLen ? name[..MaxLen] + "..." : name;
        }
    }
}
