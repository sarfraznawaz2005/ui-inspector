using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UIInspector.Session
{
    /// <summary>
    /// Holds the list of <see cref="CapturedElement"/> objects accumulated during
    /// a single application run and exposes mutating operations that keep the list
    /// consistent (index assignment, screenshot cleanup).
    ///
    /// Every mutating operation fires <see cref="SessionChanged"/> so that
    /// subscribers (e.g. the tray application) can refresh their state.
    /// </summary>
    public class InspectionSession
    {
        // =====================================================================
        // Events
        // =====================================================================

        /// <summary>Raised after any mutation to the session (add, remove, update, clear).</summary>
        public event Action? SessionChanged;

        // =====================================================================
        // Private state
        // =====================================================================

        private readonly List<CapturedElement> _elements = new();

        /// <summary>
        /// The index value that will be assigned to the next element added.
        /// Starts at 1 and only ever increases — removed elements leave gaps,
        /// which makes index values stable references (no renumbering on delete).
        /// </summary>
        private int _nextIndex = 1;

        // =====================================================================
        // Read-only access
        // =====================================================================

        public IReadOnlyList<CapturedElement> Elements => _elements.AsReadOnly();
        public int Count => _elements.Count;

        // =====================================================================
        // Mutating operations
        // =====================================================================

        /// <summary>
        /// Appends <paramref name="element"/> to the session, assigning it the
        /// next available index. Fires <see cref="SessionChanged"/>.
        /// </summary>
        public void Add(CapturedElement element)
        {
            element.Index = _nextIndex++;
            _elements.Add(element);
            SessionChanged?.Invoke();
        }

        /// <summary>
        /// Removes the element whose <see cref="CapturedElement.Index"/> equals
        /// <paramref name="index"/>, deleting its screenshot file from disk.
        /// A no-op (but still fires the event) when no such element exists.
        /// </summary>
        public void Remove(int index)
        {
            int listIndex = _elements.FindIndex(e => e.Index == index);
            if (listIndex >= 0)
            {
                CapturedElement element = _elements[listIndex];
                DeleteScreenshot(element.ScreenshotPath);
                _elements.RemoveAt(listIndex);
            }
            else
            {
                Debug.WriteLine($"[InspectionSession] Remove: no element with index {index}.");
            }

            SessionChanged?.Invoke();
        }

        /// <summary>
        /// Updates the <see cref="CapturedElement.Query"/> text for the element
        /// identified by <paramref name="index"/>.
        /// A no-op (but still fires the event) when no such element exists.
        /// </summary>
        public void UpdateQuery(int index, string newQuery)
        {
            CapturedElement? element = _elements.Find(e => e.Index == index);
            if (element != null)
            {
                element.Query = newQuery ?? string.Empty;
            }
            else
            {
                Debug.WriteLine($"[InspectionSession] UpdateQuery: no element with index {index}.");
            }

            SessionChanged?.Invoke();
        }

        /// <summary>
        /// Removes every element from the session and resets the index counter to 1.
        /// </summary>
        /// <param name="deleteScreenshots">
        /// When <c>true</c> (the default), screenshot files are deleted from disk.
        /// Pass <c>false</c> to clear the session list while leaving files on disk
        /// (used by auto-clear after copy, where the AI agent still needs the files).
        /// </param>
        public void Clear(bool deleteScreenshots = true)
        {
            if (deleteScreenshots)
            {
                foreach (CapturedElement element in _elements)
                    DeleteScreenshot(element.ScreenshotPath);
            }

            _elements.Clear();
            _nextIndex = 1;

            SessionChanged?.Invoke();
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        /// <summary>
        /// Attempts to delete a screenshot file. Logs but does not propagate
        /// exceptions — screenshot cleanup is a best-effort operation.
        /// </summary>
        private static void DeleteScreenshot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.WriteLine($"[InspectionSession] Deleted screenshot: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InspectionSession] Could not delete screenshot '{path}': {ex.Message}");
            }
        }
    }
}
