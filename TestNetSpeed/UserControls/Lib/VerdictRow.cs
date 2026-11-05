using DpiCommon.Helper;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;

namespace TestNetSpeed.UserControls.Lib
{
    /// <summary>
    /// One Quality Score line: "Streaming   GREAT   Plenty of headroom" —
    /// use case, level (in its colour) and the reason. Shared by the Speed
    /// Test insights panel and the Dashboard's Last test card.
    /// </summary>
    public sealed class VerdictRow : Panel
    {
        private readonly Label _name, _level, _reason;
        private readonly ThemeManager _theme = ThemeManager.Instance;

        /// <summary>Width of the use-case column at 96 DPI.</summary>
        public int NameWidth { get; set; } = 96;

        /// <summary>Width of the level column at 96 DPI.</summary>
        public int LevelWidth { get; set; } = 64;

        public VerdictRow(string useCase)
        {
            BackColor = Color.Transparent;

            var theme = _theme.CurrentTheme;
            _name = new Label
            {
                Text = useCase,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = theme.TextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
            };
            _level = new Label
            {
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _reason = new Label
            {
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = theme.SecondaryTextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
            };

            Controls.AddRange(new Control[] { _name, _level, _reason });
            SizeChanged += (s, e) => LayoutRow();
            Clear();
        }

        public void Show(QualityVerdict v)
        {
            _level.Text = v.Level.ToString().ToUpperInvariant();
            _level.ForeColor = QualityScorer.LevelColor(v.Level);
            _reason.Text = v.Reason;
        }

        public void Clear()
        {
            _level.Text = "—";
            _level.ForeColor = _theme.CurrentTheme.SecondaryTextColor;
            _reason.Text = "";
        }

        private void LayoutRow()
        {
            int nameW = DpiAwareService.Scale(NameWidth);
            int levelW = DpiAwareService.Scale(LevelWidth);
            _name.SetBounds(0, 0, nameW, Height);
            _level.SetBounds(nameW, 0, levelW, Height);
            _reason.SetBounds(nameW + levelW, 0, Math.Max(0, Width - nameW - levelW), Height);
        }
    }
}
