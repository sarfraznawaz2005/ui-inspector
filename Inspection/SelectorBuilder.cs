using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Automation;

namespace UIInspector.Inspection
{
    /// <summary>
    /// Builds a human-readable CSS-like selector path for a UI Automation element
    /// by walking up the ancestor chain.
    ///
    /// Example output:
    ///   Window[@Name='Notepad'] > Document > Edit[@Id='Text Area']
    ///
    /// Selection preference per segment:
    ///   1. AutomationId  (most stable, survives localization)
    ///   2. Name          (readable, but may change with locale or content)
    ///   3. Child index   (least stable, used only as last resort)
    /// </summary>
    public static class SelectorBuilder
    {
        // Maximum ancestor levels to walk before stopping.
        private const int MaxDepth = 20;

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Builds a selector string for <paramref name="element"/> by walking its
        /// ancestor chain up to the desktop root.
        /// </summary>
        public static string BuildSelector(AutomationElement element)
        {
            var segments = new List<string>();

            AutomationElement? current = element;
            int depth = 0;

            while (current != null && depth < MaxDepth)
            {
                // Stop at the desktop root (its parent is null).
                AutomationElement? parent;
                try
                {
                    parent = TreeWalker.ControlViewWalker.GetParent(current);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SelectorBuilder] GetParent failed: {ex.Message}");
                    break;
                }

                if (parent == null)
                    break; // Reached desktop root.

                string segment = BuildSegment(current, parent);
                segments.Add(segment);

                current = parent;
                depth++;
            }

            if (segments.Count == 0)
                return BuildFallbackSegment(element);

            // Segments were collected from leaf to root — reverse for readability.
            segments.Reverse();
            return string.Join(" > ", segments);
        }

        /// <summary>
        /// Returns an <see cref="ElementInfo"/> for the immediate parent of
        /// <paramref name="element"/>, or <see langword="null"/> if there is no parent
        /// or if the parent is the desktop root.
        /// </summary>
        public static ElementInfo? GetParentInfo(AutomationElement element)
        {
            try
            {
                AutomationElement? parent = TreeWalker.ControlViewWalker.GetParent(element);
                if (parent == null)
                    return null;

                return AutomationInspector.ExtractElementInfo(parent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorBuilder] GetParentInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Enumerates siblings of <paramref name="element"/> (children of its parent)
        /// up to <paramref name="maxCount"/> items.
        ///
        /// The element itself is included in the returned list.
        /// </summary>
        public static List<ElementInfo> GetSiblings(AutomationElement element, int maxCount = 10)
        {
            var results = new List<ElementInfo>();

            try
            {
                AutomationElement? parent = TreeWalker.ControlViewWalker.GetParent(element);
                if (parent == null)
                    return results;

                AutomationElement? child = TreeWalker.ControlViewWalker.GetFirstChild(parent);
                int count = 0;

                while (child != null && count < maxCount)
                {
                    ElementInfo? info = AutomationInspector.ExtractElementInfo(child);
                    if (info != null)
                        results.Add(info);

                    try
                    {
                        child = TreeWalker.ControlViewWalker.GetNextSibling(child);
                    }
                    catch
                    {
                        break;
                    }

                    count++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorBuilder] GetSiblings failed: {ex.Message}");
            }

            return results;
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>
        /// Builds a single selector segment for <paramref name="element"/>, using
        /// AutomationId, Name, or child index (in that order of preference).
        /// </summary>
        private static string BuildSegment(AutomationElement element, AutomationElement parent)
        {
            string controlTypeName = GetControlTypeName(element);

            // Prefer AutomationId.
            string automationId = GetStringProp(element, AutomationElement.AutomationIdProperty);
            if (!string.IsNullOrWhiteSpace(automationId))
                return $"{controlTypeName}[@Id='{EscapeValue(automationId)}']";

            // Fall back to Name.
            string name = GetStringProp(element, AutomationElement.NameProperty);
            if (!string.IsNullOrWhiteSpace(name))
                return $"{controlTypeName}[@Name='{EscapeValue(TruncateName(name))}']";

            // Last resort: child index among the parent's children.
            int index = GetChildIndex(element, parent);
            return $"{controlTypeName}[{index}]";
        }

        /// <summary>
        /// Fallback segment used when the ancestor walk yields nothing.
        /// </summary>
        private static string BuildFallbackSegment(AutomationElement element)
        {
            string controlTypeName = GetControlTypeName(element);
            string automationId    = GetStringProp(element, AutomationElement.AutomationIdProperty);
            string name            = GetStringProp(element, AutomationElement.NameProperty);

            if (!string.IsNullOrWhiteSpace(automationId))
                return $"{controlTypeName}[@Id='{EscapeValue(automationId)}']";

            if (!string.IsNullOrWhiteSpace(name))
                return $"{controlTypeName}[@Name='{EscapeValue(TruncateName(name))}']";

            return controlTypeName;
        }

        /// <summary>
        /// Returns the control type's programmatic name with the "ControlType." prefix removed.
        /// </summary>
        private static string GetControlTypeName(AutomationElement element)
        {
            try
            {
                var ct = element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty) as ControlType;
                if (ct != null)
                    return ct.ProgrammaticName.Replace("ControlType.", string.Empty);
            }
            catch { /* fall through */ }

            return "Unknown";
        }

        /// <summary>
        /// Determines the 1-based index of <paramref name="element"/> among the
        /// children of <paramref name="parent"/> that share the same control type.
        /// </summary>
        private static int GetChildIndex(AutomationElement element, AutomationElement parent)
        {
            try
            {
                string targetType = GetControlTypeName(element);
                int index = 1;

                AutomationElement? sibling = TreeWalker.ControlViewWalker.GetFirstChild(parent);
                while (sibling != null)
                {
                    // Check for element identity using RuntimeId.
                    bool isSame = false;
                    try
                    {
                        int[] elementRid  = (int[])element.GetCurrentPropertyValue(AutomationElement.RuntimeIdProperty);
                        int[] siblingRid  = (int[])sibling.GetCurrentPropertyValue(AutomationElement.RuntimeIdProperty);
                        isSame = RuntimeIdsEqual(elementRid, siblingRid);
                    }
                    catch
                    {
                        // If we can't compare runtime IDs fall back to reference equality.
                        isSame = ReferenceEquals(sibling, element);
                    }

                    if (isSame)
                        return index;

                    if (GetControlTypeName(sibling) == targetType)
                        index++;

                    sibling = TreeWalker.ControlViewWalker.GetNextSibling(sibling);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorBuilder] GetChildIndex failed: {ex.Message}");
            }

            return 1;
        }

        private static bool RuntimeIdsEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static string GetStringProp(AutomationElement element, AutomationProperty property)
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

        /// <summary>
        /// Escapes single-quotes in attribute values so the selector string remains valid.
        /// </summary>
        private static string EscapeValue(string value) =>
            value.Replace("'", "\\'");

        /// <summary>
        /// Truncates long element names to keep selector strings readable.
        /// </summary>
        private static string TruncateName(string name, int maxLength = 40) =>
            name.Length > maxLength ? name[..maxLength] + "..." : name;
    }
}
