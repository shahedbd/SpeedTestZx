using System.Diagnostics;
using System.Drawing.Drawing2D;
using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;

namespace SpeedTestZx.UserControls.Pages
{
    /// <summary>
    /// One app in the Our Apps list (ported from NetSpeedMeterPlus): tinted
    /// icon tile, name with Premium / Popular badges, description,
    /// "Category · Free · 1M+ installs" line and a Get / View button. The
    /// whole row is clickable and highlights on hover — the button makes the
    /// affordance obvious, it isn't the only target. Paid rows sit on a faint
    /// amber tint so they read as featured.
    /// </summary>
    public class OurAppRow : DpiAwareUserControl
    {
        private const int BaseHeight = 78;
        private const int TileSize = 44;
        private const int TextLeft = 70;

        public static readonly Color PremiumColor = Color.FromArgb(245, 158, 11); // amber
        public static readonly Color PopularColor = Color.FromArgb(244, 63, 94);  // rose

        private readonly Color _baseBack;

        /// <summary>The app this row shows — used by the page's search filter.</summary>
        public OurApp App { get; }

        public OurAppRow(OurApp app)
        {
            // This page does its own DPI scaling through Scale()/ScaledPoint,
            // so WinForms must not scale the finished layout a second time.
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;

            App = app;
            Dock = DockStyle.Top;
            Height = Scale(BaseHeight);
            _baseBack = app.IsPaid ? Color.FromArgb(14, PremiumColor) : Color.Transparent;
            BackColor = _baseBack;
            Cursor = Cursors.Hand;

            Build();
        }

        private void Build()
        {
            // Glyph centred on the tile OnPaint draws behind it.
            int glyph = 22;
            int tileTop = (BaseHeight - TileSize) / 2;
            var icon = new IconPictureBox
            {
                Size = ScaledSize(glyph, glyph),
                IconChar = App.Icon,
                IconColor = App.Accent,
                IconSize = Scale(glyph),
                Location = ScaledPoint(14 + (TileSize - glyph) / 2, tileTop + (TileSize - glyph) / 2),
                BackColor = Color.Transparent,
                Enabled = false,
            };

            var lblName = MakeLabel(App.Name, new Font("Segoe UI", 10.5f, FontStyle.Bold),
                themeManager.CurrentTheme.TextColor, ScaledPoint(TextLeft, 12));

            var lblDescription = MakeLabel(App.Description, new Font("Segoe UI", 9f),
                themeManager.CurrentTheme.SecondaryTextColor, ScaledPoint(TextLeft, 35));
            lblDescription.AutoSize = false;
            lblDescription.AutoEllipsis = true;
            lblDescription.Height = Scale(18);

            var lblMeta = MakeLabel(BuildMeta(App), new Font("Segoe UI", 8f),
                themeManager.CurrentTheme.DisabledColor, ScaledPoint(TextLeft, 55));

            var btnOpen = CreateActionButton();
            btnOpen.Click += (s, e) => OpenStore(App);

            // Badges trail the name.
            var badges = new List<Control>();
            int badgeX = lblName.Left + lblName.PreferredWidth + Scale(6);
            foreach (var (text, color) in Badges(App))
            {
                var badge = MakeBadge(text, color, new Point(badgeX, Scale(15)));
                badgeX += badge.PreferredWidth + Scale(5);
                badges.Add(badge);
            }

            Controls.AddRange(new Control[] { icon, lblName, lblDescription, lblMeta, btnOpen });
            Controls.AddRange(badges.ToArray());

            var clickable = new List<Control> { this, icon, lblName, lblDescription, lblMeta };
            clickable.AddRange(badges);

            foreach (Control child in clickable)
            {
                child.Click += (s, e) => OpenStore(App);
                child.MouseEnter += (s, e) => SetHover(true);
                child.MouseLeave += (s, e) => SetHover(false);
            }
            btnOpen.MouseEnter += (s, e) => SetHover(true);
            btnOpen.MouseLeave += (s, e) => SetHover(false);

            void Reflow()
            {
                btnOpen.Location = new Point(
                    ClientSize.Width - btnOpen.Width - Scale(14),
                    (Height - btnOpen.Height) / 2);

                lblDescription.Width = Math.Max(Scale(80), btnOpen.Left - lblDescription.Left - Scale(12));
            }

            SizeChanged += (s, e) => Reflow();
            Reflow();
        }

