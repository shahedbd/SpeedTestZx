using FontAwesome.Sharp;
using TestNetSpeed.Helper;
using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Lib;
using TestNetSpeed.UserControls.Lib.Charts;
using TestNetSpeed.UserControls.Lib.DataCard;
using TestNetSpeed.UserControls.Prompts;

namespace TestNetSpeed.UserControls.Pages.Report
{
    /// <summary>
    /// "ISP Report Card" nav page (Pro_plan.md Phase 1): "Am I getting what I
    /// pay for?"
    ///
    ///   • Plan card — ISP, plan speeds, price, Edit (T33).
    ///   • Summary cards — median % of plan, share below 50%, worst hours,
    ///     uptime (T51).
    ///   • % of plan over time — daily median, with a 100% line.
    ///   • By hour of day (Pro) and the Report card: cost view (T52) and
    ///     PDF export (T34, Pro).
    ///
    /// Free sees the last 7 days; 30 / 90 days and custom ranges are Pro.
    /// Pro-only parts are replaced by an upsell built from the user's data.
    /// This part builds and lays out; data in .Data.cs, PDF in .Export.cs.
    /// </summary>
    public partial class IspReportCardControl : DpiAwareUserControl
    {
        private const int PlanHeight = 52;
        private const int CardRowHeight = 90;
        private const int TrendHeight = 250;
        private const int BottomHeight = 240;
        private const int Gap = 10;
        private const int Pad = 12;
        private const int TwoColumnMinWidth = 700;

        private Panel _planCard = null!;
        private Label _lblPlan = null!;
        private Button _btnEditPlan = null!;

        private DataCardGrid _cards = null!;
        private DataCardControl _cardPct = null!, _cardBelow = null!, _cardWorst = null!, _cardUptime = null!;

        private Panel _trendCard = null!;
        private Label _lblTrendTitle = null!;
        private ComboBox _cmbRange = null!;
        private DateTimePicker _dtFrom = null!, _dtTo = null!;
        private CheckBox _chkWired = null!;
        private PlanTrendChart _trend = null!;

        private Panel _hourCard = null!;
        private Label _lblHourTitle = null!;
        private HourBarChart _hours = null!;
        private ProUpsellCard? _hourUpsell;

        private Panel _reportCard = null!;
        private Label _lblReportTitle = null!, _lblCost = null!, _lblReportNote = null!;
        private Button _btnExport = null!;
        private ProUpsellCard? _reportUpsell;

        private bool _inLayout;

        public IspReportCardControl()
        {
            BackColor = themeManager.CurrentTheme.ContentBackground;
            Padding = ScaledPadding(Pad);
            AutoScroll = true;
            DoubleBuffered = true;

            BuildPlanCard();
            BuildSummaryCards();
            BuildTrendCard();
            BuildHourCard();
            BuildReportCard();

            SizeChanged += (s, e) => DoLayout();
            HookData();
            DoLayout();
            _ = ReloadAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD
        // ══════════════════════════════════════════════════════════════════════

        private void BuildPlanCard()
        {
            _planCard = PageCards.Card();
            _lblPlan = PageCards.Info(bold: true);
            _lblPlan.TextAlign = ContentAlignment.MiddleLeft;
            _btnEditPlan = PageCards.Button("Edit plan");
            _btnEditPlan.Click += (s, e) => EditPlan();

            _planCard.Controls.AddRange(new Control[] { _lblPlan, _btnEditPlan });
            _planCard.SizeChanged += (s, e) =>
            {
                int pad = Scale(Pad), h = _planCard.ClientSize.Height;
                _btnEditPlan.Location = new Point(_planCard.ClientSize.Width - pad - _btnEditPlan.Width, (h - _btnEditPlan.Height) / 2);
                _lblPlan.SetBounds(pad, 0, Math.Max(0, _btnEditPlan.Left - pad * 2), h);
            };
            Controls.Add(_planCard);
        }

        private void BuildSummaryCards()
        {
            _cards = new DataCardGrid { Gap = 12 };
            _cardPct = AddCard(IconChar.Percent, "% OF PLAN", DataCardAccents.Blue);
            _cardBelow = AddCard(IconChar.ArrowTrendDown, "TESTS BELOW 50%", DataCardAccents.Amber);
            _cardWorst = AddCard(IconChar.Clock, "WORST HOURS", DataCardAccents.Red);
            _cardUptime = AddCard(IconChar.HeartPulse, "UPTIME", DataCardAccents.Green);
            _cards.ApplyPalette(PageCards.Palette());
            Controls.Add(_cards);
        }

        private DataCardControl AddCard(IconChar icon, string caption, Color accent)
        {
            var card = _cards.AddCard(new DataCardControl(icon, caption, accent));
            card.Value = "—";
            card.SubText = "Loading…";
            return card;
        }

        private void BuildTrendCard()
        {
            _trendCard = PageCards.Card();
            _lblTrendTitle = PageCards.Title("% of plan over time");

            _cmbRange = PageCards.Combo("Last 7 days", "Last 30 days  ★ Pro", "Last 90 days  ★ Pro", "Custom range  ★ Pro");
            _dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Visible = false };
            _dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Visible = false };
            _chkWired = PageCards.Check("Wired tests only");
            _chkWired.Visible = false;

            _trend = new PlanTrendChart { EmptyText = "Run a few speed tests to see your trend" };

