using System;
using System.Drawing;
using System.Windows.Forms;

namespace UIInspector.Picker
{
    /// <summary>
    /// A WinForms dialog that appears near the cursor after an element is picked
    /// and lets the user type a query / description for that element.
    ///
    /// Uses AutoScaleMode.Font for proper high-DPI scaling.
    ///
    /// Keyboard shortcuts:
    ///   Enter        — confirms and closes with DialogResult.OK
    ///   Ctrl+Enter   — inserts a newline in the text box
    ///   Escape       — skips and closes with DialogResult.Cancel
    /// </summary>
    public sealed class QueryDialog : Form
    {
        private readonly TextBox _queryTextBox;
        private readonly Button  _addButton;
        private readonly Button  _skipButton;
        private int _btnGap;          // scaled gap between textbox and buttons
        private bool _layoutReady;    // true after constructor finishes

        /// <summary>
        /// The text the user entered. Empty when the dialog was skipped.
        /// </summary>
        public string QueryText => _queryTextBox.Text;

        public QueryDialog(string controlType, string name, string automationId, string existingQuery)
        {
            SuspendLayout();

            Text             = "Add Element Query";
            Icon             = LoadAppIcon();
            FormBorderStyle  = FormBorderStyle.FixedDialog;
            StartPosition    = FormStartPosition.Manual;
            MaximizeBox      = false;
            MinimizeBox      = false;
            ShowInTaskbar    = false;
            TopMost          = true;

            AutoScaleMode       = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(7F, 15F); // Segoe UI 9pt at 96 DPI
            Font                = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Width at 96 DPI; height is computed from controls below.
            int designW = 560;
            ClientSize = new Size(designW, 300); // temporary, recalculated below

            int pad  = 16;
            int y    = pad;
            int fullW = designW - pad * 2;

            // ── Header: "ControlType "Name"" ──
            string headerText = BuildHeaderText(controlType, name);
            var headerLabel = new Label
            {
                Text         = headerText,
                Font         = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                AutoSize     = false,
                AutoEllipsis = true,
                Location     = new Point(pad, y),
                Size         = new Size(fullW, 24),
                Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(headerLabel);
            y += 28;

            // ── Subtitle: "AutomationId: ..." ──
            string subtitleText = string.IsNullOrWhiteSpace(automationId)
                ? "AutomationId: (none)"
                : $"AutomationId: {automationId}";

            var subtitleLabel = new Label
            {
                Text         = subtitleText,
                ForeColor    = SystemColors.GrayText,
                AutoSize     = false,
                AutoEllipsis = true,
                Location     = new Point(pad, y),
                Size         = new Size(fullW, 20),
                Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(subtitleLabel);
            y += 28;

            // ── Text box (starts single-line height, grows as user types) ──
            int btnH     = 32;
            int singleLineH = 24;
            int maxTextBoxH = 120;  // max ~6 lines before scrollbar kicks in

            _queryTextBox = new TextBox
            {
                Multiline     = true,
                WordWrap      = true,
                ScrollBars    = ScrollBars.None,
                Location      = new Point(pad, y),
                Size          = new Size(fullW, singleLineH),
                Text          = existingQuery,
                AcceptsReturn = false,
                Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _queryTextBox.KeyDown += OnTextBoxKeyDown;
            _queryTextBox.TextChanged += (_, _) => AutoGrowTextBox(y, singleLineH, maxTextBoxH, pad, btnH);
            ApplyPlaceholder(_queryTextBox, "Describe what this element does...");
            Controls.Add(_queryTextBox);

            // ── Buttons (positioned below textbox, move as it grows) ──
            int btnW  = 100;
            int btnY  = _queryTextBox.Bottom + 12;

            _addButton = new Button
            {
                Text         = "Add",
                Location     = new Point(designW - pad - btnW * 2 - 10, btnY),
                Size         = new Size(btnW, btnH),
                DialogResult = DialogResult.OK,
                Anchor       = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_addButton);

            _skipButton = new Button
            {
                Text         = "Skip",
                Location     = new Point(designW - pad - btnW, btnY),
                Size         = new Size(btnW, btnH),
                DialogResult = DialogResult.Cancel,
                Anchor       = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_skipButton);

            AcceptButton = _addButton;
            CancelButton = _skipButton;

            ResumeLayout(true);

            // After ResumeLayout, all control positions reflect DPI scaling.
            // Capture the scaled gap and compute correct form height.
            _btnGap = _skipButton.Top - _queryTextBox.Bottom;
            ClientSize = new Size(ClientSize.Width, _skipButton.Bottom + _btnGap);
            _layoutReady = true;

            // If editing an existing query, trigger initial sizing now.
            if (!string.IsNullOrEmpty(existingQuery))
                AutoGrowTextBox(0, 24, 120, 16, 32);

            PositionNearCursor();
            ActiveControl = _queryTextBox;
        }

        // =====================================================================
        // Auto-grow text box
        // =====================================================================

        private void AutoGrowTextBox(int textBoxTopDesign, int minHDesign, int maxHDesign, int padDesign, int btnHDesign)
        {
            if (!_layoutReady) return;

            // Get the DPI scale factor from the actual vs design font metrics.
            float scale = CurrentAutoScaleDimensions.Height / 15F; // 15F = design baseline
            int minH = (int)(minHDesign * scale);
            int maxH = (int)(maxHDesign * scale);

            // Measure how tall the text actually needs to be.
            Size proposed = new Size(_queryTextBox.Width - 8, int.MaxValue);
            int contentH = TextRenderer.MeasureText(
                _queryTextBox.Text + " ", _queryTextBox.Font, proposed,
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 8;

            int newH = Math.Clamp(contentH, minH, maxH);
            if (newH == _queryTextBox.Height) return;

            SuspendLayout();

            _queryTextBox.Height = newH;
            _queryTextBox.ScrollBars = newH >= maxH ? ScrollBars.Vertical : ScrollBars.None;

            // Reposition buttons below textbox, then size form to fit.
            int btnY = _queryTextBox.Bottom + _btnGap;
            _addButton.Top  = btnY;
            _skipButton.Top = btnY;
            ClientSize = new Size(ClientSize.Width, _skipButton.Bottom + _btnGap);

            ResumeLayout(true);
        }

        // =====================================================================
        // Positioning
        // =====================================================================

        private void PositionNearCursor()
        {
            Point cursor = Cursor.Position;
            int x = cursor.X + 16;
            int y = cursor.Y + 16;

            Screen screen = Screen.FromPoint(cursor);
            Rectangle workArea = screen.WorkingArea;

            if (x + Width > workArea.Right)
                x = workArea.Right - Width - 4;
            if (y + Height > workArea.Bottom)
                y = workArea.Bottom - Height - 4;

            x = Math.Max(x, workArea.Left);
            y = Math.Max(y, workArea.Top);

            Location = new Point(x, y);
        }

        // =====================================================================
        // Keyboard
        // =====================================================================

        private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                e.SuppressKeyPress = true;
                int caret = _queryTextBox.SelectionStart;
                _queryTextBox.Text = _queryTextBox.Text.Insert(caret, Environment.NewLine);
                _queryTextBox.SelectionStart = caret + Environment.NewLine.Length;
            }
        }

        // =====================================================================
        // Placeholder helpers
        // =====================================================================

        private static readonly Color PlaceholderColor = SystemColors.GrayText;
        private static readonly Color NormalColor      = SystemColors.WindowText;

        private static void ApplyPlaceholder(TextBox box, string placeholder)
        {
            if (!string.IsNullOrEmpty(box.Text))
                return;

            box.ForeColor = PlaceholderColor;
            box.Text      = placeholder;

            box.GotFocus += (_, _) =>
            {
                if (box.ForeColor == PlaceholderColor)
                {
                    box.Text      = string.Empty;
                    box.ForeColor = NormalColor;
                }
            };

            box.LostFocus += (_, _) =>
            {
                if (string.IsNullOrEmpty(box.Text))
                {
                    box.ForeColor = PlaceholderColor;
                    box.Text      = placeholder;
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK &&
                _queryTextBox.ForeColor == PlaceholderColor)
            {
                _queryTextBox.Text = string.Empty;
            }
            base.OnFormClosing(e);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static string BuildHeaderText(string controlType, string name)
        {
            const int MaxNameLength = 50;
            string displayName = string.IsNullOrWhiteSpace(name)
                ? "(unnamed)"
                : (name.Length > MaxNameLength ? name[..MaxNameLength] + "..." : name);
            string displayType = string.IsNullOrWhiteSpace(controlType) ? "Element" : controlType;
            return $"{displayType} \"{displayName}\"";
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