        // ── Pieces ───────────────────────────────────────────────────────

        private static Label MakeLabel(string text, Font font, Color color, Point location) => new()
        {
            Text = text,
            UseMnemonic = false,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = location,
        };

        private Label MakeBadge(string text, Color color, Point location) => new()
        {
            Text = text,
            UseMnemonic = false,
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = color,
            BackColor = Color.FromArgb(40, color),
            AutoSize = true,
            Padding = ScaledPadding(5, 1, 5, 1),
            Location = location,
        };

        private static IEnumerable<(string Text, Color Color)> Badges(OurApp app)
        {
            if (app.IsPaid) yield return ("PREMIUM", PremiumColor);
            if (app.IsPopular) yield return ("POPULAR", PopularColor);
        }

        /// <summary>Filled "View" for paid apps, outlined "Get" for free ones.</summary>
        private Button CreateActionButton()
        {
            Color accent = themeManager.CurrentTheme.AccentColor;
            var btn = new Button
            {
                Text = App.IsPaid ? "View" : "Get",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Size = ScaledSize(84, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = App.IsPaid ? Color.White : accent,
                BackColor = App.IsPaid ? accent : Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false,
                UseMnemonic = false,
                UseVisualStyleBackColor = false,
            };
            btn.FlatAppearance.BorderColor = accent;
            btn.FlatAppearance.MouseOverBackColor = App.IsPaid ? themeManager.CurrentTheme.AccentHoverColor : Color.FromArgb(30, accent);
            btn.FlatAppearance.MouseDownBackColor = App.IsPaid ? themeManager.CurrentTheme.AccentHoverColor : Color.FromArgb(55, accent);
            return btn;
        }

        /// <summary>"Network  ·  Free  ·  1M+ installs" — the count only once it is worth showing.</summary>
        private static string BuildMeta(OurApp app)
        {
            var parts = new List<string> { app.Category, app.IsPaid ? "Paid" : "Free" };
            if (app.InstallsText.Length > 0) parts.Add(app.InstallsText);
            return string.Join("  ·  ", parts);
        }

        // ── Hover & paint ────────────────────────────────────────────────

        /// <summary>
        /// Moving between the row's own children fires Leave then Enter, so a
        /// Leave only clears the hover once the cursor has really left the row.
        /// </summary>
        private void SetHover(bool hover)
        {
            if (!hover && ClientRectangle.Contains(PointToClient(Cursor.Position)))
                return;

            BackColor = hover ? themeManager.CurrentTheme.HoverColor : _baseBack;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Tinted, rounded icon tile.
            var tile = new Rectangle(Scale(14), (Height - Scale(TileSize)) / 2, Scale(TileSize), Scale(TileSize));
            using (var path = RoundedRect(tile, Scale(10)))
            using (var fill = new SolidBrush(Color.FromArgb(38, App.Accent)))
                g.FillPath(fill, path);

            // Separator starts under the text, not under the tile.
            using var pen = new Pen(themeManager.CurrentTheme.BorderColor);
            g.SmoothingMode = SmoothingMode.None;
            g.DrawLine(pen, Scale(TextLeft), Height - 1, Width, Height - 1);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Store ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the Store page. The ms-windows-store: protocol needs
        /// ShellExecute and is missing on stripped or LTSC builds, so fall back
        /// to the app's web listing before giving up.
        /// </summary>
        private static void OpenStore(OurApp app)
        {
            if (TryOpen(app.StoreLink) || TryOpen(app.WebLink)) return;

            AlertService.ShowWarning(
                $"Windows couldn't open the Store page for {app.Name}. " +
                "The Microsoft Store may not be available on this PC.",
                "Couldn't open the Store");
        }

        private static bool TryOpen(string link)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = link, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"OurAppRow: link failed ({link}): {ex.Message}");
                return false;
            }
        }
    }
}
