using FontAwesome.Sharp;

namespace SpeedTestZx.UserControls.Prompts
{
    /// <summary>
    /// ProPromptCard — the purchase promises (one-time payment, no ads,
    /// lifetime updates, lifetime support) as a 2-column grid of small tiles:
    /// icon, bold title, one-line detail. Sits just above the price, where the
    /// "is it worth it?" question is asked.
    /// </summary>
    internal partial class ProPromptCard
    {
        private const int PerkColumns = 2;

        private Control BuildPerkGrid()
        {
            var perks = _content.Perks;

            var grid = new TableLayoutPanel
            {
                ColumnCount = PerkColumns,
                RowCount = (perks.Length + PerkColumns - 1) / PerkColumns,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, S(12)),
                Padding = new Padding(0)
            };

            for (int c = 0; c < PerkColumns; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            for (int i = 0; i < perks.Length; i++)
            {
                var (icon, title, detail) = perks[i];
                grid.Controls.Add(BuildPerkTile(icon, title, detail), i % PerkColumns, i / PerkColumns);
            }

            return grid;
        }

        private Control BuildPerkTile(IconChar icon, string title, string detail)
        {
            var tile = new Panel
            {
                Size = new Size(S(248), S(40)),
                BackColor = Color.Transparent,
                Margin = new Padding(S(2), 0, S(2), S(6))
            };

            int textX = S(28);
            int textWidth = tile.Width - textX;

            tile.Controls.Add(new IconPictureBox
            {
                IconChar = icon,
                IconColor = _theme.CurrentTheme.SuccessColor,
                IconSize = S(15),
                Size = new Size(S(20), S(20)),
                Location = new Point(0, S(1)),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Enabled = false
            });

            tile.Controls.Add(new Label
            {
                Text = title,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = _theme.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(textWidth, S(20)),
                Location = new Point(textX, 0),
                TextAlign = ContentAlignment.MiddleLeft
            });

            tile.Controls.Add(new Label
            {
                Text = detail,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 8f),
                ForeColor = _theme.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(textWidth, S(18)),
                Location = new Point(textX, S(20)),
                TextAlign = ContentAlignment.TopLeft
            });

            return tile;
        }
    }
}
