using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace UIInspector.Settings
{
    /// <summary>
    /// Handles persistence of <see cref="AppSettings"/> to and from
    /// <c>%APPDATA%\UIInspector\settings.json</c>.
    ///
    /// Also manages the Windows auto-start registry entry under
    /// <c>HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run</c>.
    /// </summary>
    public static class SettingsManager
    {
        // =====================================================================
        // Private helpers
        // =====================================================================

        private static readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UIInspector");

        private static readonly string SettingsFilePath =
            Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            // Be lenient when reading so that unknown future fields don't blow up
            // older versions of the application.
            PropertyNameCaseInsensitive = true,
        };

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Loads settings from disk.
        /// Returns a default <see cref="AppSettings"/> instance when the file
        /// does not exist or contains invalid JSON.
        /// </summary>
        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                Debug.WriteLine($"[SettingsManager] No settings file found at {SettingsFilePath}. Using defaults.");
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

                if (loaded is null)
                {
                    Debug.WriteLine("[SettingsManager] Deserialization returned null. Using defaults.");
                    return new AppSettings();
                }

                Debug.WriteLine("[SettingsManager] Settings loaded successfully.");
                return loaded;
            }
            catch (JsonException ex)
            {
                // Corrupt or incompatible JSON — fall back to defaults so the
                // application can still start rather than crashing.
                Debug.WriteLine($"[SettingsManager] Corrupt settings JSON, reverting to defaults. Details: {ex.Message}");
                return new AppSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsManager] Unexpected error reading settings, reverting to defaults. Details: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// Persists <paramref name="settings"/> to disk as indented JSON.
        /// Creates the settings directory if it does not already exist.
        /// </summary>
        /// <param name="settings">The settings object to save.</param>
        public static void Save(AppSettings settings)
        {
            try
            {
                // Ensure the directory exists before attempting to write.
                Directory.CreateDirectory(SettingsDirectory);

                string json = JsonSerializer.Serialize(settings, SerializerOptions);

                // Atomic write: write to a temp file, then rename. This prevents
                // a crash mid-write from leaving a corrupt settings.json.
                string tempPath = SettingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsFilePath, overwrite: true);

                Debug.WriteLine($"[SettingsManager] Settings saved to {SettingsFilePath}.");
            }
            catch (Exception ex)
            {
                // Writing settings is non-critical — log and continue.
                Debug.WriteLine($"[SettingsManager] Failed to save settings: {ex.Message}");
            }
        }

        // =====================================================================
        // Auto-start (registry)
        // =====================================================================

        /// <summary>
        /// Adds or removes the <c>UIInspector</c> value under the Windows
        /// <c>Run</c> registry key so the application launches at user logon.
        /// </summary>
        /// <param name="enabled">
        /// <c>true</c> to create the run entry; <c>false</c> to delete it.
        /// </param>
        public static void SetAutoStart(bool enabled)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key == null)
            {
                Debug.WriteLine("[SettingsManager] Could not open Run registry key.");
                return;
            }

            if (enabled)
            {
                // Environment.ProcessPath is the canonical path for the running image and
                // works correctly for both regular builds and single-file publish.
                string? path = Environment.ProcessPath;

                if (string.IsNullOrEmpty(path))
                {
                    Debug.WriteLine("[SettingsManager] Cannot determine executable path for auto-start.");
                    return;
                }

                key.SetValue("UIInspector", $"\"{path}\"");
                Debug.WriteLine($"[SettingsManager] Auto-start enabled: {path}");
            }
            else
            {
                key.DeleteValue("UIInspector", throwOnMissingValue: false);
                Debug.WriteLine("[SettingsManager] Auto-start disabled.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> when the <c>UIInspector</c> auto-start registry
        /// entry currently exists under the current user's <c>Run</c> key.
        /// </summary>
        public static bool GetAutoStartEnabled()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("UIInspector") != null;
        }
    }
}