            _trendCard.Controls.AddRange(new Control[] { _lblTrendTitle, _cmbRange, _dtFrom, _dtTo, _chkWired, _trend });
            _trendCard.SizeChanged += (s, e) => LayoutTrendCard();
            Controls.Add(_trendCard);
        }

        private void LayoutTrendCard()
        {
            int w = _trendCard.ClientSize.Width, h = _trendCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(Pad), rowH = Scale(26);
            int x = w - pad;

            _cmbRange.Width = Scale(170);
            x -= _cmbRange.Width;
            _cmbRange.Location = new Point(x, pad);

            foreach (var picker in new[] { _dtTo, _dtFrom })
            {
                picker.Width = Scale(110);
                if (!picker.Visible) continue;
                x -= picker.Width + Scale(6);
                picker.Location = new Point(x, pad);
            }

            if (_chkWired.Visible)
            {
                x -= _chkWired.Width + Scale(12);
                _chkWired.Location = new Point(x, pad + (rowH - _chkWired.Height) / 2);
            }

            _lblTrendTitle.SetBounds(pad, pad, Math.Max(0, x - pad * 2), rowH);
            int top = pad + rowH + Scale(8);
            _trend.SetBounds(pad, top, Math.Max(0, w - pad * 2), Math.Max(0, h - top - pad));
        }

        private void BuildHourCard()
        {
            _hourCard = PageCards.Card();
            _lblHourTitle = PageCards.Title("By hour of day");
            _hours = new HourBarChart { EmptyText = "Tests at different times of day fill this in" };
            _hourCard.Controls.AddRange(new Control[] { _lblHourTitle, _hours });
            _hourCard.SizeChanged += (s, e) => LayoutHourCard();
            Controls.Add(_hourCard);
        }

        private void LayoutHourCard()
        {
            int w = _hourCard.ClientSize.Width, h = _hourCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(Pad), rowH = Scale(26), top = pad + rowH + Scale(6);
            _lblHourTitle.SetBounds(pad, pad, Math.Max(0, w - pad * 2), rowH);
            var body = new Rectangle(pad, top, Math.Max(0, w - pad * 2), Math.Max(0, h - top - pad));
            _hours.Bounds = body;
            _hourUpsell?.SetBounds(body.X, body.Y, body.Width, body.Height);
        }

        private void BuildReportCard()
        {
            _reportCard = PageCards.Card();
            _lblReportTitle = PageCards.Title("Report");
            _lblCost = PageCards.Info(bold: true);
            _lblReportNote = PageCards.Info();
            _btnExport = PageCards.Button("  Export PDF report");
            _btnExport.Click += async (s, e) => await ExportPdfAsync();

            _reportCard.Controls.AddRange(new Control[] { _lblReportTitle, _lblCost, _lblReportNote, _btnExport });
            _reportCard.SizeChanged += (s, e) => LayoutReportCard();
            Controls.Add(_reportCard);
        }

        private void LayoutReportCard()
        {
            int w = _reportCard.ClientSize.Width, h = _reportCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int pad = Scale(Pad), rowH = Scale(26), inner = Math.Max(0, w - pad * 2);
            int y = pad;
            _lblReportTitle.SetBounds(pad, y, inner, rowH);
            y += rowH + Scale(6);

            if (_reportUpsell != null)
            {
                _reportUpsell.SetBounds(pad, y, inner, Math.Max(0, h - y - pad));
                return;
            }

            _lblCost.SetBounds(pad, y, inner, Scale(60));
            y += Scale(64);
            _btnExport.Location = new Point(pad, y);
            y += _btnExport.Height + Scale(8);
            _lblReportNote.SetBounds(pad, y, inner, Math.Max(0, h - y - pad));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PAGE LAYOUT
        // ══════════════════════════════════════════════════════════════════════

        private void DoLayout()
        {
            if (_inLayout) return;
            _inLayout = true;
            try
            {
                var pad = Padding;
                int w = ClientSize.Width - pad.Horizontal;
                if (w <= 0) return;

                int gap = Scale(Gap);
                int x = pad.Left + AutoScrollPosition.X;
                int y = pad.Top + AutoScrollPosition.Y, top = y;

                _planCard.SetBounds(x, y, w, Scale(PlanHeight));
                y += Scale(PlanHeight) + gap;
                _cards.SetBounds(x, y, w, Scale(CardRowHeight));
                y += Scale(CardRowHeight) + gap;
                _trendCard.SetBounds(x, y, w, Scale(TrendHeight));
                y += Scale(TrendHeight) + gap;

                int bh = Scale(BottomHeight);
                if (w >= Scale(TwoColumnMinWidth))
                {
                    int colW = (w - gap) * 3 / 5;
                    _hourCard.SetBounds(x, y, colW, bh);
                    _reportCard.SetBounds(x + colW + gap, y, w - colW - gap, bh);
                    y += bh;
                }
                else
                {
                    _hourCard.SetBounds(x, y, w, bh);
                    y += bh + gap;
                    _reportCard.SetBounds(x, y, w, bh);
                    y += bh;
                }

                int needed = y - top + pad.Vertical;
                var min = needed > ClientSize.Height ? new Size(0, needed) : Size.Empty;
                if (AutoScrollMinSize != min) AutoScrollMinSize = min;
            }
            finally
            {
                _inLayout = false;
            }
        }
    }
}
