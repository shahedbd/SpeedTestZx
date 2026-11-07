using DpiCommon.Helper;
using FontAwesome.Sharp;
using TestNetSpeed.Managers;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.About
{
    /// <summary>
    /// One About-screen card: accented header with a left accent bar, a list of
    /// icon rows, an optional note and an optional Go Pro call to action.
    /// Ported from CPU ZX Pro's AboutFeatureCard — the caller supplies title,
    /// accent, rows and CTA, so one control renders every card.
    /// </summary>
    public class AboutFeatureCard : Panel
    {
        private readonly ThemeManager _theme;
        private readonly Color _accent;

        private Panel? _header;
        private readonly List<Label> _rowLabels = new();
        private Label? _lblNote;

        /// <summary>Raised when the card's call to action is clicked.</summary>
        public event EventHandler? CtaClicked;

        public AboutFeatureCard(ThemeManager theme, int width, AboutCardSpec spec)
        {
            _theme = theme;
            _accent = spec.Accent;

            Width = width;
            BackColor = _theme.CurrentTheme.CardBackground;
            Margin = new Padding(0, 0, 0, S(14));

            Build(spec);
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private void Build(AboutCardSpec spec)
        {
            int headerH = S(38);
            AddHeader(spec.Icon, spec.Title, headerH);

            int y = headerH + S(8);

            foreach (var (rowIcon, text, rowAccent) in spec.Rows)
                y += AddRow(rowIcon, rowAccent, text, y);

            if (!string.IsNullOrWhiteSpace(spec.Note))
            {
                var font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                int noteWidth = Width - S(32);
                int noteHeight = TextRenderer.MeasureText(spec.Note, font,
                    new Size(noteWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + S(4);

                _lblNote = new Label
                {
                    Text = spec.Note,
                    Font = font,
                    ForeColor = _theme.CurrentTheme.SecondaryTextColor,
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    UseMnemonic = false,
                    Size = new Size(noteWidth, noteHeight),
                    Location = new Point(S(16), y + S(4)),
                    TextAlign = ContentAlignment.TopLeft
                };
                Controls.Add(_lblNote);
                y += noteHeight + S(8);
            }

            if (!string.IsNullOrWhiteSpace(spec.CtaText))
            {
                AddCta(spec.CtaText, y + S(4));
                y += S(46);
            }

            Height = y + S(10);
        }

        private void AddHeader(IconChar icon, string title, int headerH)
        {
            _header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(Width, headerH),
                BackColor = Color.FromArgb(28, _accent)
            };

            // Left accent bar.
            _header.Controls.Add(new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(S(4), headerH),
                BackColor = _accent
            });

            _header.Controls.Add(new IconPictureBox
            {
                IconChar = icon,
                IconColor = _accent,
                // Size before IconSize — IconPictureBox resizes to IconSize.
                Size = new Size(S(22), headerH),
                IconSize = S(16),
                Location = new Point(S(14), 0),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent
            });

            _header.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = _accent,
                BackColor = Color.Transparent,
                AutoSize = false,
                UseMnemonic = false,
                AutoEllipsis = true,
                Size = new Size(Width - S(48), headerH),
                Location = new Point(S(42), 0),
                TextAlign = ContentAlignment.MiddleLeft
            });

            Controls.Add(_header);
        }

        /// <summary>Icon + text row; wraps long text instead of clipping it. Returns the row height.</summary>
        private int AddRow(IconChar icon, Color accent, string text, int y)
        {
            var font = new Font("Segoe UI", 9f);
            int textWidth = Width - S(56);
            int textHeight = TextRenderer.MeasureText(text, font,
                new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            int rowH = Math.Max(S(24), textHeight + S(6));

            Controls.Add(new IconPictureBox
            {
                IconChar = icon,
                IconColor = accent,
                Size = new Size(S(20), S(24)),
                IconSize = S(14),
                Location = new Point(S(16), y),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Enabled = false
            });

            var label = new Label
            {
                Text = text,
                // Feature strings contain real ampersands.
                UseMnemonic = false,
                Font = font,
                ForeColor = _theme.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(textWidth, rowH),
                Location = new Point(S(44), y),
                TextAlign = rowH > S(24) ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft
            };
            if (rowH > S(24)) label.Padding = new Padding(0, S(3), 0, 0);

            _rowLabels.Add(label);
            Controls.Add(label);
            return rowH + S(2);
        }

        /// <summary>
        /// The shared gradient Go Pro pill (UserControls/Lib/PremiumButton),
        /// sized to its own label and centred — a full-width pill splits the
        /// crown and the words across the two halves of the button.
        /// </summary>
        private void AddCta(string text, int y)
        {
            var btn = PremiumButton.Create(new Size(Width - S(32), S(38)), S(15), fontSize: 9.5f, text: text);

            int content = TextRenderer.MeasureText(btn.Text, btn.Font).Width + S(70);
            btn.Size = new Size(Math.Clamp(content, S(200), Width - S(32)), S(38));
            btn.Location = new Point((Width - btn.Width) / 2, y);
            btn.Click += (s, e) => CtaClicked?.Invoke(this, e);

            // The gradient is a bitmap sized to the button — re-apply after resize.
            PremiumButton.Apply(btn);

            Controls.Add(btn);
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            var theme = _theme.CurrentTheme;

            BackColor = theme.CardBackground;

            if (_header != null)
                _header.BackColor = Color.FromArgb(28, _accent);

            foreach (var label in _rowLabels)
                label.ForeColor = theme.TextColor;

            if (_lblNote != null)
                _lblNote.ForeColor = theme.SecondaryTextColor;
        }
    }
}
