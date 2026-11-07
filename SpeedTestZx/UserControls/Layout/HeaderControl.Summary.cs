using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace SpeedTestZx.UserControls.Layout
{
    /// <summary>
    /// HeaderControl — the system summary strip on the left of the bar.
    ///
    /// Rendered as icon + text segments rather than one "|"-joined string: the
    /// icons are the same coloured Font Awesome glyphs the sidebar and pages
    /// use, so each fact reads at a glance. Same design as Net Speed Meter Plus.
    /// </summary>
    public partial class HeaderControl
    {
        /// <summary>
        /// One "icon + value" segment of the summary line. Tooltip defaults to
        /// Text; set it when Text is shortened (the processor name).
        /// </summary>
        public readonly record struct SummaryItem(IconChar Icon, string Text, Color Accent, string Tooltip = null);

        private static readonly Font SummaryFont = new("Segoe UI", 9f, FontStyle.Regular);

        private FlowLayoutPanel _summaryStrip;
        private IReadOnlyList<SummaryItem> _summaryItems = [];
        private string _summaryFallback = "Loading system information...";

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private FlowLayoutPanel BuildSummaryStrip() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Padding = SummaryPadding()
        };

        /// <summary>Left inset plus a top inset that centres one text line vertically.</summary>
        private Padding SummaryPadding() => new(
            DpiAwareService.Scale(16),
            Math.Max(0, (Height - DpiAwareService.Scale(22)) / 2),
            0, 0);

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the summary as coloured icon + value segments. Safe to call from
        /// a background thread — marshalling is handled here.
        /// </summary>
        public void SetSummary(IReadOnlyList<SummaryItem> items)
        {
            _summaryItems = items ?? [];
            _summaryFallback = null;

            RenderSummary();
        }

        /// <summary>Plain one-line summary — used for the loading and error states.</summary>
        public void SetSummaryText(string text)
        {
            _summaryItems = [];
            _summaryFallback = text;

            RenderSummary();
        }

        // ─────────────────────────────────────────────────────────────────────
        // RENDER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Rebuilds the segments — also the theme path, since label colours are set at build time.</summary>
        private void RenderSummary()
        {
            if (_summaryStrip == null || _summaryStrip.IsDisposed) return;

            if (_summaryStrip.InvokeRequired)
            {
                // The window may have closed while the WMI query was running —
                // its handle is gone and BeginInvoke would throw.
                if (!_summaryStrip.IsHandleCreated) return;

                try { _summaryStrip.BeginInvoke(new Action(RenderSummary)); }
                catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
                return;
            }

            _summaryStrip.SuspendLayout();
            ClearSummary();

            if (_summaryFallback != null)
            {
                _summaryStrip.Controls.Add(TextLabel(_summaryFallback));
            }
            else
            {
                for (int i = 0; i < _summaryItems.Count; i++)
                {
                    // Spacing separates the segments instead of a "|" glyph —
                    // the separators cost width the header cannot spare.
                    _summaryStrip.Controls.Add(IconFor(_summaryItems[i], leadingGap: i > 0));
                    var label = TextLabel(_summaryItems[i].Text);
                    _toolTip.SetToolTip(label, _summaryItems[i].Tooltip ?? _summaryItems[i].Text);
                    _summaryStrip.Controls.Add(label);
                }
            }

            _summaryStrip.ResumeLayout(true);
        }

        private void ClearSummary()
        {
            var existing = new Control[_summaryStrip.Controls.Count];
            _summaryStrip.Controls.CopyTo(existing, 0);
            _summaryStrip.Controls.Clear();

            foreach (var control in existing)
                control.Dispose();
        }

        private static IconPictureBox IconFor(SummaryItem item, bool leadingGap) => new()
        {
            IconChar = item.Icon,
            IconColor = item.Accent,
            IconSize = DpiAwareService.Scale(13),
            Size = new Size(DpiAwareService.Scale(16), DpiAwareService.Scale(20)),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Margin = new Padding(
                leadingGap ? DpiAwareService.Scale(16) : 0,
                DpiAwareService.Scale(1),
                DpiAwareService.Scale(5),
                0),
            Enabled = false
        };

        private Label TextLabel(string text) => new()
        {
            Text = text,
            Font = SummaryFont,
            ForeColor = _themeManager.CurrentTheme.TextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, DpiAwareService.Scale(2), 0, 0)
        };
    }
}
