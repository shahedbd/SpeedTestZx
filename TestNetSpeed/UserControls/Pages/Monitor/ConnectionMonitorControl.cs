using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.UserControls.Lib;
using SpeedTestZx.UserControls.Lib.Charts;
using SpeedTestZx.UserControls.Lib.DataCard;
using SpeedTestZx.UserControls.Prompts;

namespace SpeedTestZx.UserControls.Pages.Monitor
{
    /// <summary>
    /// "Connection Monitor" nav page (Pro_plan.md Phase 2, T57): "When is my
    /// internet bad?"
    ///
    ///   • Cards — uptime 24 h, outages, median ping, next scheduled test.
    ///   • Heatmap — weekday × hour, speed (% of plan) or ping (Pro).
    ///   • Latency &amp; loss, last 24 h — line, loss ticks, outage bands.
    ///   • Outages — 24 h free, 90 days Pro.
    ///   • Schedule &amp; alerts — the switches for the whole Monitor.
    ///
    /// The engine (Service/Monitor) runs app-wide; this page only reads its
    /// files and settings. Build/layout here; data in .Data.cs; the schedule
    /// card in .Schedule.cs.
    /// </summary>
    public partial class ConnectionMonitorControl : DpiAwareUserControl
    {
        private const int CardRowHeight = 90;
        private const int HeatmapHeight = 230;
        private const int TimelineHeight = 200;
        private const int BottomHeight = 420;   // fits every schedule & alert option without a scrollbar
        private const int Gap = 10;
        private const int Pad = 12;
        private const int TwoColumnMinWidth = 760;

        private DataCardGrid _cards = null!;
        private DataCardControl _cardUptime = null!, _cardOutages = null!, _cardPing = null!, _cardNext = null!;

        private Panel _heatCard = null!;
        private Label _lblHeatTitle = null!;
        private ComboBox _cmbHeatMode = null!;
        private HeatmapControl _heatmap = null!;
        private ProUpsellCard? _heatUpsell;
        private bool _heatAllowed;

        private Panel _timelineCard = null!;
        private Label _lblTimelineTitle = null!, _lblTimelineStatus = null!;
        private LatencyTimelineChart _timeline = null!;

        private Panel _outageCard = null!;
        private Label _lblOutageTitle = null!, _lblOutageFooter = null!;
        private ListView _lvOutages = null!;

        private Panel _scheduleCard = null!;

        private bool _inLayout;

        private static bool IsPro => EntitlementManager.Instance.IsPro;

