using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using UIInspector.Tray;

namespace UIInspector
{
    /// <summary>
    /// Application entry point.
    ///
    /// Responsibilities handled here:
    ///   1. Single-instance enforcement via a named Mutex
    ///   2. Global unhandled-exception handlers (UI thread + all threads)
    ///   3. WinForms bootstrap (visual styles, DPI, ApplicationContext)
    /// </summary>
    internal static class Program
    {
        // Named mutex that guarantees only one instance of UI Inspector runs
        // at any time across all user sessions on the machine.
        // Local\ scope ensures one instance per user session, not machine-wide.
        private const string MutexName = "Local\\UIInspector_SingleInstance";

        /// <summary>
        /// Application entry point.
        /// Must be STA because WinForms (and COM-based UIA) require it.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // -----------------------------------------------------------------
            // Single-instance guard
            // -----------------------------------------------------------------
            bool createdNew;
            using var mutex = new Mutex(
                initiallyOwned: true,
                name:           MutexName,
                createdNew:     out createdNew);

            if (!createdNew)
            {
                // Another instance is already running — inform the user and bail.
                MessageBox.Show(
                    text:    "UI Inspector is already running.\n\nCheck the system tray.",
                    caption: "UI Inspector",
                    buttons: MessageBoxButtons.OK,
                    icon:    MessageBoxIcon.Information);
                return;
            }

            try
            {
                // -------------------------------------------------------------
                // Global exception handlers
                // Must be registered before Application.Run so that any
                // exceptions thrown during startup are also caught.
                // -------------------------------------------------------------

                // Catches unhandled exceptions on any non-UI thread.
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Catches unhandled exceptions on the WinForms UI message-pump thread.
                Application.ThreadException += OnThreadException;

                // Ensure WinForms propagates exceptions to ThreadException rather
                // than showing its own default dialog.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                // -------------------------------------------------------------
                // WinForms configuration
                // -------------------------------------------------------------
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Note: HighDpiMode is set to PerMonitorV2 via <ApplicationHighDpiMode>
                // in the .csproj, which generates the correct WinForms bootstrap code.
                // The app.manifest also declares PerMonitorV2 for the window manager.

                // -------------------------------------------------------------
                // Start the tray application
                // -------------------------------------------------------------
                Application.Run(new TrayApplication());
            }
            finally
            {
                // Always release the mutex so it does not become orphaned if
                // the process exits unexpectedly (e.g. via Environment.Exit).
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // ReleaseMutex throws if the current thread does not own the
                    // mutex (e.g. if it was already released elsewhere). Safe to ignore.
                }
            }
        }

        // =====================================================================
        // Global exception handlers
        // =====================================================================

        /// <summary>
        /// Handles unhandled exceptions thrown on background / non-UI threads.
        /// At this point the runtime considers the application fatally broken,
        /// so we log the error and allow the CLR to terminate the process.
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = e.ExceptionObject is Exception ex
                ? ex.ToString()
                : e.ExceptionObject?.ToString() ?? "Unknown error";

            Debug.WriteLine($"[UIInspector] FATAL unhandled exception:\n{message}");
            LogError($"FATAL unhandled exception:\n{message}");

            // Show a brief dialog before the process dies so the user knows
            // why the tray icon disappeared.
            try
            {
                MessageBox.Show(
                    text:    $"UI Inspector encountered a fatal error and must close.\n\n{message}",
                    caption: "UI Inspector — Fatal Error",
                    buttons: MessageBoxButtons.OK,
                    icon:    MessageBoxIcon.Error);
            }
            catch
            {
                // If we can't even show a dialog, swallow silently and let
                // the runtime take over.
            }
        }

        /// <summary>
        /// Handles unhandled exceptions thrown on the WinForms UI message-pump thread.
        /// Unlike <see cref="OnUnhandledException"/>, the application can potentially
        /// continue running after this handler returns.
        /// </summary>
        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Debug.WriteLine($"[UIInspector] Unhandled UI-thread exception:\n{e.Exception}");
            LogError($"Unhandled UI-thread exception:\n{e.Exception}");

            DialogResult result = MessageBox.Show(
                text:    $"An unexpected error occurred in UI Inspector.\n\n{e.Exception.Message}\n\nDo you want to continue?",
                caption: "UI Inspector — Error",
                buttons: MessageBoxButtons.YesNo,
                icon:    MessageBoxIcon.Error);

            if (result == DialogResult.No)
            {
                // User chose to quit — exit cleanly.
                Application.Exit();
            }
            // If Yes, we return and let the message pump resume, which may or
            // may not work depending on what state the exception left things in.
        }

        // =====================================================================
        // Error logging
        // =====================================================================

        /// <summary>
        /// Appends a timestamped entry to %APPDATA%\UIInspector\error.log.
        ///
        /// The log file is silently truncated when it exceeds 1 MB so it never
        /// grows unbounded on a machine where the user never checks it.
        ///
        /// This method must never throw — it is called from exception handlers
        /// where raising a secondary exception would hide the original one.
        /// </summary>
        private static void LogError(string message)
        {
            try
            {
                string logDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "UIInspector");

                Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, "error.log");

                // Keep the log file small — reset it when it exceeds 1 MB.
                if (File.Exists(logFile) && new FileInfo(logFile).Length > 1_048_576)
                    File.WriteAllText(logFile, "");

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logFile, entry);
            }
            catch
            {
                // Logging must never throw.
            }
        }
    }
}
