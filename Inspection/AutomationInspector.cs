using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using WpfPoint = System.Windows.Point;
using WpfRect  = System.Windows.Rect;

namespace UIInspector.Inspection
{
    /// <summary>
    /// Inspects the UI element at a given screen coordinate using UI Automation.
    ///
    /// UIA calls (particularly <see cref="AutomationElement.FromPoint"/>) can
    /// occasionally block for extended periods when targeting unresponsive processes.
    /// <see cref="InspectAtPoint"/> therefore runs the UIA call on a background thread
    /// with a 2-second timeout so the picker's UI thread stays responsive.
    /// </summary>
    public static class AutomationInspector
    {
        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Returns an <see cref="ElementInfo"/> describing the topmost UI element
        /// at <paramref name="screenPoint"/>, or <see langword="null"/> if:
        /// <list type="bullet">
        ///   <item>No element is found at that location.</item>
        ///   <item>The UIA call times out (2 seconds).</item>
        ///   <item>The target element is no longer available.</item>
        ///   <item>Any other exception occurs.</item>
        /// </list>
        /// </summary>
        /// <param name="screenPoint">
        /// The point in screen (physical pixel) coordinates to inspect.
        /// </param>
        public static async Task<ElementInfo?> InspectAtPoint(WpfPoint screenPoint)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            CancellationToken token = cts.Token;

            try
            {
                ElementInfo? result = await Task.Run(() =>
                {
                    // Check for cancellation before starting the potentially
                    // blocking UIA call.
                    token.ThrowIfCancellationRequested();

                    AutomationElement? element = AutomationElement.FromPoint(
                        new WpfPoint(screenPoint.X, screenPoint.Y));

                    token.ThrowIfCancellationRequested();

                    if (element == null)
                        return null;

                    return ExtractElementInfo(element);

                }, token);

                return result;
            }
            catch (ElementNotAvailableException ex)
            {
                Debug.WriteLine($"[AutomationInspector] Element not available at {screenPoint}: {ex.Message}");
                return null;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[AutomationInspector] UIA call timed out at {screenPoint}.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutomationInspector] Unexpected error at {screenPoint}: {ex}");
                return null;
            }
        }

        // =====================================================================
        // Internal helpers (also used by SelectorBuilder)
        // =====================================================================

        /// <summary>
        /// Extracts all relevant UIA properties from <paramref name="element"/>
        /// into an <see cref="ElementInfo"/> record.
        ///
        /// Every property access is individually guarded so that a single
        /// unavailable property does not prevent the rest from being read.
        /// </summary>
        internal static ElementInfo? ExtractElementInfo(AutomationElement element)
        {
            try
            {
                string automationId          = GetStringProperty(element, AutomationElement.AutomationIdProperty);
                string name                  = GetStringProperty(element, AutomationElement.NameProperty);

                // Many modern UI frameworks (WPF, WinUI) use composite button templates
                // where Content is a panel with icon + text rather than a plain string,
                // so UIA's Name comes back empty. Walk immediate children to find text.
                if (string.IsNullOrWhiteSpace(name))
                    name = TryGetNameFromChildren(element);

                string className             = GetStringProperty(element, AutomationElement.ClassNameProperty);
                string localizedControlType  = GetStringProperty(element, AutomationElement.LocalizedControlTypeProperty);
                bool   isEnabled             = GetBoolProperty(element, AutomationElement.IsEnabledProperty);
                bool   isOffscreen           = GetBoolProperty(element, AutomationElement.IsOffscreenProperty);

                // ControlType
                string controlType = "Unknown";
                try
                {
                    var ct = element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty) as ControlType;
                    controlType = ct?.ProgrammaticName?.Replace("ControlType.", string.Empty) ?? "Unknown";
                }
                catch { /* keep default */ }

                // BoundingRectangle
                WpfRect bounds = WpfRect.Empty;
                try
                {
                    var rawRect = element.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
                    if (rawRect is WpfRect r && !r.IsEmpty)
                        bounds = r;
                }
                catch { /* keep empty */ }

                // ProcessId
                int processId = 0;
                try
                {
                    processId = (int)element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty);
                }
                catch { /* keep 0 */ }

                // ProcessName (derived from ProcessId)
                string processName = GetProcessName(processId);

                return new ElementInfo(
                    AutomationId:        automationId,
                    Name:                name,
                    ControlType:         controlType,
                    ClassName:           className,
                    LocalizedControlType: localizedControlType,
                    IsEnabled:           isEnabled,
                    IsOffscreen:         isOffscreen,
                    BoundingRectangle:   bounds,
                    ProcessId:           processId,
                    ProcessName:         processName,
                    RawElement:          element);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutomationInspector] ExtractElementInfo failed: {ex}");
                return null;
            }
        }

        // =====================================================================
        // Private property-access helpers
        // =====================================================================

        private static string GetStringProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                var value = element.GetCurrentPropertyValue(property);
                return value as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool GetBoolProperty(AutomationElement element, AutomationProperty property)
        {
            try
            {
                var value = element.GetCurrentPropertyValue(property);
                return value is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Walks immediate children (max 2 levels deep) looking for Text/Name values
        /// to use as a fallback name for elements with composite templates.
        /// Returns the first non-empty text found, or empty string.
        /// </summary>
        private static string TryGetNameFromChildren(AutomationElement element)
        {
            try
            {
                var walker = TreeWalker.ControlViewWalker;
                var child = walker.GetFirstChild(element);
                int checked_ = 0;

                while (child != null && checked_ < 20)
                {
                    checked_++;

                    string childName = GetStringProperty(child, AutomationElement.NameProperty);
                    if (!string.IsNullOrWhiteSpace(childName))
                        return childName;

                    // Check one level deeper (e.g. StackPanel > TextBlock)
                    var grandchild = walker.GetFirstChild(child);
                    int innerChecked = 0;
                    while (grandchild != null && innerChecked < 10)
                    {
                        innerChecked++;
                        string gcName = GetStringProperty(grandchild, AutomationElement.NameProperty);
                        if (!string.IsNullOrWhiteSpace(gcName))
                            return gcName;
                        grandchild = walker.GetNextSibling(grandchild);
                    }

                    child = walker.GetNextSibling(child);
                }
            }
            catch
            {
                // Best-effort — don't let child traversal failure block extraction.
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves a process name from its ID.
        /// Returns an empty string if the process is inaccessible (e.g., elevated).
        /// </summary>
        internal static string GetProcessName(int processId)
        {
            if (processId <= 0)
                return string.Empty;

            try
            {
                using Process process = Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
