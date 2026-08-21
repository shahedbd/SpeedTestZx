using DpiCommon.Helper;
using System.Drawing.Drawing2D;

namespace TestNetSpeed.UserControls.Lib
{
    public class StatTileControl : DpiAwareUserControl
    {
        // ── Config record ─────────────────────────────────────────────────────
        public record TileConfig(
            string Icon,
            Color IconBackground,
            string Caption,
            string Value,
            string SubText,
            Color AccentColor,
            Color? ValueOverride = null
        );

        // ── State ─────────────────────────────────────────────────────────────
        private TileConfig _cfg;
        private Label _lblValue;
        private Label _lblSub;
        private Label _lblCaption;
        private bool _built = false;

        public StatTileControl(TileConfig cfg)
        {
            _cfg = cfg;
            this.BackColor = Color.Transparent;
            this.BorderStyle = BorderStyle.None;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  AUTO-BUILD when control gets a real size
        // ══════════════════════════════════════════════════════════════════════

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (this.Width > 0 && this.Height > 0)
                Rebuild();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  REBUILD
        // ══════════════════════════════════════════════════════════════════════

        public void Rebuild()
        {
            // Guards against a base-constructor timing quirk: on displays
            // scaled above 100%, DpiAwareUserControl's AutoScaleMode setup
            // triggers an internal auto-scale resize synchronously *during*
            // the base constructor call — before this class's own
            // constructor body (which sets _cfg) has run. OnSizeChanged is
            // virtual, so that early resize dispatches here with _cfg still
            // null. At 100% scale no rescale transform happens, so it never
            // fires early — this is why the bug only reproduced at 200%.
            // Harmless to bail out here: the real layout pass that follows
            // (once construction finishes and the control is actually
            // docked/sized by its parent) fires OnSizeChanged again with
            // _cfg set, and Rebuild() proceeds normally.
            if (_cfg == null) return;
            if (this.Width <= 0 || this.Height <= 0) return;

            this.Controls.Clear();
            _built = false;

            int w = this.Width;
            int h = this.Height;

            // ── Card ─────────────────────────────────────────────────────────
            Panel card = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(w, h),
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(card);

            // ── Left accent strip ─────────────────────────────────────────────
            card.Controls.Add(new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(Scale(4), h),
                BackColor = _cfg.AccentColor
            });

            // ── Icon circle ───────────────────────────────────────────────────
            int circleSize = Math.Min(Scale(42), h - Scale(16));
            int circleX = Scale(14);
            int circleY = (h - circleSize) / 2;

            Panel iconCircle = new Panel
            {
                Location = new Point(circleX, circleY),
                Size = new Size(circleSize, circleSize),
                BackColor = _cfg.IconBackground
            };
            MakeCircle(iconCircle);

            iconCircle.Controls.Add(new Label
            {
                Text = _cfg.Icon,
                // Fixed point size — Windows already renders fonts at the
                // correct pixel size per-DPI. Wrapping this in Scale() (as
                // it was before) double-scales it at >100%: the glyph comes
                // out ~2x too large for the circle and overflows/clips.
                Font = new Font("Segoe UI Emoji", 13f),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            card.Controls.Add(iconCircle);

            // ── Text block ────────────────────────────────────────────────────
            int textX = circleX + circleSize + Scale(10);
            int textW = w - textX - Scale(8);

            // Row heights
            int capH = Scale(14);
            int valH = Scale(22);
            int subH = Scale(14);
            int totalTextH = capH + valH + subH + Scale(2);
            int textY = (h - totalTextH) / 2;

            // Caption
            _lblCaption = new Label
            {
                Text = _cfg.Caption,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(textX, textY),
                Size = new Size(textW, capH),
                AutoEllipsis = true
            };
            card.Controls.Add(_lblCaption);

            // Value
            _lblValue = new Label
            {
                Text = _cfg.Value,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = _cfg.ValueOverride ?? _cfg.AccentColor,
                BackColor = Color.Transparent,
                Location = new Point(textX, textY + capH + Scale(1)),
                Size = new Size(textW, valH),
                AutoEllipsis = true
            };
            card.Controls.Add(_lblValue);

            // Sub-text
            _lblSub = new Label
            {
                Text = _cfg.SubText,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(textX, textY + capH + valH + Scale(2)),
                Size = new Size(textW, subH),
                AutoEllipsis = true
            };
            card.Controls.Add(_lblSub);

            _built = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LIVE UPDATE
        // ══════════════════════════════════════════════════════════════════════

        public void UpdateValue(string value)
        {
            if (_lblValue != null) _lblValue.Text = value;
        }

        public void UpdateSubText(string sub)
        {
            if (_lblSub != null) _lblSub.Text = sub;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════════════

        private static void MakeCircle(Panel p)
        {
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, p.Width, p.Height);
            p.Region = new Region(path);
        }
    }
}
