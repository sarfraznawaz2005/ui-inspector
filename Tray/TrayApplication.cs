using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using UIInspector.Hotkeys;
using UIInspector.Inspection;
using UIInspector.Picker;
using UIInspector.Session;
using UIInspector.Settings;

namespace UIInspector.Tray
{
    /// <summary>
    /// Central application context for UI Inspector.
    ///
    /// Extends <see cref="ApplicationContext"/> so that the application's lifetime
    /// is tied to the tray icon rather than to any visible window.
    /// Responsible for:
    ///   - Creating and showing the <see cref="NotifyIcon"/>
    ///   - Owning the <see cref="InspectionSession"/> and the <see cref="ElementPicker"/>
    ///   - Orchestrating the full pick flow (pick → screenshot → query → session.Add)
    ///   - Regenerating the context menu whenever the session changes
    ///   - Responding to left-click (show menu) and right-click (automatic via WinForms)
    ///   - Disposing all resources on exit
    /// </summary>
    public sealed class TrayApplication : ApplicationContext
    {
        // =====================================================================
        // Fields
        // =====================================================================

        private readonly NotifyIcon       _notifyIcon;
        private readonly AppSettings      _settings;

        // Session and pickers — owned by TrayApplication for the process lifetime.
        private readonly InspectionSession _session;
        private readonly ElementPicker     _picker;
        private readonly SpotPicker        _spotPicker;

        // Global hotkey manager — receives WM_HOTKEY and fires HotkeyPressed.
        private readonly GlobalHotkeyManager _hotkeyManager;

        // Generated icons — stored so we can swap them (idle ↔ active) and
        // dispose them properly when the application exits.
        private Icon _idleIcon;
        private Icon _activeIcon;

        // Track how many elements are currently captured.
        // Kept as a field so RefreshTooltip/UpdateElementCount can use it cheaply.
        private int _capturedElementCount = 0;

        // Guard against re-entrant pick calls (user double-clicks the menu item).
        private volatile bool _pickInProgress = false;

        // =====================================================================
        // Constructor
        // =====================================================================

        /// <summary>
        /// Initialises the tray icon, session, picker, and wires all event handlers.
        /// </summary>
        public TrayApplication()
        {
            // Load user settings (or defaults if first run).
            _settings = SettingsManager.Load();

            // Load the app icon from embedded resource.
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("logo.ico");
            var logoIcon = new Icon(stream!);
            _idleIcon   = logoIcon;
            _activeIcon = logoIcon;

            // Create session and subscribe to changes so the menu stays current.
            _session = new InspectionSession();
            _session.SessionChanged += OnSessionChanged;

            // Create pickers (reusable across multiple pick sessions).
            _picker     = new ElementPicker();
            _spotPicker = new SpotPicker();

            // Run screenshot auto-cleanup on startup if the user has opted in.
            if (_settings.AutoCleanScreenshots)
                RunScreenshotCleanup();

            // Build the NotifyIcon.
            _notifyIcon = new NotifyIcon
            {
                Icon             = _idleIcon,
                Text             = "UI Inspector",
                Visible          = true,
                ContextMenuStrip = BuildMenu(),
            };

            // Left-click should also show the context menu.
            _notifyIcon.MouseClick += OnTrayIconMouseClick;

            // Set up global hotkeys.
            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.Register(1, _settings.PickHotkey);
            _hotkeyManager.Register(2, _settings.CopyHotkey);
            _hotkeyManager.Register(3, _settings.SpotHotkey);
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            RefreshTooltip();
        }

        // =====================================================================
        // Public methods
        // =====================================================================

        /// <summary>
        /// Updates the captured-element count displayed in the tray menu and tooltip,
        /// and optionally switches between the idle and active icons.
        /// </summary>
        public void UpdateElementCount(int count)
        {
            _capturedElementCount = count;

            // Swap icon to reflect whether any elements are captured.
            _notifyIcon.Icon = count > 0 ? _activeIcon : _idleIcon;

            // Rebuild the menu so counts and enabled-states are current.
            DisposeCurrentMenu();
            _notifyIcon.ContextMenuStrip = BuildMenu();

            RefreshTooltip();
        }

