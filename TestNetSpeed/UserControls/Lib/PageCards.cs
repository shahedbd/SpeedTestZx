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
        private static readonly Font HeaderFont = new("Segoe UI", 8.5f, FontStyle.Bold);

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
            ThemeInput(combo);
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
            UseMnemonic = false,   // "&" is literal text ("latency & outages")
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

        /// <summary>
        /// Dark-theme colours for combo boxes and text boxes (SettingsStyle
        /// only themes buttons, which left them white on dark cards).
        /// </summary>
        public static void ThemeInput(Control input)
        {
            input.BackColor = ThemeManager.Instance.IsDarkTheme ? Color.FromArgb(45, 45, 45) : Color.White;
            input.ForeColor = Theme.TextColor;
            if (input is TextBox box) box.BorderStyle = BorderStyle.FixedSingle;
        }

        /// <summary>
        /// Details ListView with a themed header row (the system header stays
        /// white in dark mode). Rows keep the default drawing.
        /// </summary>
        public static ListView List()
        {
            var lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.CardBackground,
                ForeColor = Theme.TextColor,
                Font = new Font("Segoe UI", 9f),
                OwnerDraw = true,
            };
            lv.DrawItem += (s, e) => e.DrawDefault = true;
            lv.DrawSubItem += (s, e) => e.DrawDefault = true;
            lv.DrawColumnHeader += (s, e) =>
            {
                bool dark = ThemeManager.Instance.IsDarkTheme;
                using var bg = new SolidBrush(dark ? Color.FromArgb(38, 38, 38) : Color.FromArgb(226, 232, 240));
                e.Graphics.FillRectangle(bg, e.Bounds);
                var text = Rectangle.Inflate(e.Bounds, -6, 0);
                TextRenderer.DrawText(e.Graphics, e.Header?.Text, HeaderFont, text,
                    Theme.SecondaryTextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            return lv;
        }

        /// <summary>Data cards are theme-agnostic — map this app's theme onto their palette.</summary>
        public static DataCardPalette Palette() => DataCardPalette.From(
            Theme.CardBackground, Theme.BorderColor, Theme.TextColor,
            Theme.SecondaryTextColor, ThemeManager.Instance.IsDarkTheme);
    }
}
