using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace TestNetSpeed.Helper.Report
{
    /// <summary>
    /// Top-to-bottom flow layout over PDFsharp (T34): text with word wrap,
    /// images, and tables that break cleanly across pages and repeat their
    /// header. A4, 40 pt margins, Segoe UI from the Windows fonts folder
    /// (same resolver approach as Billora.Desktop).
    /// </summary>
    internal sealed class PdfLayout : IDisposable
    {
        private const double Margin = 40;
        private const double FooterHeight = 20;

        public readonly XFont Title = new("Segoe UI", 20, XFontStyleEx.Bold);
        public readonly XFont Heading = new("Segoe UI", 13, XFontStyleEx.Bold);
        public readonly XFont Body = new("Segoe UI", 10, XFontStyleEx.Regular);
        public readonly XFont Bold = new("Segoe UI", 10, XFontStyleEx.Bold);
        public readonly XFont Small = new("Segoe UI", 8, XFontStyleEx.Regular);
        public readonly XFont SmallBold = new("Segoe UI", 8, XFontStyleEx.Bold);

        public static readonly XBrush Ink = new XSolidBrush(XColor.FromArgb(30, 35, 45));
        public static readonly XBrush Muted = new XSolidBrush(XColor.FromArgb(100, 108, 120));
        private static readonly XBrush HeaderFill = new XSolidBrush(XColor.FromArgb(236, 239, 244));
        private static readonly XPen Rule = new(XColor.FromArgb(215, 220, 228), 0.6);

        public PdfDocument Document { get; } = new();
        private PdfPage _page = null!;
        private XGraphics _g = null!;
        private double _y;

        static PdfLayout() => EnsureFontResolver();

        public PdfLayout(string title)
        {
            Document.Info.Title = title;
            NewPage();
        }

        public double Width => _page.Width.Point - Margin * 2;
        private double Bottom => _page.Height.Point - Margin - FooterHeight;

        public void NewPage()
        {
            _g?.Dispose();
            _page = Document.AddPage();
            _page.Size = PageSize.A4;
            _g = XGraphics.FromPdfPage(_page);
            _y = Margin;
        }

        public void Space(double points) => _y += points;

        public void EnsureSpace(double height)
        {
            if (_y + height > Bottom) NewPage();
        }

        /// <summary>Word-wrapped paragraph.</summary>
        public void Text(string text, XFont font, XBrush? brush = null, double after = 4)
        {
            foreach (var line in Wrap(text, font, Width))
            {
                double h = font.GetHeight();
                EnsureSpace(h);
                _g.DrawString(line, font, brush ?? Ink, new XRect(Margin, _y, Width, h), XStringFormats.TopLeft);
                _y += h;
            }
            _y += after;
        }

        /// <param name="keepWith">
        /// Height of what follows that must start on the same page — a chart,
        /// or a few table rows — so a heading is never stranded at the bottom.
        /// </param>
        public void SectionHeading(string text, double keepWith = 70)
        {
            EnsureSpace(Heading.GetHeight() + 18 + keepWith);
            _y += 8;
            Text(text, Heading, Ink, 2);
            _g.DrawLine(Rule, Margin, _y, Margin + Width, _y);
            _y += 8;
        }

        /// <summary>Label / value rows, two columns.</summary>
        public void KeyValues(IEnumerable<(string Key, string Value)> rows)
        {
            foreach (var (key, value) in rows)
            {
                double h = Body.GetHeight() + 3;
                EnsureSpace(h);
                _g.DrawString(key, Body, Muted, new XRect(Margin, _y, Width * 0.45, h), XStringFormats.TopLeft);
                _g.DrawString(value, Bold, Ink, new XRect(Margin + Width * 0.45, _y, Width * 0.55, h), XStringFormats.TopLeft);
                _y += h;
            }
            _y += 4;
        }

        /// <summary>A bitmap scaled to the full content width.</summary>
        public void Image(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            using var image = XImage.FromStream(stream);

            double height = Width * bitmap.Height / bitmap.Width;
            EnsureSpace(height);
            _g.DrawImage(image, Margin, _y, Width, height);
            _y += height + 6;
        }

        /// <summary>Table with a shaded header that repeats on every page it spans.</summary>
        /// <param name="widths">Column widths as fractions of the content width.</param>
        public void Table(string[] headers, double[] widths, IEnumerable<string[]> rows)
        {
            double rowH = Small.GetHeight() + 5;
            EnsureSpace(rowH * 3);
            DrawRow(headers, widths, rowH, header: true);

            foreach (var row in rows)
            {
                if (_y + rowH > Bottom)
                {
                    NewPage();
                    DrawRow(headers, widths, rowH, header: true);
                }
                DrawRow(row, widths, rowH, header: false);
            }
            _y += 6;
        }

        private void DrawRow(string[] cells, double[] widths, double rowH, bool header)
        {
            if (header)
                _g.DrawRectangle(HeaderFill, Margin, _y, Width, rowH);

            double x = Margin;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                double w = Width * widths[i];
                var rect = new XRect(x + 3, _y + 2.5, w - 6, rowH - 2.5);
                var font = header ? SmallBold : Small;
                _g.DrawString(Fit(cells[i], font, w - 6), font, Ink, rect, XStringFormats.TopLeft);
                x += w;
            }

            _y += rowH;
            _g.DrawLine(Rule, Margin, _y, Margin + Width, _y);
        }

        /// <summary>Page numbers, drawn once the page count is known.</summary>
        public void AddFooters(string text)
        {
            _g.Dispose();
            int count = Document.PageCount;
            for (int i = 0; i < count; i++)
            {
                var page = Document.Pages[i];
                using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var rect = new XRect(Margin, page.Height.Point - Margin - 10, page.Width.Point - Margin * 2, 12);
                g.DrawString(text, Small, Muted, rect, XStringFormats.TopLeft);
                g.DrawString($"Page {i + 1} of {count}", Small, Muted, rect, XStringFormats.TopRight);
            }
        }

        public void Save(string path) => Document.Save(path);

        public void Dispose()
        {
            Document.Dispose();
        }

        // ── Text helpers ─────────────────────────────────────────────────────

        private IEnumerable<string> Wrap(string text, XFont font, double width)
        {
            foreach (var paragraph in text.Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && _g.MeasureString(candidate, font).Width > width)
                    {
                        yield return line;
                        line = word;
                    }
                    else line = candidate;
                }
                yield return line;
            }
        }

        private string Fit(string text, XFont font, double width)
        {
            if (_g.MeasureString(text, font).Width <= width) return text;
            while (text.Length > 1 && _g.MeasureString(text + "…", font).Width > width)
                text = text[..^1];
            return text + "…";
        }

        // ── Fonts ────────────────────────────────────────────────────────────

        private static void EnsureFontResolver()
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new SegoeFontResolver();
        }

        private sealed class SegoeFontResolver : IFontResolver
        {
            public byte[]? GetFont(string faceName)
            {
                string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                string file = faceName == "Segoe UI#Bold" ? "segoeuib.ttf" : "segoeui.ttf";
                return File.ReadAllBytes(Path.Combine(fonts, file));
            }

            public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
                new(isBold ? "Segoe UI#Bold" : "Segoe UI#Regular");
        }
    }
}