        // =====================================================================
        // Private — menu construction
        // =====================================================================

        /// <summary>
        /// Builds a fresh <see cref="ContextMenuStrip"/> from the current session state.
        /// </summary>
        private ContextMenuStrip BuildMenu() =>
            TrayMenuBuilder.Build(
                session:        _session,
                onPickElement:  OnPickElementClicked,
                onPickSpot:     OnPickSpotClicked,
                onCopyAll:      OnCopyAllClicked,
                onClearAll:     OnClearAllClicked,
                onSettings:     OnSettingsClicked,
                onCopySingle:   OnCopySingleClicked,
                onEditQuery:    OnEditQueryClicked,
                onViewShot:     OnViewShotClicked,
                onRemove:       OnRemoveClicked);

        // =====================================================================
        // Private — settings dialog
        // =====================================================================

        /// <summary>
        /// Opens the Settings dialog.  When the user saves, re-reads the settings
        /// from disk into the in-memory <see cref="_settings"/> object and
        /// re-registers the (possibly changed) global hotkeys.
        /// </summary>
        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            using var dialog = new SettingsDialog(_settings);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // The dialog has already written to disk via SettingsManager.Save.
                // Re-read so our in-memory state reflects the new values.
                AppSettings newSettings = SettingsManager.Load();
                CopySettings(newSettings, _settings);

                // Re-register hotkeys — combos may have changed.
                _hotkeyManager.UnregisterAll();
                _hotkeyManager.Register(1, _settings.PickHotkey);
                _hotkeyManager.Register(2, _settings.CopyHotkey);
                _hotkeyManager.Register(3, _settings.SpotHotkey);

