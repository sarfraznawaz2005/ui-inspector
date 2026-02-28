using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace UIInspector.Settings
{
    /// <summary>
    /// Modal settings dialog for UI Inspector.
    /// Uses AutoScaleMode.Font for proper high-DPI scaling.
    /// All sizes are specified for 100% scale and auto-scaled by WinForms.
    /// </summary>
    public sealed class SettingsDialog : Form
    {
        private readonly TextBox       _pickHotkeyBox;
        private readonly TextBox       _copyHotkeyBox;
        private readonly TextBox       _folderBox;
        private readonly CheckBox      _autoCleanCheck;
        private readonly NumericUpDown  _cleanAfterSpinner;
        private readonly Label         _cleanAfterLabel;
        private readonly Label         _cleanAfterUnitLabel;
        private readonly CheckBox      _startWithWindowsCheck;
        private readonly Button        _colorButton;
        private readonly TrackBar      _opacityTrack;
        private readonly Label         _opacityValueLabel;
        private readonly Button        _browseButton;
        private readonly CheckBox      _autoCopyCheck;
        private readonly CheckBox      _autoClearCheck;
        private readonly Button        _saveButton;
        private readonly Button        _cancelButton;
        private Color                  _selectedColor;

        public SettingsDialog(AppSettings currentSettings)
        {
            SuspendLayout();

            Text             = "UI Inspector Settings";
            Icon             = LoadAppIcon();
            FormBorderStyle  = FormBorderStyle.FixedDialog;
            MaximizeBox      = false;
            MinimizeBox      = false;
            StartPosition    = FormStartPosition.CenterScreen;
            TopMost          = true;
            ShowInTaskbar    = false;

            // Font-based scaling: WinForms measures the font at runtime and
            // scales all controls proportionally. This works reliably across DPI.
            AutoScaleMode       = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(7F, 15F); // Segoe UI 9pt at 96 DPI
            Font                = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Width at 96 DPI; height is computed from controls after ResumeLayout.
            int designW = 520;
            ClientSize = new Size(designW, 700); // temporary, recalculated below

            int pad   = 16;
            int fullW = designW - pad * 2;
            int y     = pad;
            int labelGap = 4;  // gap between label and its control
            int rowGap   = 12; // gap between rows

            // ── Start with Windows (at the top) ──
            _startWithWindowsCheck = new CheckBox
            {
                Text     = "Start with Windows",
                Checked  = SettingsManager.GetAutoStartEnabled(),
                AutoSize = true,
                Location = new Point(pad, y),
            };
            Controls.Add(_startWithWindowsCheck);
            y += 24 + 8;

            // ── Separator ──
            Controls.Add(MakeHLine(pad, y, fullW));
            y += 2 + rowGap;

            // ── Pick Hotkey ──
            Controls.Add(MakeFieldLabel("Pick Hotkey:", pad, y));
            y += 18 + labelGap;
            _pickHotkeyBox = new TextBox
            {
                ReadOnly  = true,
                Text      = currentSettings.PickHotkey,
                Location  = new Point(pad, y),
                Size      = new Size(fullW, 24),
                BackColor = SystemColors.Window,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _pickHotkeyBox.KeyDown += (s, e) => OnHotkeyBoxKeyDown(_pickHotkeyBox, e);
            Controls.Add(_pickHotkeyBox);
            y += 24 + rowGap;

            // ── Copy Hotkey ──
            Controls.Add(MakeFieldLabel("Copy Hotkey:", pad, y));
            y += 18 + labelGap;
            _copyHotkeyBox = new TextBox
            {
                ReadOnly  = true,
                Text      = currentSettings.CopyHotkey,
                Location  = new Point(pad, y),
                Size      = new Size(fullW, 24),
                BackColor = SystemColors.Window,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _copyHotkeyBox.KeyDown += (s, e) => OnHotkeyBoxKeyDown(_copyHotkeyBox, e);
            Controls.Add(_copyHotkeyBox);
            y += 24 + rowGap;

            // ── Auto-copy on pick + Auto-clear before copy ──
            _autoCopyCheck = new CheckBox
            {
                Text     = "Auto-copy to clipboard on pick",
                Checked  = currentSettings.AutoCopy,
                AutoSize = true,
                Location = new Point(pad, y),
            };
            _autoCopyCheck.CheckedChanged += OnAutoCopyChanged;
            Controls.Add(_autoCopyCheck);

            _autoClearCheck = new CheckBox
            {
                Text     = "Auto-clear before copy",
                Checked  = currentSettings.AutoClearBeforeCopy,
                AutoSize = true,
                Enabled  = currentSettings.AutoCopy,
                Location = new Point(0, y), // repositioned after ResumeLayout
            };
            Controls.Add(_autoClearCheck);
            y += 24 + 8;

            // ── Separator ──
            Controls.Add(MakeHLine(pad, y, fullW));
            y += 2 + rowGap;

            // ── Screenshot Folder ──
            Controls.Add(MakeFieldLabel("Screenshot Folder:", pad, y));
            y += 18 + labelGap;
            int browseW = 90;
            int folderW = fullW - browseW - 8;
            _folderBox = new TextBox
            {
                Text     = currentSettings.ScreenshotFolder,
                Location = new Point(pad, y),
                Size     = new Size(folderW, 24),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(_folderBox);

            _browseButton = new Button
            {
                Text     = "Browse...",
                Location = new Point(pad + folderW + 8, y),
                Size     = new Size(browseW, 24),
                Anchor   = AnchorStyles.Top | AnchorStyles.Right,
            };
            _browseButton.Click += OnBrowseClicked;
            Controls.Add(_browseButton);
            y += 24 + rowGap;

            // ── Auto-clean ──
            _autoCleanCheck = new CheckBox
            {
                Text     = "Auto-clean screenshots",
                Checked  = currentSettings.AutoCleanScreenshots,
                AutoSize = true,
                Location = new Point(pad, y),
            };
            _autoCleanCheck.CheckedChanged += OnAutoCleanChanged;
            Controls.Add(_autoCleanCheck);

            // ── Clean after N hours (right-aligned on same row) ──
            int hoursLabelW = 40;
            _cleanAfterUnitLabel = new Label
            {
                Text     = "hours",
                AutoSize = true,
                Location = new Point(designW - pad - hoursLabelW, y + 2),
                Enabled  = currentSettings.AutoCleanScreenshots,
                Anchor   = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_cleanAfterUnitLabel);

            _cleanAfterSpinner = new NumericUpDown
            {
                Minimum   = 1,
                Maximum   = 168,
                Increment = 1,
                Value     = Math.Clamp(currentSettings.CleanAfterHours, 1, 168),
                Location  = new Point(designW - pad - hoursLabelW - 65 - 4, y),
                Size      = new Size(65, 24),
                Enabled   = currentSettings.AutoCleanScreenshots,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_cleanAfterSpinner);

            _cleanAfterLabel = new Label
            {
                Text      = "Clean after:",
                AutoSize  = true,
                Location  = new Point(designW - pad - hoursLabelW - 65 - 4 - 80, y + 2),
                Enabled   = currentSettings.AutoCleanScreenshots,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_cleanAfterLabel);
            y += 24 + 8;

            // ── Separator ──
            Controls.Add(MakeHLine(pad, y, fullW));
            y += 2 + rowGap;

            // ── Highlight Color ──
            _selectedColor = ParseColorSafe(currentSettings.HighlightColor, Color.FromArgb(0x44, 0x88, 0xFF));
            Controls.Add(MakeFieldLabel("Highlight Color:", pad, y));
            y += 18 + labelGap;
            _colorButton = new Button
            {
                Text      = currentSettings.HighlightColor,
                Location  = new Point(pad, y),
                Size      = new Size(140, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = _selectedColor,
                ForeColor = GetContrastColor(_selectedColor),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _colorButton.Click += OnColorButtonClicked;
            Controls.Add(_colorButton);
            y += 24 + rowGap + 20;

            // ── Highlight Opacity ──
            int opacityTick = Math.Clamp((int)Math.Round(currentSettings.HighlightOpacity * 100), 10, 80);

            Controls.Add(MakeFieldLabel("Opacity:", pad, y));
            y += 18 + labelGap;
            int opacityLabelW = 40;
            _opacityTrack = new TrackBar
            {
                Minimum       = 10,
                Maximum       = 80,
                Value         = opacityTick,
                TickFrequency = 10,
                SmallChange   = 1,
                LargeChange   = 5,
                Location      = new Point(pad, y),
                Size          = new Size(fullW - opacityLabelW - 8, 35),
                Anchor        = AnchorStyles.Top | AnchorStyles.Left,
            };
            _opacityTrack.ValueChanged += OnOpacityChanged;
            Controls.Add(_opacityTrack);

            _opacityValueLabel = new Label
            {
                Text      = FormatOpacity(opacityTick),
                AutoSize  = false,
                Size      = new Size(opacityLabelW, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Location  = new Point(pad + fullW - opacityLabelW, y),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_opacityValueLabel);
            y += 35 + 8;

            // ── Separator ──
            Controls.Add(MakeHLine(pad, y, fullW));
            y += 2 + rowGap;

            // ── Save / Cancel buttons ──
            int btnW = 100;
            int btnH = 32;
            _saveButton = new Button
            {
                Text     = "Save",
                Location = new Point(designW - pad - btnW, y),
                Size     = new Size(btnW, btnH),
                Anchor   = AnchorStyles.Top | AnchorStyles.Right,
            };
            _saveButton.Click += OnSaveClicked;
            Controls.Add(_saveButton);

            _cancelButton = new Button
            {
                Text         = "Cancel",
                Location     = new Point(designW - pad - btnW * 2 - 10, y),
                Size         = new Size(btnW, btnH),
                DialogResult = DialogResult.Cancel,
                Anchor       = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_cancelButton);

            AcceptButton = _saveButton;
            CancelButton = _cancelButton;

            ResumeLayout(true);

            // After ResumeLayout, all positions are DPI-scaled.
            float scale = CurrentAutoScaleDimensions.Height / 15F;
            int scaledPad = (int)(pad * scale);

            // Match browse button height to textbox (WinForms auto-sizes textbox height).
            _browseButton.Height = _folderBox.Height;

            // Right-align the auto-clear checkbox using its actual rendered width.
            _autoClearCheck.Left = ClientSize.Width - scaledPad - _autoClearCheck.Width;

            // Align opacity value label with the trackbar's thumb (near top of control).
            _opacityValueLabel.Top = _opacityTrack.Top + 2;

            // Compute form height from actual bottom of last control.
            ClientSize = new Size(ClientSize.Width, _saveButton.Bottom + scaledPad);
        }

        // =====================================================================
        // Layout helpers
        // =====================================================================

        private Label MakeFieldLabel(string text, int x, int y) => new Label
        {
            Text     = text,
            AutoSize = true,
            Location = new Point(x - 1, y),
        };

        private Label MakeHLine(int x, int y, int w) => new Label
        {
            Location    = new Point(x, y),
            Size        = new Size(w, 2),
            BorderStyle = BorderStyle.Fixed3D,
            Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        // =====================================================================
        // Hotkey capture
        // =====================================================================

        private static void OnHotkeyBoxKeyDown(TextBox box, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled          = true;

            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey ||
                key == Keys.Menu       || key == Keys.LWin     ||
                key == Keys.RWin       || key == Keys.None)
                return;

            var parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Shift)   parts.Add("Shift");
            if (e.Alt)     parts.Add("Alt");
            parts.Add(key.ToString());
            box.Text = string.Join("+", parts);
        }

        // =====================================================================
        // Event handlers
        // =====================================================================

        private void OnBrowseClicked(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description         = "Select screenshot folder",
                SelectedPath        = _folderBox.Text,
                ShowNewFolderButton = true,
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _folderBox.Text = dialog.SelectedPath;
        }

        private void OnAutoCopyChanged(object? sender, EventArgs e)
        {
            _autoClearCheck.Enabled = _autoCopyCheck.Checked;
        }

        private void OnAutoCleanChanged(object? sender, EventArgs e)
        {
            bool enabled = _autoCleanCheck.Checked;
            _cleanAfterSpinner.Enabled   = enabled;
            _cleanAfterLabel.Enabled     = enabled;
            _cleanAfterUnitLabel.Enabled = enabled;
        }

        private void OnColorButtonClicked(object? sender, EventArgs e)
        {
            using var dlg = new ColorDialog
            {
                Color    = _selectedColor,
                FullOpen = true,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _selectedColor       = dlg.Color;
                _colorButton.BackColor = _selectedColor;
                _colorButton.ForeColor = GetContrastColor(_selectedColor);
                _colorButton.Text      = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            }
        }

        private void OnOpacityChanged(object? sender, EventArgs e)
        {
            _opacityValueLabel.Text = FormatOpacity(_opacityTrack.Value);
        }

        // =====================================================================
        // Save
        // =====================================================================

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pickHotkeyBox.Text))
            {
                ShowError("Pick Hotkey cannot be empty.\nPress a key combination in the field.");
                _pickHotkeyBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_copyHotkeyBox.Text))
            {
                ShowError("Copy Hotkey cannot be empty.\nPress a key combination in the field.");
                _copyHotkeyBox.Focus();
                return;
            }
            if (_pickHotkeyBox.Text.Equals(_copyHotkeyBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Pick Hotkey and Copy Hotkey must be different.");
                _pickHotkeyBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_folderBox.Text))
            {
                ShowError("Screenshot Folder cannot be empty.");
                _folderBox.Focus();
                return;
            }

            string colorHex = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";

            var settings = new AppSettings
            {
                PickHotkey           = _pickHotkeyBox.Text.Trim(),
                CopyHotkey           = _copyHotkeyBox.Text.Trim(),
                AutoCopy             = _autoCopyCheck.Checked,
                AutoClearBeforeCopy  = _autoClearCheck.Checked,
                ScreenshotFolder     = _folderBox.Text.Trim(),
                AutoCleanScreenshots = _autoCleanCheck.Checked,
                CleanAfterHours      = (int)_cleanAfterSpinner.Value,
                StartWithWindows     = _startWithWindowsCheck.Checked,
                HighlightColor       = colorHex,
                HighlightOpacity     = _opacityTrack.Value / 100.0,
            };

            SettingsManager.Save(settings);
            try { SettingsManager.SetAutoStart(settings.StartWithWindows); }
            catch (Exception ex) { Debug.WriteLine($"[SettingsDialog] SetAutoStart failed: {ex.Message}"); }

            DialogResult = DialogResult.OK;
            Close();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void ShowError(string msg) =>
            MessageBox.Show(this, msg, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private static string FormatOpacity(int tick) => (tick / 100.0).ToString("F1");

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string h = hex.TrimStart('#').Trim();
            try
            {
                if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
                if (h.Length == 6)
                {
                    int rgb = Convert.ToInt32(h, 16);
                    color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static Color ParseColorSafe(string hex, Color fallback) =>
            TryParseHexColor(hex, out Color c) ? c : fallback;

        private static Color GetContrastColor(Color bg)
        {
            double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
            return lum > 0.5 ? Color.Black : Color.White;
        }

        private static Icon? LoadAppIcon()
        {
            try
            {
                var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("logo.ico");
                return stream != null ? new Icon(stream) : null;
            }
            catch { return null; }
        }
    }
}
