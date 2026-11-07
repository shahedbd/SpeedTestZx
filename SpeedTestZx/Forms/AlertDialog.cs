using SpeedTestZx.Managers;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace SpeedTestZx.Forms
{
    public enum AlertType { Info, Success, Warning, Error }

    /// <summary>
    /// Modern, theme-aware replacement for MessageBox — a coloured icon,
    /// title, wrapped message, and either a single OK button or a Yes/No
    /// pair. Built via AlertService, not directly.
    /// </summary>
    public class AlertDialog : Form
    {
        private readonly ThemeManager _themeManager;
        private readonly AlertType _type;

        private Panel _iconCircle;
        private IconPictureBox _icon;
        private Label _lblTitle;
        private Label _lblMessage;
        private Button _btnPrimary;
        private Button _btnSecondary;

        public AlertDialog(
            string title,
            string message,
            AlertType type,
            bool isConfirm,
            string primaryText,
            string secondaryText)
        {
            _themeManager = ThemeManager.Instance;
            _type = type;

            InitializeComponent(title);
            BuildLayout(title, message, isConfirm, primaryText, secondaryText);
            ApplyTheme();

            _themeManager.ThemeChanged += OnThemeChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────────────────────────────

        private void InitializeComponent(string title)
        {
            SuspendLayout();

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = title;
            AutoScaleMode = AutoScaleMode.Dpi;

            ResumeLayout(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private void BuildLayout(
            string title, string message, bool isConfirm,
            string primaryText, string secondaryText)
        {
            int width = DpiAwareService.Scale(380);
            int contentW = width - DpiAwareService.Scale(48);
            int contentX = DpiAwareService.Scale(24);
            int y = DpiAwareService.Scale(28);

            // ── Icon circle ──────────────────────────────────────────────────
            int circleSize = DpiAwareService.Scale(56);
            _iconCircle = new Panel
            {
                Size = new Size(circleSize, circleSize),
                Location = new Point((width - circleSize) / 2, y)
            };
            _icon = new IconPictureBox
            {
                IconChar = IconForType(_type),
                IconColor = Color.White,
                IconSize = DpiAwareService.Scale(26),
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            _iconCircle.Controls.Add(_icon);
            MakeCircle(_iconCircle);
            Controls.Add(_iconCircle);
            y += circleSize + DpiAwareService.Scale(14);

            // ── Title ─────────────────────────────────────────────────────────
            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(contentX, y),
                Size = new Size(contentW, DpiAwareService.Scale(24)),
                BackColor = Color.Transparent
            };
            Controls.Add(_lblTitle);
            y += DpiAwareService.Scale(28);

            // ── Message — wrapped, height measured from actual text ────────────
            var msgFont = new Font("Segoe UI", 9.5f);
            int msgHeight = TextRenderer.MeasureText(
                message, msgFont, new Size(contentW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter).Height;

            _lblMessage = new Label
            {
                Text = message,
                Font = msgFont,
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(contentX, y),
                Size = new Size(contentW, msgHeight + DpiAwareService.Scale(4)),
                BackColor = Color.Transparent
            };
            Controls.Add(_lblMessage);
            y += _lblMessage.Height + DpiAwareService.Scale(26);

            // ── Buttons ───────────────────────────────────────────────────────
            _btnPrimary = CreateButton(primaryText, solid: true);
            _btnPrimary.DialogResult = isConfirm ? DialogResult.Yes : DialogResult.OK;

            if (isConfirm)
            {
                _btnSecondary = CreateButton(secondaryText, solid: false);
                _btnSecondary.DialogResult = DialogResult.No;

                int gap = DpiAwareService.Scale(10);
                int totalW = _btnSecondary.Width + _btnPrimary.Width + gap;
                int startX = (width - totalW) / 2;

                _btnSecondary.Location = new Point(startX, y);
                _btnPrimary.Location = new Point(startX + _btnSecondary.Width + gap, y);

                Controls.Add(_btnSecondary);
                CancelButton = _btnSecondary;
            }
            else
            {
                _btnPrimary.Location = new Point((width - _btnPrimary.Width) / 2, y);
                CancelButton = _btnPrimary;
            }

            Controls.Add(_btnPrimary);
            AcceptButton = _btnPrimary;
            y += _btnPrimary.Height + DpiAwareService.Scale(24);

            ClientSize = new Size(width, y);
        }

        private Button CreateButton(string text, bool solid)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f, solid ? FontStyle.Bold : FontStyle.Regular),
                Size = DpiAwareService.ScaledSize(110, 34),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = solid ? 0 : 1;
            return btn;
        }

        private static IconChar IconForType(AlertType type) => type switch
        {
            AlertType.Success => IconChar.CircleCheck,
            AlertType.Warning => IconChar.TriangleExclamation,
            AlertType.Error => IconChar.CircleXmark,
            _ => IconChar.CircleInfo
        };

        private static Color ColorForType(AlertType type) => type switch
        {
            AlertType.Success => Color.FromArgb(34, 197, 94),
            AlertType.Warning => Color.FromArgb(245, 158, 11),
            AlertType.Error => Color.FromArgb(239, 68, 68),
            _ => Color.FromArgb(59, 130, 246)
        };

        private static void MakeCircle(Panel p)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, p.Width, p.Height);
            p.Region = new Region(path);
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            var theme = _themeManager.CurrentTheme;
            Color accent = ColorForType(_type);

            BackColor = theme.ContentBackground;
            _iconCircle.BackColor = accent;
            _lblTitle.ForeColor = theme.HeadingColor;
            _lblMessage.ForeColor = theme.SecondaryTextColor;

            _btnPrimary.BackColor = accent;
            _btnPrimary.ForeColor = Color.White;
            _btnPrimary.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(accent, 0.08f);

            if (_btnSecondary != null)
            {
                _btnSecondary.BackColor = theme.CardBackground;
                _btnSecondary.ForeColor = theme.TextColor;
                _btnSecondary.FlatAppearance.BorderColor = theme.BorderColor;
                _btnSecondary.FlatAppearance.MouseOverBackColor = theme.HoverColor;
            }

            Invalidate(true);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyTheme();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _themeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
