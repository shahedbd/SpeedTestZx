using FontAwesome.Sharp;
using SpeedTestZx.Helper;

namespace SpeedTestZx.UserControls.Pages
{
    /// <summary>
    /// "Our Apps" nav page — cross-promotion for the rest of the portfolio
    /// (ported from NetSpeedMeterPlus).
    ///
    /// The catalogue lives in <see cref="OurAppsCatalog"/>; this page lays it
    /// out as a heading (title, portfolio stats, search) over one scrolling
    /// card split into Featured (paid), Most Popular and More Apps sections,
    /// each a header plus <see cref="OurAppRow"/>s.
    /// </summary>
    public class OurAppsControl : DpiAwareUserControl
    {
        private static readonly Color MoreColor = Color.FromArgb(99, 102, 241); // indigo

        private readonly List<(Control Header, List<OurAppRow> Rows)> _sections = new();
        private TextBox _search;
        private Panel _list;
        private Label _empty;

        public OurAppsControl()
        {
            // This page does its own DPI scaling through Scale()/ScaledPadding,
            // so WinForms must not scale the finished layout a second time.
            AutoScaleMode = AutoScaleMode.None;

            BackColor = themeManager.CurrentTheme.ContentBackground;
            Padding = ScaledPadding(16, 10, 16, 16);

            Panel card = BuildCard();

            AddSection(IconChar.Crown, "Featured", "Premium tools with advanced features",
                OurAppRow.PremiumColor, OurAppsCatalog.Featured);
            AddSection(IconChar.Star, "Most Popular", "Our most installed free apps",
                OurAppRow.PopularColor, OurAppsCatalog.Popular);
            AddSection(IconChar.List, "More Apps", "Handy free utilities for everyday tasks",
                MoreColor, OurAppsCatalog.More);

            // Docked controls stack in reverse add order, so the sections are
            // added backwards to read top-down on screen.
            _list.SuspendLayout();
            foreach (var (header, rows) in Enumerable.Reverse(_sections))
            {
                foreach (var row in Enumerable.Reverse(rows))
                    _list.Controls.Add(row);
                _list.Controls.Add(header);
            }
            _list.ResumeLayout(true);

            // Card (Fill) added first, heading (Top) last — the later addition
            // claims the top strip and the card fills what's left.
            Controls.Add(card);
            Controls.Add(BuildHeading());
        }

        // ── Chrome ───────────────────────────────────────────────────────

        /// <summary>Title and portfolio stats on the left, search box on the right.</summary>
        private Control BuildHeading()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = Scale(64),
                BackColor = Color.Transparent,
            };

            panel.Controls.Add(new Label
            {
                Text = "Our Apps",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(0, 0),
            });

            panel.Controls.Add(new Label
            {
                Text = BuildStats(),
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(Scale(2), Scale(34)),
            });

            _search = new TextBox
            {
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "Search apps or categories…",
                Width = Scale(240),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = themeManager.CurrentTheme.CardBackground,
                ForeColor = themeManager.CurrentTheme.TextColor,
            };
            _search.TextChanged += (s, e) => ApplyFilter();
            panel.Controls.Add(_search);

            // Placed on every resize rather than anchored: anchoring computes
            // its offset from the initial layout, which is wrong before the
            // panel has its real width.
            void PlaceSearch() =>
                _search.Location = new Point(
                    Math.Max(0, panel.ClientSize.Width - _search.Width), Scale(8));

            panel.Resize += (s, e) => PlaceSearch();
            PlaceSearch();

            return panel;
        }

        /// <summary>"21 Microsoft Store apps by Zero Byte Software Solutions  ·  1.3M+ installs"</summary>
        private static string BuildStats()
        {
            long total = OurAppsCatalog.All.Sum(a => (long)a.Acquisitions);
            string installs = total >= 1_000_000
                ? $"{total / 100_000 / 10.0:0.#}M+ installs"
                : $"{total / 1_000}K+ installs";

            return $"{OurAppsCatalog.All.Count} Microsoft Store apps by {OurAppsCatalog.DeveloperShortName}  ·  {installs}";
        }

        /// <summary>Bordered card holding the scrolling section list.</summary>
        private Panel BuildCard()
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
            };

            _list = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = ScaledPadding(10, 4, 10, 10),
            };

            _empty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "No app matches that search.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
            };

            card.Controls.Add(_empty);
            card.Controls.Add(_list);
            return card;
        }

        // ── Sections ─────────────────────────────────────────────────────

        private void AddSection(IconChar icon, string title, string subtitle, Color accent, IEnumerable<OurApp> apps)
        {
            var rows = apps.Select(a => new OurAppRow(a)).ToList();
            if (rows.Count == 0) return; // no empty section headers

            _sections.Add((BuildSectionHeader(icon, title, subtitle, accent), rows));
        }

        /// <summary>Accent glyph, uppercase title and a one-line subtitle.</summary>
        private Control BuildSectionHeader(IconChar icon, string title, string subtitle, Color accent)
        {
            int height = Scale(46);
            var header = new Panel { Dock = DockStyle.Top, Height = height, BackColor = Color.Transparent };

            header.Controls.Add(new IconPictureBox
            {
                Size = ScaledSize(16, 16),
                IconChar = icon,
                IconColor = accent,
                IconSize = Scale(16),
                Location = new Point(Scale(4), Scale(20)),
                BackColor = Color.Transparent,
                Enabled = false,
            });

            var lblTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = accent,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(Scale(26), Scale(18)),
            };
            header.Controls.Add(lblTitle);

            header.Controls.Add(new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(lblTitle.Left + lblTitle.PreferredWidth + Scale(8), Scale(19)),
            });

            return header;
        }

        // ── Search ───────────────────────────────────────────────────────

        /// <summary>
        /// Hides the rows that don't match, and any section left empty. Rows
        /// are Dock=Top, so the stack closes up on its own — no rebuild, and
        /// scroll position survives.
        /// </summary>
        private void ApplyFilter()
        {
            string query = _search.Text?.Trim() ?? string.Empty;
            int shown = 0;

            _list.SuspendLayout();
            foreach (var (header, rows) in _sections)
            {
                int inSection = 0;
                foreach (var row in rows)
                {
                    bool match = query.Length == 0 || Matches(row.App, query);
                    row.Visible = match;
                    if (match) inSection++;
                }
                header.Visible = inSection > 0;
                shown += inSection;
            }
            _list.ResumeLayout(true);

            _list.Visible = shown > 0;
            _empty.Visible = shown == 0;
        }

        private static bool Matches(OurApp app, string query) =>
            app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || app.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
