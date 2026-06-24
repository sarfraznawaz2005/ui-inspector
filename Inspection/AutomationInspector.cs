using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using UIInspector.Interop;
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
        // Cached once at startup — used to skip UIA inspection of our own windows.
        private static readonly int _ownProcessId = Process.GetCurrentProcess().Id;

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
            // Inspecting our own process via UIA causes cross-thread deadlocks.
            // Use WindowFromPoint (fast, no UIA) to detect this before committing.
            var winPoint = new POINT { X = (int)screenPoint.X, Y = (int)screenPoint.Y };
            IntPtr hwnd = NativeMethods.WindowFromPoint(winPoint);
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowPid);
                if ((int)windowPid == _ownProcessId)
                    return null;
            }

            // AutomationElement.FromPoint is a synchronous, non-cancellable COM call.
            // RunWithTimeout runs it on a background thread and stops waiting if it
            // wedges against an unresponsive target, returning null instead of hanging.
            return await RunWithTimeout(
                () =>
                {
                    AutomationElement? element = AutomationElement.FromPoint(
                        new WpfPoint(screenPoint.X, screenPoint.Y));

                    return element == null ? null : ExtractElementInfo(element);
                },
                fallback: null,
                timeout: TimeSpan.FromSeconds(2),
                label: $"InspectAtPoint{screenPoint}");
        }

        /// <summary>
        /// Runs a synchronous, potentially-blocking UI Automation operation on a
        /// background thread and races it against <paramref name="timeout"/>.
        ///
        /// UIA calls (tree walks, property reads, <c>FromPoint</c>) are blocking COM
        /// calls that ignore <see cref="CancellationToken"/>s, so once one is in
        /// progress it cannot be interrupted. On a slow or memory-pressured machine,
        /// or against an unresponsive target process, such a call can block for a long
        /// time. The only way to guarantee the caller is never wedged is to stop
        /// waiting on it: if the work does not finish in time it is abandoned (left to
        /// complete or die with the process) and <paramref name="fallback"/> is returned.
        /// </summary>
        public static async Task<T> RunWithTimeout<T>(
            Func<T> work, T fallback, TimeSpan timeout, string label)
        {
            Task<T> task;
            try
            {
                task = Task.Run(work);
            }
            catch (Exception ex)
            {
                // Task.Run itself can throw under severe resource pressure (e.g. the
                // thread pool cannot start a thread when low on memory).
                Debug.WriteLine($"[AutomationInspector] {label} could not start: {ex.Message}");
                return fallback;
            }

            Task finished = await Task.WhenAny(task, Task.Delay(timeout));

            if (finished != task)
            {
                // Observe the abandoned task's eventual exception so it is not raised
                // as an unobserved task exception later.
                _ = task.ContinueWith(
                    t => { _ = t.Exception; },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);

                Debug.WriteLine($"[AutomationInspector] {label} timed out after {timeout.TotalSeconds:0.#}s; using fallback.");
                return fallback;
            }

            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutomationInspector] {label} failed: {ex.Message}");
                return fallback;
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
