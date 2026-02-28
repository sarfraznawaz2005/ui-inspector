using System.Windows;
using System.Windows.Automation;

namespace UIInspector.Inspection
{
    /// <summary>
    /// Immutable snapshot of a UI element's automation properties captured at
    /// the moment the user clicked on it.
    ///
    /// The <see cref="RawElement"/> reference allows callers to perform additional
    /// UIA queries (e.g. retrieving parent/sibling information) without re-inspecting
    /// the screen coordinates.
    /// </summary>
    public record ElementInfo(
        string          AutomationId,
        string          Name,
        string          ControlType,
        string          ClassName,
        string          LocalizedControlType,
        bool            IsEnabled,
        bool            IsOffscreen,
        Rect            BoundingRectangle,
        int             ProcessId,
        string          ProcessName,
        AutomationElement RawElement
    );
}
