using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using TestNetSpeed.Model;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Draws the shareable result card (T16): a fixed 1200 × 630 PNG (the
    /// common social-preview size) with the app's brand colour, the three
    /// headline numbers, the Quality Score verdicts, bufferbloat grade, ISP
    /// and date. Always rendered on a dark background so it looks the same
    /// wherever it's pasted, regardless of the app theme or screen DPI.
    /// </summary>
    public static class ResultCardRenderer
    {
        public const int CardWidth = 1200;
        public const int CardHeight = 630;

        private static readonly Color Bg1 = Color.FromArgb(24, 24, 32);
        private static readonly Color Bg2 = Color.FromArgb(36, 30, 30);
        private static readonly Color PanelColor = Color.FromArgb(40, 40, 52);
        private static readonly Color TextMain = Color.FromArgb(240, 240, 245);
        private static readonly Color TextDim = Color.FromArgb(160, 160, 176);

        private static readonly Color DownColor = Color.FromArgb(59, 130, 246);
        private static readonly Color UpColor = Color.FromArgb(16, 185, 129);
        private static readonly Color PingColor = Color.FromArgb(245, 158, 11);

        public static Bitmap Render(SpeedTestResult r, QualityReport q)
        {
            var bmp = new Bitmap(CardWidth, CardHeight, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // ── Background + brand bar ───────────────────────────────────────
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, CardWidth, CardHeight), Bg1, Bg2, 35f))
                g.FillRectangle(bg, 0, 0, CardWidth, CardHeight);

            using (var brand = new SolidBrush(AppConfig.PrimaryColor))
                g.FillRectangle(brand, 0, 0, CardWidth, 10);

            // ── Header: logo, app name, date ─────────────────────────────────
            int x = 60, y = 50;
            DrawLogo(g, new Rectangle(x, y, 64, 64));

            using (var titleFont = new Font("Segoe UI", 26f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var subFont = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var main = new SolidBrush(TextMain))
            using (var dim = new SolidBrush(TextDim))
            {
                g.DrawString(AppConfig.AppName, titleFont, main, x + 80, y + 4);
                g.DrawString(r.TestedAt.ToString("dd MMM yyyy  ·  HH:mm"), subFont, dim, x + 82, y + 38);

                string isp = BuildIspLine(r);
                if (isp.Length > 0)
                {
                    var sz = g.MeasureString(isp, subFont);
                    g.DrawString(isp, subFont, dim, CardWidth - 60 - sz.Width, y + 22);
                }
            }

            // ── Headline numbers ─────────────────────────────────────────────
            int boxY = 150, boxH = 190, gap = 24;
            int boxW = (CardWidth - 120 - gap * 2) / 3;

            DrawMetric(g, new Rectangle(60, boxY, boxW, boxH), "DOWNLOAD", $"{r.DownloadMbps:0.#}", "Mbps", DownColor);
            DrawMetric(g, new Rectangle(60 + boxW + gap, boxY, boxW, boxH), "UPLOAD", $"{r.UploadMbps:0.#}", "Mbps", UpColor);

            string pingSub = r.BufferbloatMs != null
                ? $"ms  ·  Bufferbloat {q.BufferbloatGrade}"
                : $"ms  ·  Jitter {r.JitterMs:0} ms";
            DrawMetric(g, new Rectangle(60 + (boxW + gap) * 2, boxY, boxW, boxH), "PING", $"{r.PingMs:0}", pingSub, PingColor);

            // ── Quality verdicts ─────────────────────────────────────────────
            int vy = 370, vh = 150;
            int vw = (CardWidth - 120 - gap * 2) / 3;
            int i = 0;
            foreach (var v in q.Verdicts)
            {
                string extra = ReferenceEquals(v, q.Streaming) ? q.StreamingEstimate.Resolution : v.Reason;
                DrawVerdict(g, new Rectangle(60 + (vw + gap) * i, vy, vw, vh), v, extra);
                i++;
            }

            // ── Footer ───────────────────────────────────────────────────────
            using (var footFont = new Font("Segoe UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var dim = new SolidBrush(TextDim))
            {
                string foot = $"Tested with {AppConfig.AppName}  ·  Free on the Microsoft Store";
                g.DrawString(foot, footFont, dim, 60, CardHeight - 62);
            }

            return bmp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PIECES
        // ══════════════════════════════════════════════════════════════════════

        private static string BuildIspLine(SpeedTestResult r)
        {
            var parts = new List<string>();
            if (r.Isp.Length > 0) parts.Add(r.Isp);
            if (r.ServerColo.Length > 0) parts.Add($"Server {r.ServerColo}");
            return string.Join("  ·  ", parts);
        }

        private static void DrawLogo(Graphics g, Rectangle rect)
        {
            try
            {
                if (File.Exists(AppConfig.AppIconPath))
                {
                    using var logo = Image.FromFile(AppConfig.AppIconPath);
                    g.DrawImage(logo, rect);
                    return;
                }
            }
            catch
            {
                // Fall through to the plain brand tile.
            }

            using var path = Rounded(rect, 14);
            using var b = new SolidBrush(AppConfig.PrimaryColor);
            g.FillPath(b, path);
        }

        private static void DrawMetric(Graphics g, Rectangle rect, string caption, string value, string unit, Color accent)
        {
            using (var path = Rounded(rect, 18))
            using (var fill = new SolidBrush(PanelColor))
                g.FillPath(fill, path);

            using (var strip = new SolidBrush(accent))
                g.FillRectangle(strip, rect.X + 18, rect.Y, rect.Width - 36, 5);

            using var capFont = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var valFont = new Font("Segoe UI", 72f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var unitFont = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var dim = new SolidBrush(TextDim);
            using var acc = new SolidBrush(accent);

            g.DrawString(caption, capFont, dim, rect.X + 26, rect.Y + 22);
            g.DrawString(value, valFont, acc, rect.X + 18, rect.Y + 50);
            g.DrawString(unit, unitFont, dim, rect.X + 28, rect.Y + rect.Height - 42);
        }

        private static void DrawVerdict(Graphics g, Rectangle rect, QualityVerdict v, string detail)
        {
            Color c = QualityScorer.LevelColor(v.Level);

            using (var path = Rounded(rect, 18))
            {
                using var fill = new SolidBrush(PanelColor);
                g.FillPath(fill, path);
                using var edge = new Pen(Color.FromArgb(120, c), 2f);
                g.DrawPath(edge, path);
            }

            using var nameFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var levelFont = new Font("Segoe UI", 36f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var detailFont = new Font("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var main = new SolidBrush(TextMain);
            using var dim = new SolidBrush(TextDim);
            using var lvl = new SolidBrush(c);

            g.DrawString(v.UseCase, nameFont, main, rect.X + 24, rect.Y + 18);
            g.DrawString(v.Level.ToString(), levelFont, lvl, rect.X + 20, rect.Y + 48);

            var detailRect = new RectangleF(rect.X + 24, rect.Y + 102, rect.Width - 48, 40);
            using var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(detail, detailFont, dim, detailRect, fmt);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
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
    }
}
