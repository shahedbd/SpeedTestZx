using TestNetSpeed.Helper;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// TestHistoryControl — paging (Pro_plan.md T49 / T21). Every test is now
    /// kept, so the table shows <see cref="PageSize"/> rows at a time with
    /// ‹ › buttons in the header, newest first. Also the "% of plan" colour.
    /// </summary>
    public partial class TestHistoryControl
    {
        private const int PageSize = 100;

        private int _page;
        private Label _lblPage;
        private Button _btnPrev, _btnNext;

        private int PageCount => Math.Max(1, (_results.Count + PageSize - 1) / PageSize);
        private int PageStart => Math.Min(_page * PageSize, _results.Count);
        private int PageEnd => Math.Min(PageStart + PageSize, _results.Count);

        private void BuildPager(Panel header, Button btnRefresh)
        {
            _btnPrev = PagerButton("‹", () => GoToPage(_page - 1));
            _btnNext = PagerButton("›", () => GoToPage(_page + 1));
            _lblPage = new Label
            {
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                Size = ScaledSize(140, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            header.Controls.AddRange(new Control[] { _lblPage, _btnPrev, _btnNext });

            header.SizeChanged += (s, e) =>
            {
                int gap = Scale(6), top = btnRefresh.Top;
                _btnNext.Location = new Point(btnRefresh.Left - gap - _btnNext.Width, top);
                _btnPrev.Location = new Point(_btnNext.Left - Scale(2) - _btnPrev.Width, top);
                _lblPage.Location = new Point(_btnPrev.Left - gap - _lblPage.Width, top);
            };
        }

        private Button PagerButton(string text, Action onClick)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 11f),
                Size = ScaledSize(28, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = themeManager.IsDarkTheme ? Color.FromArgb(55, 55, 55) : Color.FromArgb(226, 232, 240),
                ForeColor = themeManager.CurrentTheme.TextColor,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false,
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => onClick();
            return b;
        }

        private void GoToPage(int page)
        {
            int clamped = Math.Clamp(page, 0, PageCount - 1);
            if (clamped == _page) return;
            _page = clamped;
            RenderRows();
        }

        private void UpdatePager()
        {
            if (_lblPage == null) return;

            bool paged = _results.Count > PageSize;
            _lblPage.Visible = _btnPrev.Visible = _btnNext.Visible = paged;
            _lblPage.Text = $"{PageStart + 1:N0}–{PageEnd:N0} of {_results.Count:N0}";
            _btnPrev.Enabled = _page > 0;
            _btnNext.Enabled = _page < PageCount - 1;
        }

        /// <summary>Green ≥ 80% of plan, amber ≥ 50%, red below; muted when not graded.</summary>
        private Color PlanColor(double? pct) => pct switch
        {
            null => themeManager.CurrentTheme.SecondaryTextColor,
            >= 80 => Color.FromArgb(16, 185, 129),
            >= 50 => Color.FromArgb(245, 158, 11),
            _ => Color.FromArgb(239, 68, 68),
        };
    }
}
