using System.Drawing.Drawing2D;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using FontAwesome.Sharp;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Prompts
{
    /// <summary>
    /// The upgrade card shown by ProPromptOverlay — badge, headline, unlock
    /// list, live Store price, CTA, "Not now" and the payment trust line. Copy
    /// comes from ProPromptContent.
    ///
    /// Same structure as CPU ZX Pro's PremiumPromptCard: a single-column
    /// TableLayoutPanel where every row is Anchor=None, so the block centres
    /// itself with no position maths, and the height is measured from content.
    ///
    /// This part builds the layout; chips live in ProPromptCard.Chips.cs,
    /// the purchase promises in ProPromptCard.Perks.cs,
    /// price/busy state and chrome in ProPromptCard.Price.cs.
    /// </summary>
    internal partial class ProPromptCard : Panel
    {
        /// <summary>Unlock chips shown before the list folds into "+N more".</summary>
        private const int MaxChipsShown = 6;

        private static Color AccentStart => AppConfig.ProAccentStart;
        private static Color AccentEnd => AppConfig.ProAccentEnd;

        private readonly ThemeManager _theme;
        private readonly float _dpiScale;
        private readonly ProPromptContent _content;

        private Label _lblPrice = null!;
        private IconButton _btnBuy = null!;

        /// <summary>Benefits beyond MaxChipsShown; drives the "+N more" row.</summary>
        private int _hiddenToolCount;

        /// <summary>User asked to buy — the overlay runs the purchase.</summary>
        public event EventHandler? BuyRequested;

        /// <summary>User chose "Not now".</summary>
        public event EventHandler? DismissRequested;

        /// <summary>User chose "Already purchased? Refresh".</summary>
        public event EventHandler? RestoreRequested;

        /// <summary>Short-window layout: no badge or perk grid, so the CTA and links fit.</summary>
        private readonly bool _compact;

        public bool IsCompact => _compact;

        public ProPromptCard(ThemeManager theme, float dpiScale, ProPromptContent content, bool compact = false)
        {
            _theme = theme;
            _dpiScale = dpiScale;
            _content = content;
            _compact = compact;

            // Width is fixed; height is measured from the content in BuildLayout.
            Width = S(560);
            BackColor = _theme.CurrentTheme.CardBackground;
            Padding = new Padding(S(28), S(28), S(28), S(20));
            DoubleBuffered = true;
            Paint += Card_Paint;

            BuildLayout();
            _ = LoadPriceAsync();
        }

        private int S(int value) => (int)Math.Round(value * _dpiScale);

        // ══════════════════════════════════════════════════════════════════
        //  LAYOUT
        // ══════════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(stack);

            // BuildFeatureGrid sets _hiddenToolCount, so it must run before the
            // "+N more" row is considered.
            var rows = new List<Control>();
            if (!_compact)
                rows.Add(BuildBadge());

            rows.Add(BuildText(_content.Headline, 18f, FontStyle.Bold, _theme.CurrentTheme.HeadingColor, 4));
            rows.Add(BuildText(_content.Subhead, 10f, FontStyle.Regular, _theme.CurrentTheme.SecondaryTextColor, 14));
            rows.Add(BuildFeatureGrid());

            if (_hiddenToolCount > 0)
                rows.Add(BuildMoreToolsLine());

            if (_content.Perks.Length > 0 && !_compact)
                rows.Add(BuildPerkGrid());

            rows.Add(BuildPriceBlock());
            rows.Add(BuildCta());
            rows.Add(BuildLink("Not now", 12, (s, e) => DismissRequested?.Invoke(this, EventArgs.Empty)));
            rows.Add(BuildTrustLine());
            rows.Add(BuildLink("Already purchased? Refresh status", 0,
                (s, e) => RestoreRequested?.Invoke(this, EventArgs.Empty)));

            stack.RowCount = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                rows[i].Anchor = AnchorStyles.None;
                stack.Controls.Add(rows[i], 0, i);
            }

            // Measure once the rows are in place so nothing at the bottom clips.
            Height = Padding.Vertical + stack.PreferredSize.Height;
        }

        /// <summary>Tinted circular star — the card sells the upgrade rather than flagging a lock.</summary>
        private Control BuildBadge()
        {
            int size = S(56);

            var badge = new Panel
            {
                Size = new Size(size, size),
                BackColor = Color.FromArgb(38, AccentEnd),
                Margin = new Padding(0, 0, 0, S(10))
            };

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, size, size);
                badge.Region = new Region(path);
            }

            badge.Controls.Add(new IconPictureBox
            {
                IconChar = IconChar.Star,
                IconColor = AccentStart,
                IconSize = S(24),
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Enabled = false
            });

            return badge;
        }

        private Control BuildText(string text, float size, FontStyle style, Color color, int bottomGap) => new Label
        {
            Text = text,
            UseMnemonic = false,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, S(bottomGap))
        };

        /// <summary>
        /// The same gradient pill + gold crown as the header's Go Pro button
        /// (UserControls/Lib/PremiumButton.cs), so the offer looks identical from
        /// wherever the user reached this card. Wide enough for the busy text.
        /// </summary>
        private Control BuildCta()
        {
            _btnBuy = PremiumButton.Create(
                new Size(S(280), S(46)), S(18), fontSize: 11f, text: BuyLabel.Trim());

            _btnBuy.Margin = new Padding(0, 0, 0, S(8));
            _btnBuy.Click += (s, e) => BuyRequested?.Invoke(this, EventArgs.Empty);

            return _btnBuy;
        }

        private Control BuildLink(string text, int bottomGap, EventHandler onClick)
        {
            var link = new LinkLabel
            {
                Text = text,
                Font = new Font("Segoe UI", 9f),
                LinkColor = _theme.CurrentTheme.SecondaryTextColor,
                ActiveLinkColor = AccentStart,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, S(bottomGap))
            };
            link.LinkClicked += (s, e) => onClick(s, e);
            return link;
        }

        /// <summary>Payment reassurance directly under the CTA.</summary>
        private Control BuildTrustLine()
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, S(4), 0, S(6))
            };

            // Microsoft Store badge, as in CPU ZX Pro; a lock glyph if the
            // asset is missing, so the line never loses its icon.
            if (StoreBadge != null)
            {
                row.Controls.Add(new PictureBox
                {
                    Image = StoreBadge,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(S(32), S(32)),
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, S(2), S(10), 0)
                });
            }
            else
            {
                row.Controls.Add(new IconPictureBox
                {
                    IconChar = IconChar.Lock,
                    IconColor = _theme.CurrentTheme.SecondaryTextColor,
                    IconSize = S(12),
                    Size = new Size(S(16), S(18)),
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Margin = new Padding(0, 0, S(4), 0),
                    Enabled = false
                });
            }

            row.Controls.Add(new Label
            {
                Text = "Paid securely through the Microsoft Store\n"
                     + "No card details ever reach this app",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = _theme.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, S(1), 0, 0)
            });

            return row;
        }
    }
}
