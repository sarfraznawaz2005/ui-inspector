using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UIInspector.Inspection
{
    /// <summary>
    /// Classifies a process into broad technology categories so that callers
    /// can tailor the inspection strategy (e.g., suggesting DevTools for
    /// Chromium-based browsers instead of UI Automation).
    /// </summary>
    public enum ProcessType
    {
        NativeDesktop,
        ChromiumBrowser,
        Firefox,
        ElectronApp,
        WebView2App,
        Unknown
    }

    /// <summary>
    /// Detects the technology stack of a process by examining its name and
    /// loaded modules.
    ///
    /// Results are cached in a static dictionary because process identity and
    /// loaded modules do not change during the process lifetime, so repeated
    /// inspections of the same PID are essentially free after the first call.
    /// </summary>
    public static class ProcessDetector
    {
        // =====================================================================
        // Constants — executable names (lower-case, no extension)
        // =====================================================================

        private static readonly HashSet<string> ChromiumNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "brave", "opera", "vivaldi", "arc"
        };

        // =====================================================================
        // Cache
        // =====================================================================

        /// <summary>
        /// Maps process ID → detected type. Populated lazily on first lookup.
        /// Capped at <see cref="MaxCacheSize"/> entries to prevent unbounded growth
        /// from PID reuse in long-running sessions.
        /// </summary>
        private static readonly Dictionary<int, ProcessType> Cache = new();

        private const int MaxCacheSize = 500;

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Returns the <see cref="ProcessType"/> for the process with the given
        /// <paramref name="processId"/>.
        ///
        /// Returns <see cref="ProcessType.Unknown"/> if the process cannot be
        /// accessed (e.g., it has exited or is running at a higher integrity level).
        /// </summary>
        public static ProcessType Detect(int processId)
        {
            if (processId <= 0)
                return ProcessType.Unknown;

            // Fast path — already classified.
            lock (Cache)
            {
                if (Cache.TryGetValue(processId, out ProcessType cached))
                    return cached;
            }

            ProcessType result = ClassifyProcess(processId);

            lock (Cache)
            {
                // Evict the entire cache when it grows too large (PID reuse
                // means stale entries accumulate in long-running sessions).
                if (Cache.Count >= MaxCacheSize)
                    Cache.Clear();

                Cache[processId] = result;
            }

            return result;
        }

        /// <summary>
        /// Removes all cached entries. Useful in tests or when processes have
        /// been recycled (PID reuse).
        /// </summary>
        public static void ClearCache()
        {
            lock (Cache)
            {
                Cache.Clear();
            }
        }

        // =====================================================================
        // Private classification logic
        // =====================================================================

        private static ProcessType ClassifyProcess(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                string processName = process.ProcessName;

                // -----------------------------------------------------------------
                // 1. Check for well-known browser process names first (fastest path,
                //    works even when module enumeration is denied).
                // -----------------------------------------------------------------
                if (ChromiumNames.Contains(processName))
                    return ProcessType.ChromiumBrowser;

                if (string.Equals(processName, "firefox", StringComparison.OrdinalIgnoreCase))
                    return ProcessType.Firefox;

                // -----------------------------------------------------------------
                // 2. Inspect loaded modules for embedded runtimes.
                //    This can throw AccessViolationException or Win32Exception for
                //    elevated or protected processes — catch everything.
                // -----------------------------------------------------------------
                try
                {
                    ProcessModuleCollection modules = process.Modules;
                    bool hasElectron  = false;
                    bool hasWebView2  = false;

                    foreach (ProcessModule module in modules)
                    {
                        string moduleName = module.ModuleName ?? string.Empty;

                        if (moduleName.Equals("electron.dll", StringComparison.OrdinalIgnoreCase))
                            hasElectron = true;

                        if (moduleName.Equals("WebView2Loader.dll", StringComparison.OrdinalIgnoreCase))
                            hasWebView2 = true;

                        // Short-circuit once we have determined the most specific type.
                        if (hasElectron) break;
                        if (hasWebView2) break;
                    }

                    if (hasElectron)  return ProcessType.ElectronApp;
                    if (hasWebView2)  return ProcessType.WebView2App;
                }
                catch (Exception moduleEx)
                {
                    // Module enumeration is denied for many system and elevated processes.
                    // This is expected — fall through to the NativeDesktop default.
                    Debug.WriteLine(
                        $"[ProcessDetector] Cannot enumerate modules for PID {processId} " +
                        $"({process.ProcessName}): {moduleEx.Message}");
                }

                // -----------------------------------------------------------------
                // 3. Default: assume standard Win32/WPF/WinForms native desktop app.
                // -----------------------------------------------------------------
                return ProcessType.NativeDesktop;
            }
            catch (ArgumentException)
            {
                // Process no longer exists.
                Debug.WriteLine($"[ProcessDetector] Process {processId} not found.");
                return ProcessType.Unknown;
            }
            catch (Exception ex)
            {
                // Access denied, or any other unexpected error.
                Debug.WriteLine($"[ProcessDetector] Error classifying process {processId}: {ex.Message}");
                return ProcessType.Unknown;
            }
        }
    }
}
