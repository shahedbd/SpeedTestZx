using TestNetSpeed.Managers;
using TestNetSpeed.UserControls.Lib.DataCard;

namespace TestNetSpeed.UserControls.Lib
{
    /// <summary>
    /// The Dashboard's card look (bordered panel, bold title, small info
    /// text) for the ISP Report Card and Connection Monitor pages.
    /// </summary>
    public static class PageCards
    {
        private static AppTheme Theme => ThemeManager.Instance.CurrentTheme;

        public static Panel Card() => new()
        {
            BackColor = Theme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle,
        };

        public static Label Title(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Theme.HeadingColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        public static Label Info(string text = "", bool bold = false) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = bold ? Theme.TextColor : Theme.SecondaryTextColor,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        public static ComboBox Combo(params string[] items)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
            };
            combo.Items.AddRange(items);
            SettingsStyle.ThemeInput(combo);
            return combo;
        }

        public static CheckBox Check(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Theme.TextColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };

        public static Button Button(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                AutoSize = true,
            };
            SettingsStyle.ThemeGhostButton(b);
            return b;
        }

        /// <summary>Data cards are theme-agnostic — map this app's theme onto their palette.</summary>
        public static DataCardPalette Palette() => DataCardPalette.From(
            Theme.CardBackground, Theme.BorderColor, Theme.TextColor,
            Theme.SecondaryTextColor, ThemeManager.Instance.IsDarkTheme);
    }
}