                Debug.WriteLine("[TrayApplication] Settings reloaded after dialog save.");
            }
        }

        /// <summary>
        /// Copies every property from <paramref name="source"/> into
        /// <paramref name="destination"/>, allowing the readonly <see cref="_settings"/>
        /// field to stay in place while its contents are refreshed.
        /// </summary>
        private static void CopySettings(AppSettings source, AppSettings destination)
        {
            destination.PickHotkey           = source.PickHotkey;
            destination.SpotHotkey           = source.SpotHotkey;
            destination.CopyHotkey           = source.CopyHotkey;
            destination.AutoCopy             = source.AutoCopy;
            destination.AutoClearBeforeCopy  = source.AutoClearBeforeCopy;
            destination.ScreenshotFolder     = source.ScreenshotFolder;
            destination.AutoCleanScreenshots = source.AutoCleanScreenshots;
            destination.CleanAfterHours      = source.CleanAfterHours;
            destination.StartWithWindows     = source.StartWithWindows;
            destination.HighlightColor       = source.HighlightColor;
            destination.HighlightOpacity     = source.HighlightOpacity;
        }

        // =====================================================================
        // Private — pick flow
        // =====================================================================

        /// <summary>
        /// Entry point for the pick flow.  Called from the tray menu and from the
        /// hot-key handler (future).
        ///
        /// The method is <c>async void</c> because it is a fire-and-forget event
        /// handler; all exceptions are caught internally so they do not crash the
        /// application.
        /// </summary>
        private async void OnPickElementClicked(object? sender, EventArgs e)
        {
            if (_pickInProgress)
            {
                Debug.WriteLine("[TrayApplication] Pick already in progress — ignoring.");
                return;
            }

            _pickInProgress = true;

            try
            {
                // ----------------------------------------------------------
                // Step 1: Enter pick mode — returns the clicked element or null
                // ----------------------------------------------------------
                ElementInfo? elementInfo = await _picker.PickAsync(_settings);

                if (elementInfo == null)
                {
                    // User cancelled (Escape or right-click).
                    Debug.WriteLine("[TrayApplication] Pick cancelled.");
                    return;
                }

                // ----------------------------------------------------------
                // Step 2: Build selector
                // ----------------------------------------------------------
                string selector = string.Empty;
                try
                {
                    selector = SelectorBuilder.BuildSelector(elementInfo.RawElement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayApplication] BuildSelector failed: {ex.Message}");
                }

                // ----------------------------------------------------------
                // Step 3: Get parent info
                // ----------------------------------------------------------
                ElementInfo? parentInfo = null;
                try
                {
                    parentInfo = SelectorBuilder.GetParentInfo(elementInfo.RawElement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayApplication] GetParentInfo failed: {ex.Message}");
                }

                // ----------------------------------------------------------
                // Step 4: Get siblings
                // ----------------------------------------------------------
                List<ElementInfo> siblings = new();
                try
                {
                    siblings = SelectorBuilder.GetSiblings(elementInfo.RawElement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayApplication] GetSiblings failed: {ex.Message}");
                }

                // ----------------------------------------------------------
                // Step 5: Detect process type
                // ----------------------------------------------------------
                ProcessType processType = ProcessDetector.Detect(elementInfo.ProcessId);

                // ----------------------------------------------------------
                // Step 6: Capture screenshot
                // ----------------------------------------------------------
                string screenshotPath = string.Empty;
                try
                {
                    screenshotPath = ScreenshotCapture.CaptureElement(
                        elementInfo.BoundingRectangle,
                        _settings.ScreenshotFolder,
                        _settings.HighlightColor);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayApplication] CaptureElement failed: {ex.Message}");
                }

                // ----------------------------------------------------------
                // Step 7: Build CapturedElement (without query yet)
                // ----------------------------------------------------------
                string parentDescription = BuildElementDescription(parentInfo);

                var siblingDescriptions = new List<string>();
                foreach (ElementInfo sib in siblings)
                    siblingDescriptions.Add(BuildElementDescription(sib));

                var captured = new CapturedElement
                {
                    ControlType         = elementInfo.ControlType,
                    Name                = elementInfo.Name,
                    AutomationId        = elementInfo.AutomationId,
                    ClassName           = elementInfo.ClassName,
                    Selector            = selector,
                    IsEnabled           = elementInfo.IsEnabled,
                    IsOffscreen         = elementInfo.IsOffscreen,
                    BoundingRectangle   = elementInfo.BoundingRectangle,
                    ProcessName         = elementInfo.ProcessName,
                    ProcessType         = processType,
                    ScreenshotPath      = screenshotPath,
                    ParentDescription   = parentDescription,
                    SiblingDescriptions = siblingDescriptions,
                    CapturedAt          = DateTime.UtcNow,
                };

                // ----------------------------------------------------------
                // Step 8: Show QueryDialog to get user annotation
                //         Must run on the WinForms UI thread.
                // ----------------------------------------------------------
                string query = string.Empty;

                using var dialog = new QueryDialog(
                    elementInfo.ControlType,
                    elementInfo.Name,
                    elementInfo.AutomationId,
                    existingQuery: string.Empty);

                DialogResult result = dialog.ShowDialog();

                if (result != DialogResult.OK)
                {
                    // User clicked Skip or closed via X — discard the element.
                    // Clean up the screenshot since we won't be keeping it.
                    if (!string.IsNullOrEmpty(screenshotPath))
                    {
                        try { System.IO.File.Delete(screenshotPath); }
                        catch { /* best effort */ }
                    }
                    Debug.WriteLine("[TrayApplication] Query dialog skipped/closed — element discarded.");
                    return;
                }

                captured.Query = dialog.QueryText;

                // ----------------------------------------------------------
                // Step 9: Add to session (fires SessionChanged → rebuilds menu)
                // ----------------------------------------------------------
                _session.Add(captured);

                Debug.WriteLine(
                    $"[TrayApplication] Added element [{captured.Index}] " +
                    $"{captured.ControlType} \"{captured.Name}\"");

                // Auto-copy to clipboard if enabled.
                if (_settings.AutoCopy)
                {
                    ClipboardExporter.ExportToClipboard(_session);
                    Debug.WriteLine("[TrayApplication] Auto-copied to clipboard.");

                    if (_settings.AutoClearBeforeCopy)
                    {
                        _session.Clear(deleteScreenshots: false);
                        Debug.WriteLine("[TrayApplication] Auto-cleared session after copy (screenshots retained).");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayApplication] Unhandled error in pick flow: {ex}");
                MessageBox.Show(
                    $"An error occurred while picking an element:\n\n{ex.Message}",
                    "UI Inspector — Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _pickInProgress = false;
            }
        }

        /// <summary>
        /// Entry point for the spot-pick flow.  The user draws a rectangular
        /// region; we capture a screenshot of that region and attempt to detect
        /// the UI element at its centre.
        /// </summary>
        private async void OnPickSpotClicked(object? sender, EventArgs e)
        {
            if (_pickInProgress)
            {
                Debug.WriteLine("[TrayApplication] Pick already in progress — ignoring.");
                return;
            }

            _pickInProgress = true;

            try
            {
                // ----------------------------------------------------------
                // Step 1: Enter spot-pick mode — user draws a rectangle
                // ----------------------------------------------------------
                SpotResult? spotResult = await _spotPicker.PickAsync(_settings);

                if (spotResult == null)
                {
                    Debug.WriteLine("[TrayApplication] Spot pick cancelled.");
                    return;
                }

                ElementInfo? elementInfo = spotResult.DetectedElement;

                // ----------------------------------------------------------
                // Steps 2-4: Selector / parent / siblings
                //            (only possible when a UIA element was detected)
                // ----------------------------------------------------------
                string selector = string.Empty;
                ElementInfo? parentInfo = null;
                List<ElementInfo> siblings = new();

                if (elementInfo != null)
                {
                    try { selector = SelectorBuilder.BuildSelector(elementInfo.RawElement); }
                    catch (Exception ex) { Debug.WriteLine($"[TrayApplication] Spot BuildSelector failed: {ex.Message}"); }

                    try { parentInfo = SelectorBuilder.GetParentInfo(elementInfo.RawElement); }
                    catch (Exception ex) { Debug.WriteLine($"[TrayApplication] Spot GetParentInfo failed: {ex.Message}"); }

                    try { siblings = SelectorBuilder.GetSiblings(elementInfo.RawElement); }
                    catch (Exception ex) { Debug.WriteLine($"[TrayApplication] Spot GetSiblings failed: {ex.Message}"); }
                }

                // ----------------------------------------------------------
                // Step 5: Process type
                // ----------------------------------------------------------
                ProcessType processType = elementInfo != null
                    ? ProcessDetector.Detect(elementInfo.ProcessId)
                    : ProcessType.Unknown;

                // ----------------------------------------------------------
                // Step 6: Capture screenshot of the drawn region
                // ----------------------------------------------------------
                string screenshotPath = string.Empty;
                try
                {
                    screenshotPath = ScreenshotCapture.CaptureElement(
                        spotResult.DrawnBounds,
                        _settings.ScreenshotFolder,
                        _settings.HighlightColor);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayApplication] Spot CaptureElement failed: {ex.Message}");
                }

                // ----------------------------------------------------------
                // Step 7: Build CapturedElement
                // ----------------------------------------------------------
                string parentDescription = BuildElementDescription(parentInfo);
                var siblingDescriptions  = new List<string>();
                foreach (ElementInfo sib in siblings)
                    siblingDescriptions.Add(BuildElementDescription(sib));

                var captured = new CapturedElement
                {
                    ControlType         = elementInfo?.ControlType  ?? "Region",
                    Name                = elementInfo?.Name         ?? string.Empty,
                    AutomationId        = elementInfo?.AutomationId ?? string.Empty,
                    ClassName           = elementInfo?.ClassName    ?? string.Empty,
                    Selector            = selector,
                    IsEnabled           = elementInfo?.IsEnabled    ?? false,
                    IsOffscreen         = elementInfo?.IsOffscreen  ?? false,
                    BoundingRectangle   = spotResult.DrawnBounds,
                    ProcessName         = elementInfo?.ProcessName  ?? string.Empty,
                    ProcessType         = processType,
                    ScreenshotPath      = screenshotPath,
                    ParentDescription   = parentDescription,
                    SiblingDescriptions = siblingDescriptions,
                    CapturedAt          = DateTime.UtcNow,
                };

                // ----------------------------------------------------------
                // Step 8: Query dialog
                // ----------------------------------------------------------
                using var dialog = new QueryDialog(
                    captured.ControlType,
                    captured.Name,
                    captured.AutomationId,
                    existingQuery: string.Empty);

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(screenshotPath))
                    {
                        try { System.IO.File.Delete(screenshotPath); }
                        catch { /* best effort */ }
                    }
                    Debug.WriteLine("[TrayApplication] Spot query dialog skipped — element discarded.");
                    return;
                }

                captured.Query = dialog.QueryText;

                // ----------------------------------------------------------
                // Step 9: Add to session
                // ----------------------------------------------------------
                _session.Add(captured);

                Debug.WriteLine(
                    $"[TrayApplication] Added spot [{captured.Index}] " +
                    $"{captured.ControlType} \"{captured.Name}\"");

                if (_settings.AutoCopy)
                {
                    ClipboardExporter.ExportToClipboard(_session);
                    Debug.WriteLine("[TrayApplication] Auto-copied to clipboard.");

                    if (_settings.AutoClearBeforeCopy)
                    {
                        _session.Clear(deleteScreenshots: false);
                        Debug.WriteLine("[TrayApplication] Auto-cleared session after copy (screenshots retained).");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayApplication] Unhandled error in spot pick flow: {ex}");
                MessageBox.Show(
                    $"An error occurred while picking a spot:\n\n{ex.Message}",
                    "UI Inspector — Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _pickInProgress = false;
            }
        }

        // =====================================================================
        // Private — element submenu callbacks
        // =====================================================================

        private void OnCopySingleClicked(int index)
        {
            CapturedElement? element = FindElement(index);
            if (element == null) return;

            ClipboardExporter.ExportSingleToClipboard(element);
        }

        private void OnEditQueryClicked(int index)
        {
            CapturedElement? element = FindElement(index);
            if (element == null) return;

            using var dialog = new QueryDialog(
                element.ControlType,
                element.Name,
                element.AutomationId,
                existingQuery: element.Query);

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK)
                _session.UpdateQuery(index, dialog.QueryText);
        }

        private void OnViewShotClicked(int index)
        {
            CapturedElement? element = FindElement(index);
            if (element == null || string.IsNullOrWhiteSpace(element.ScreenshotPath))
                return;

            // Only open files with a .png extension to prevent shell-execute abuse.
            if (!element.ScreenshotPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(element.ScreenshotPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayApplication] Could not open screenshot: {ex.Message}");
                MessageBox.Show(
                    $"Could not open screenshot:\n{element.ScreenshotPath}\n\n{ex.Message}",
                    "UI Inspector",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OnRemoveClicked(int index)
        {
            _session.Remove(index);
        }

        // =====================================================================
        // Private — session-level callbacks
        // =====================================================================

        private void OnClearAllClicked(object? sender, EventArgs e)
        {
            _session.Clear();
        }

        private void OnCopyAllClicked(object? sender, EventArgs e)
        {
            if (_session.Count == 0)
            {
                _notifyIcon.ShowBalloonTip(
                    2000,
                    "UI Inspector",
                    "Nothing to copy — pick some elements first.",
                    ToolTipIcon.Info);
                return;
            }

            ClipboardExporter.ExportToClipboard(_session);
        }

        // =====================================================================
        // Private — global hotkey handler
        // =====================================================================

        /// <summary>
        /// Dispatches global hotkey activations to the appropriate actions.
        /// ID 1 = PickHotkey → pick element flow.
        /// ID 2 = CopyHotkey → copy all to clipboard.
        /// </summary>
        private void OnHotkeyPressed(int id)
        {
            switch (id)
            {
                case 1:
                    OnPickElementClicked(null, EventArgs.Empty);
                    break;
                case 2:
                    OnCopyAllClicked(null, EventArgs.Empty);
                    break;
                case 3:
                    OnPickSpotClicked(null, EventArgs.Empty);
                    break;
                default:
                    Debug.WriteLine($"[TrayApplication] Unknown hotkey id={id}");
                    break;
            }
        }

        // =====================================================================
        // Private — session event handler
        // =====================================================================

        private void OnSessionChanged()
        {
            // This is called from within session mutations; all we need to do
            // is update the count and let UpdateElementCount rebuild the menu.
            UpdateElementCount(_session.Count);
        }

        // =====================================================================
        // Private — helpers
        // =====================================================================

        private CapturedElement? FindElement(int index)
        {
            foreach (CapturedElement e in _session.Elements)
                if (e.Index == index) return e;
            return null;
        }

        /// <summary>
        /// Builds a one-line description string from an <see cref="ElementInfo"/>
        /// suitable for use as a sibling/parent description in the session.
        /// </summary>
        private static string BuildElementDescription(ElementInfo? info)
        {
            if (info == null)
                return string.Empty;

            string name = string.IsNullOrWhiteSpace(info.Name)
                ? "(unnamed)"
                : info.Name;

            return string.IsNullOrWhiteSpace(info.AutomationId)
                ? $"{info.ControlType} \"{Truncate(name, 40)}\""
                : $"{info.ControlType} \"{Truncate(name, 40)}\" [{info.AutomationId}]";
        }

        private static string Truncate(string s, int maxLen) =>
            s.Length <= maxLen ? s : s[..maxLen] + "...";

        private void RunScreenshotCleanup()
        {
            try
            {
                ScreenshotCapture.CleanupOldScreenshots(
                    _settings.ScreenshotFolder,
                    _settings.CleanAfterHours);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayApplication] Screenshot cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the tray icon tooltip to reflect the current element count.
        /// Windows truncates NotifyIcon.Text at 63 characters, so keep it short.
        /// </summary>
        private void RefreshTooltip()
        {
            _notifyIcon.Text = _capturedElementCount > 0
                ? $"UI Inspector — {_capturedElementCount} element{(_capturedElementCount == 1 ? "" : "s")} captured"
                : "UI Inspector — click to pick elements";

            // Clamp to 63 chars (WinForms throws ArgumentException otherwise).
            if (_notifyIcon.Text.Length > 63)
                _notifyIcon.Text = _notifyIcon.Text[..63];
        }

        /// <summary>
        /// Disposes the existing <see cref="ContextMenuStrip"/> if one is attached,
        /// to avoid a resource leak each time the menu is rebuilt.
        /// </summary>
        private void DisposeCurrentMenu()
        {
            var old = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = null;
            old?.Dispose();
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        /// <summary>
        /// Shows the context menu when the user left-clicks the tray icon.
        /// </summary>
        private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                try
                {
                    var method = typeof(NotifyIcon).GetMethod(
                        "ShowContextMenu",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

                    method?.Invoke(_notifyIcon, null);
                }
                catch
                {
                    _notifyIcon.ContextMenuStrip?.Show(Cursor.Position);
                }
            }
        }

        // =====================================================================
        // Disposal
        // =====================================================================

        /// <summary>
        /// Hides and disposes the tray icon and all associated resources.
        /// Called automatically by <see cref="ApplicationContext"/> on exit.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _session.SessionChanged -= OnSessionChanged;

                _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                _hotkeyManager.UnregisterAll();
                _hotkeyManager.Dispose();

                DisposeCurrentMenu();

                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();

                _picker.Dispose();

                _idleIcon.Dispose();
                if (_activeIcon != _idleIcon)
                    _activeIcon.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