        public ConnectionMonitorControl()
        {
            BackColor = themeManager.CurrentTheme.ContentBackground;
            Padding = ScaledPadding(Pad);
            AutoScroll = true;
            DoubleBuffered = true;

            BuildCards();
            BuildHeatmapCard();
            BuildTimelineCard();
            BuildOutageCard();
            BuildScheduleCard();   // .Schedule.cs

            SizeChanged += (s, e) => DoLayout();
            HookData();            // .Data.cs
            DoLayout();
            _ = ReloadAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD
        // ══════════════════════════════════════════════════════════════════════

        private void BuildCards()
        {
            _cards = new DataCardGrid { Gap = 12 };
            _cardUptime = AddCard(IconChar.HeartPulse, "UPTIME · 24 H", DataCardAccents.Green);
            _cardOutages = AddCard(IconChar.TriangleExclamation, IsPro ? "OUTAGES · 7 DAYS" : "OUTAGES · 24 H", DataCardAccents.Red);
            _cardPing = AddCard(IconChar.Gauge, "MEDIAN PING · 24 H", DataCardAccents.Purple);
            _cardNext = AddCard(IconChar.Clock, "NEXT TEST", DataCardAccents.Blue);
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

        private void BuildHeatmapCard()
        {
            _heatCard = PageCards.Card();
            _lblHeatTitle = PageCards.Title("When is my internet bad?  Weekday × hour");
            _cmbHeatMode = PageCards.Combo("Speed", "Ping");
            _heatmap = new HeatmapControl { EmptyText = "Scheduled tests and the latency log fill this in over the coming days" };

            _heatCard.Controls.AddRange(new Control[] { _lblHeatTitle, _cmbHeatMode, _heatmap });

            _heatAllowed = EntitlementManager.Instance.HasAccess(ProFeature.Heatmap);
            if (!_heatAllowed)
            {
                _heatmap.Visible = false;
                _cmbHeatMode.Visible = false;
                _heatUpsell = new ProUpsellCard(ProFeature.Heatmap);
                _heatCard.Controls.Add(_heatUpsell);
            }

            _heatCard.SizeChanged += (s, e) =>
            {
                var body = LayoutTitled(_heatCard, _lblHeatTitle, _heatAllowed ? _cmbHeatMode : null);
                _heatmap.Bounds = body;
                _heatUpsell?.SetBounds(body.X, body.Y, body.Width, body.Height);
            };
            Controls.Add(_heatCard);
        }

        private void BuildTimelineCard()
        {
            _timelineCard = PageCards.Card();
            _lblTimelineTitle = PageCards.Title("Latency & loss · last 24 h");
            _lblTimelineStatus = PageCards.Info();
            _lblTimelineStatus.TextAlign = ContentAlignment.MiddleRight;
            _lblTimelineStatus.Width = Scale(300);
            _timeline = new LatencyTimelineChart();

            _timelineCard.Controls.AddRange(new Control[] { _lblTimelineTitle, _lblTimelineStatus, _timeline });
            _timelineCard.SizeChanged += (s, e) =>
                _timeline.Bounds = LayoutTitled(_timelineCard, _lblTimelineTitle, _lblTimelineStatus);
            Controls.Add(_timelineCard);
        }

        private void BuildOutageCard()
        {
            _outageCard = PageCards.Card();
            _lblOutageTitle = PageCards.Title("Outages");
            _lblOutageFooter = PageCards.Info();

            _lvOutages = PageCards.List();
            _lvOutages.Columns.Add("Start", Scale(120));
            _lvOutages.Columns.Add("End", Scale(70));
            _lvOutages.Columns.Add("Duration", Scale(80));
            _lvOutages.Columns.Add("Cause", Scale(120));

            _outageCard.Controls.AddRange(new Control[] { _lblOutageTitle, _lvOutages, _lblOutageFooter });
            _outageCard.SizeChanged += (s, e) =>
            {
                var body = LayoutTitled(_outageCard, _lblOutageTitle, null);
                int footerH = Scale(36);
                _lvOutages.SetBounds(body.X, body.Y, body.Width, Math.Max(0, body.Height - footerH));
                _lblOutageFooter.SetBounds(body.X, body.Bottom - footerH + Scale(4), body.Width, footerH - Scale(4));
            };
            Controls.Add(_outageCard);
        }

        /// <summary>Title on the left, optional control on the right; returns the body rectangle below.</summary>
        private Rectangle LayoutTitled(Panel card, Label title, Control? right)
        {
            int w = card.ClientSize.Width, h = card.ClientSize.Height;
            int pad = Scale(Pad), rowH = Scale(26);
            int titleRight = w - pad;

            if (right != null)
            {
                if (right is ComboBox) right.Width = Scale(110);
                right.SetBounds(w - pad - right.Width, pad, right.Width, right is ComboBox ? right.Height : rowH);
                titleRight = right.Left - Scale(8);
            }

            title.SetBounds(pad, pad, Math.Max(0, titleRight - pad), rowH);
            int top = pad + rowH + Scale(6);
            return new Rectangle(pad, top, Math.Max(0, w - pad * 2), Math.Max(0, h - top - pad));
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

                _cards.SetBounds(x, y, w, Scale(CardRowHeight));
                y += Scale(CardRowHeight) + gap;
                _heatCard.SetBounds(x, y, w, Scale(HeatmapHeight));
                y += Scale(HeatmapHeight) + gap;
                _timelineCard.SetBounds(x, y, w, Scale(TimelineHeight));
                y += Scale(TimelineHeight) + gap;

                int bh = Scale(BottomHeight);
                if (w >= Scale(TwoColumnMinWidth))
                {
                    int colW = (w - gap) / 2;
                    _outageCard.SetBounds(x, y, colW, bh);
                    _scheduleCard.SetBounds(x + colW + gap, y, w - colW - gap, bh);
                    y += bh;
                }
                else
                {
                    _scheduleCard.SetBounds(x, y, w, bh);
                    y += bh + gap;
                    _outageCard.SetBounds(x, y, w, bh);
                    y += bh;
                }

                int needed = y - top + pad.Vertical;
                var min = needed > ClientSize.Height ? new Size(0, needed) : Size.Empty;
                if (AutoScrollMinSize != min) AutoScrollMinSize = min;

                // Recompute the scroll extent: the first layout runs at the page's
                // tiny pre-dock size (cards stacked, ~1400 px tall), and AutoScroll
                // kept that stale extent, leaving a scrollbar with nothing below.
                AdjustFormScrollbars(AutoScroll);
            }
            finally
            {
                _inLayout = false;
            }
        }
    }
}
