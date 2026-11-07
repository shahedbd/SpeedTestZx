using SpeedTestZx.UserControls.Lib;
using SpeedTestZx.UserControls.Pages.Settings;

namespace SpeedTestZx.UserControls.Pages
{
    public partial class SettingsPageControl
    {
        private TextBox _txtSearch;
        private Panel _host;
        private Button _btnReset;
        private readonly List<Label> _navLinks = new();
        private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 150 };
        private bool _suppressSearch;

        /// <summary>
        /// Category column (search + group headings + links) on the left, a
        /// footer strip under the content, and the scrolling host filling the
        /// rest. Docking order matters: the last control added docks first, so
        /// the column spans the full height and the footer only the content side.
        /// </summary>
        private void BuildChrome()
        {
            var theme = themeManager.CurrentTheme;

            _host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = theme.ContentBackground };
            _host.Resize += (s, e) => SizeContent();
            Controls.Add(_host);

            Controls.Add(BuildFooter());
            Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = theme.BorderColor });
            Controls.Add(BuildNavColumn());

            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RunSearch();
            };
        }

        private Panel BuildNavColumn()
        {
            var theme = themeManager.CurrentTheme;
            var nav = new Panel
            {
                Dock = DockStyle.Left,
                Width = Scale(200),
                BackColor = theme.ContentBackground,
                Padding = ScaledPadding(12, 8, 12, 12)
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search settings  (Ctrl+F)",
                Font = ScaledFont(9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = themeManager.IsDarkTheme ? Color.FromArgb(45, 45, 45) : Color.White,
                ForeColor = theme.TextColor,
                Location = new Point(nav.Padding.Left, nav.Padding.Top),
                Width = nav.Width - nav.Padding.Horizontal,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AccessibleName = "Search settings"
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                if (_suppressSearch) return;
                _searchTimer.Stop();
                _searchTimer.Start();
            };
            nav.Controls.Add(_txtSearch);

            int y = _txtSearch.Bottom + Scale(10);
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                if (GroupHeadings.TryGetValue(i, out var heading))
                {
                    var group = MakeGroupHeading(heading, nav, y);
                    nav.Controls.Add(group);
                    y = group.Bottom + Scale(2);
                }

                var link = MakeNavLink(i, nav, y);
                _navLinks.Add(link);
                nav.Controls.Add(link);
                y += link.Height + Scale(2);
            }
            return nav;
        }

        /// <summary>"APP" / "DATA" — small caps above a group of links.</summary>
        private Label MakeGroupHeading(string text, Panel nav, int y) => new()
        {
            Text = text,
            UseMnemonic = false, // "&" is literal text, not an access key
            Font = ScaledFont(7.5f, FontStyle.Bold),
            ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = ScaledPadding(4, 0, 0, 2),
            Location = new Point(nav.Padding.Left, y),
            Size = new Size(nav.Width - nav.Padding.Horizontal, Scale(28)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        private Label MakeNavLink(int index, Panel nav, int y)
        {
            var theme = themeManager.CurrentTheme;
            var link = new Label
            {
                Text = NavLabel(index),
                UseMnemonic = false, // "&" is literal text, not an access key
                Font = ScaledFont(9.5f),
                ForeColor = theme.TextColor,
                BackColor = theme.ContentBackground,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = ScaledPadding(14, 0, 0, 0),
                Location = new Point(nav.Padding.Left, y),
                Size = new Size(nav.Width - nav.Padding.Horizontal, Scale(34)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                AccessibleRole = AccessibleRole.Link,
                AccessibleName = CategoryNames[index]
            };
            link.Click += (s, e) => OpenCategoryFromNav(index);
            link.Paint += (s, e) => PaintNavLink(link, e);
            link.MouseEnter += (s, e) => link.BackColor = theme.HoverColor;
            link.MouseLeave += (s, e) => link.BackColor = IsActiveLink(link) ? theme.HoverColor : theme.ContentBackground;
            return link;
        }

        private Panel BuildFooter()
        {
            var theme = themeManager.CurrentTheme;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = Scale(52), BackColor = theme.ContentBackground };
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = theme.BorderColor });

            footer.Controls.Add(new Label
            {
                Text = "Changes apply and save instantly.",
                Font = ScaledFont(9f),
                ForeColor = theme.SecondaryTextColor,
                AutoSize = true,
                Location = ScaledPoint(24, 18),
                BackColor = Color.Transparent
            });

            _btnReset = new Button
            {
                Text = "Reset page",
                Font = ScaledFont(9f),
                Size = ScaledSize(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            SettingsStyle.ThemeGhostButton(_btnReset);
            _btnReset.Click += (s, e) => ResetCurrentPage();
            footer.Controls.Add(_btnReset);
            footer.Resize += (s, e) => _btnReset.Location = new Point(footer.Width - _btnReset.Width - Scale(24), Scale(12));
            return footer;
        }

        // ── Nav state ─────────────────────────────────────────────────────────

        private void OpenCategoryFromNav(int index)
        {
            if (_txtSearch.TextLength > 0) ClearSearchSilently();
            ShowCategory(index);
        }

        /// <summary>-1 = none (search results are showing).</summary>
        private void SetActiveNav(int index)
        {
            var theme = themeManager.CurrentTheme;
            for (int i = 0; i < _navLinks.Count; i++)
            {
                bool active = i == index;
                _navLinks[i].BackColor = active ? theme.HoverColor : theme.ContentBackground;
                _navLinks[i].Font = ScaledFont(9.5f, active ? FontStyle.Bold : FontStyle.Regular);
                _navLinks[i].Invalidate();
            }
        }

        private bool IsActiveLink(Label link) =>
            _shown is not SettingsSearchResults && _navLinks.IndexOf(link) == _current;

        /// <summary>The accent bar on the active link — the design's "▌ Colors".</summary>
        private void PaintNavLink(Label link, PaintEventArgs e)
        {
            if (!IsActiveLink(link)) return;
            using var bar = new SolidBrush(themeManager.CurrentTheme.AccentColor);
            e.Graphics.FillRectangle(bar, 0, Scale(8), Scale(3), link.Height - Scale(16));
        }

        // ── Sizing ────────────────────────────────────────────────────────────

        /// <summary>
        /// Width for the content: the host's client width minus a scrollbar's
        /// worth even when none shows, so a vertical scrollbar appearing never
        /// forces a horizontal one.
        /// </summary>
        private void SizeContent()
        {
            if (_host == null || _shown == null) return;
            int width = _host.ClientSize.Width - (_host.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth);
            if (width < Scale(200)) return;

            if (_shown is SettingsCategory category) category.SetContentWidth(width);
            else if (_shown is SettingsSearchResults results) results.SetContentWidth(width);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _searchTimer.Dispose();
                // Cached categories that aren't on screen aren't in the control
                // tree, so base.Dispose wouldn't reach them.
                foreach (var category in _categories)
                    if (category != null && category != _shown) category.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
