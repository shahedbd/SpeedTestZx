using FontAwesome.Sharp;

namespace SpeedTestZx.UserControls.Prompts
{
    /// <summary>
    /// ProPromptCard — the unlock list: one chip per benefit, folding into a
    /// "+N more" line past MaxChipsShown.
    /// </summary>
    internal partial class ProPromptCard
    {
        private Control BuildFeatureGrid()
        {
            var benefits = _content.Benefits;
            _hiddenToolCount = Math.Max(0, benefits.Length - MaxChipsShown);

            var grid = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                // Tighter gap when the "+N more" line follows, so the two read as one block.
                Margin = new Padding(0, 0, 0, S(_hiddenToolCount > 0 ? 6 : 14))
            };

            foreach (var (icon, text, accent) in benefits.Take(MaxChipsShown))
                grid.Controls.Add(BuildFeatureChip(icon, accent, text));

            return grid;
        }

        /// <summary>"+N more" as its own row, so it gets the card's centring.</summary>
        private Control BuildMoreToolsLine() => new Label
        {
            Text = $"+{_hiddenToolCount} more",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = _theme.CurrentTheme.SecondaryTextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, S(16))
        };

        private Control BuildFeatureChip(IconChar icon, Color accent, string text)
        {
            var chip = new Panel
            {
                Size = new Size(S(440), S(28)),
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, S(4))
            };

            chip.Controls.Add(new IconPictureBox
            {
                IconChar = icon,
                IconColor = accent,
                IconSize = S(16),
                Size = new Size(S(22), chip.Height),
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Enabled = false
            });

            chip.Controls.Add(new Label
            {
                Text = text,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _theme.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(chip.Width - S(32), chip.Height),
                Location = new Point(S(32), 0),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });

            return chip;
        }
    }
}
