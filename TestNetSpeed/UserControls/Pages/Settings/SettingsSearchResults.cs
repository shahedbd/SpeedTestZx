using TestNetSpeed.Managers;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Pages.Settings
{
    /// <summary>
    /// Search results across every category, grouped by category (ported from
    /// PC Health Optimizer). Results are links, not copies of the controls —
    /// clicking one opens the category and scrolls to the real row, so each
    /// setting keeps exactly one live control.
    /// </summary>
    internal sealed class SettingsSearchResults : FlowLayoutPanel
    {
        private static int S(int value) => SettingsStyle.Scale(value);
        private static AppTheme Theme => ThemeManager.Instance.CurrentTheme;

        private readonly List<SettingsCard> _cards = new();

        public SettingsSearchResults(string query, IEnumerable<SettingsCategory.SearchEntry> entries,
            Action<SettingsCategory.SearchEntry> onPick)
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.ContentBackground;
            Padding = new Padding(S(24), S(4), S(24), S(12));

            var matches = entries.Where(e => Matches(e, query)).ToList();

            Controls.Add(Heading(matches.Count == 0
                ? $"No results for '{query}'"
                : $"{matches.Count} result{(matches.Count == 1 ? "" : "s")} for '{query}'", 16f, FontStyle.Bold, Theme.HeadingColor, 2));
            Controls.Add(Heading(matches.Count == 0
                ? "Try a shorter word, like 'theme' or 'tray'."
                : "Click a result to jump to it.", 9.5f, FontStyle.Regular, Theme.SecondaryTextColor, 18));

            foreach (var group in matches.GroupBy(m => m.Category))
            {
                Controls.Add(Heading(group.Key.Title.ToUpperInvariant(), 7.5f, FontStyle.Bold, Theme.SecondaryTextColor, 6));
                var card = new SettingsCard();
                bool first = true;
                foreach (var entry in group)
                {
                    if (!first) card.AddDivider();
                    card.AddFull(MakeLink(entry, onPick));
                    first = false;
                }
                _cards.Add(card);
                Controls.Add(card);
            }
        }

        public void SetContentWidth(int width)
        {
            int inner = Math.Max(S(320), width - Padding.Horizontal);
            foreach (var card in _cards) card.SetWidth(inner);
            MinimumSize = new Size(width, 0);
            MaximumSize = new Size(width, 0);
        }

        /// <summary>
        /// Every word typed must prefix-match a word of the title, description
        /// or keywords — so "lock pos" finds "Lock widget position".
        /// </summary>
        internal static bool Matches(SettingsCategory.SearchEntry entry, string query)
        {
            var words = $"{entry.Title} {entry.Description} {string.Join(' ', entry.Keywords)} {entry.Category.Title}"
                .Split(new[] { ' ', '/', '\\', '-', ',', '.', '&', '\'', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(q => words.Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase)));
        }

        private static Control MakeLink(SettingsCategory.SearchEntry entry, Action<SettingsCategory.SearchEntry> onPick)
        {
            var link = new Panel { Height = S(46), Cursor = Cursors.Hand, BackColor = Theme.CardBackground };

            var title = new Label
            {
                Text = entry.Title,
                UseMnemonic = false, // "&" is literal text, not an access key
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Theme.TextColor,
                AutoSize = true,
                Location = new Point(0, S(4))
            };
            var desc = new Label
            {
                Text = entry.Description,
                UseMnemonic = false, // "&" is literal text, not an access key
                Font = new Font("Segoe UI", 8f),
                ForeColor = Theme.SecondaryTextColor,
                AutoSize = true,
                Location = new Point(0, S(24))
            };
            var arrow = new Label
            {
                Text = "›",
                Font = new Font("Segoe UI", 14f),
                ForeColor = Theme.SecondaryTextColor,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            link.Controls.AddRange(new Control[] { title, desc, arrow });
            link.Resize += (s, e) => arrow.Location = new Point(link.Width - arrow.Width, S(8));

            foreach (Control c in new Control[] { link, title, desc, arrow })
            {
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => onPick(entry);
                c.MouseEnter += (s, e) => link.BackColor = Theme.HoverColor;
                c.MouseLeave += (s, e) => link.BackColor = Theme.CardBackground;
            }
            title.BackColor = desc.BackColor = arrow.BackColor = Color.Transparent;
            return link;
        }

        private static Label Heading(string text, float size, FontStyle style, Color color, int bottom) => new()
        {
            Text = text,
            UseMnemonic = false, // "&" is literal text, not an access key
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, S(bottom))
        };
    }
}
