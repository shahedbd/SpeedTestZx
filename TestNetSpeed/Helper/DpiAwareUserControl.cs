using SpeedTestZx.Managers;
using DpiCommon.Helper;

namespace SpeedTestZx.UserControls
{
    /// <summary>
    /// Base class for DPI-aware user controls with proper scaling
    /// KEY INSIGHT: Font sizes should NOT be scaled - Windows handles font rendering at high DPI
    /// Only positions and dimensions need to be scaled
    /// </summary>
    public class DpiAwareUserControl : UserControl
    {
        protected ThemeManager themeManager;
        protected float _dpiScale = 1.0f;

        // Base dimensions at 96 DPI
        protected const int BASE_CONTENT_WIDTH = 900;
        protected const int BASE_CARD_SPACING = 15;
        protected const int BASE_BOTTOM_PADDING = 40;

        // Scaled dimensions
        protected virtual int ContentWidth => Scale(BASE_CONTENT_WIDTH);
        protected int CardSpacing => Scale(BASE_CARD_SPACING);
        protected int BottomPadding => Scale(BASE_BOTTOM_PADDING);

        public DpiAwareUserControl()
        {
            themeManager = ThemeManager.Instance;
            _dpiScale = DpiAwareService.ScaleFactor;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            this.ParentChanged += OnParentChanged;
        }

        private void OnParentChanged(object sender, EventArgs e)
        {
            if (this.Parent != null)
            {
                var form = this.FindForm();
                if (form != null)
                {
                    float newScale = form.DeviceDpi / 96f;
                    if (Math.Abs(newScale - _dpiScale) > 0.01f)
                    {
                        _dpiScale = newScale;
                    }
                }
            }
        }

        #region Scaling Helpers

        protected int Scale(int value) => (int)Math.Round(value * _dpiScale);
        protected float Scale(float value) => value * _dpiScale;
        protected Point ScaledPoint(int x, int y) => new Point(Scale(x), Scale(y));
        protected Size ScaledSize(int width, int height) => new Size(Scale(width), Scale(height));


        //protected Padding ScaledPadding(int all) => new Padding(Scale(all));
        //protected Padding ScaledPadding(int h, int v) => new Padding(Scale(h), Scale(v), Scale(h), Scale(v));

        /// <summary>
        /// Creates padding with the same value for all sides
        /// </summary>
        protected Padding ScaledPadding(int all) => new Padding(Scale(all));

        /// <summary>
        /// Creates padding with horizontal and vertical values
        /// </summary>
        protected Padding ScaledPadding(int horizontal, int vertical)
            => new Padding(Scale(horizontal), Scale(vertical), Scale(horizontal), Scale(vertical));

        /// <summary>
        /// Creates padding with individual values for left, top, right, bottom
        /// </summary>
        protected Padding ScaledPadding(int left, int top, int right, int bottom)
            => new Padding(Scale(left), Scale(top), Scale(right), Scale(bottom));

        /// <summary>
        /// ✅ FIXED: Returns a font with FIXED size (not scaled)
        /// Windows automatically renders fonts correctly at high DPI
        /// Scaling fonts causes text to overflow containers
        /// </summary>
        protected Font ScaledFont(float size, FontStyle style = FontStyle.Regular)
        {
            // DO NOT scale font size - Windows handles high DPI font rendering
            return new Font("Segoe UI", size, style);
        }

        #endregion

        #region Common UI Components

        protected Label CreateTitleLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),  // Fixed size
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(x), Scale(y)),
                AutoSize = true
            };
        }

        protected Label CreateSectionTitleLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),  // Fixed size
                ForeColor = themeManager.CurrentTheme.HeadingColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(x), Scale(y)),
                AutoSize = true
            };
        }

        protected Panel CreateCardPanel(int x, int y, int width, int height)
        {
            return new Panel
            {
                Location = new Point(Scale(x), Scale(y)),
                Size = new Size(Scale(width), Scale(height)),
                BackColor = themeManager.CurrentTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        /// <summary>
        /// Creates a stat box with icon, label and value - FIXED font sizes
        /// </summary>
        protected void CreateStatBox(Panel parent, string icon, string label, string value,
            Color accentColor, int x, int y, int width)
        {
            int boxHeight = Scale(70);

            Panel statBox = new Panel
            {
                Location = new Point(Scale(x), Scale(y)),
                Size = new Size(Scale(width), boxHeight),
                BackColor = Color.FromArgb(30, accentColor)
            };

            // Icon - fixed font
            Label lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 16f),  // Fixed size
                Location = new Point(Scale(10), Scale(10)),
                Size = new Size(Scale(35), Scale(35)),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            statBox.Controls.Add(lblIcon);

            // Label - fixed font
            Label lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 8f),  // Fixed size
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(50), Scale(8)),
                AutoSize = true
            };
            statBox.Controls.Add(lblLabel);

            // Value - fixed font, AutoSize to prevent cutoff
            Label lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),  // Fixed size
                ForeColor = accentColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(50), Scale(28)),
                AutoSize = true  // Prevents cutoff
            };
            statBox.Controls.Add(lblValue);

            parent.Controls.Add(statBox);
        }

        /// <summary>
        /// Creates a detail row with label and value - FIXED font sizes
        /// </summary>
        protected void CreateDetailRow(Panel parent, string label, string value, int x, int y)
        {
            Label lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 9f),  // Fixed size
                ForeColor = themeManager.CurrentTheme.SecondaryTextColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(x), Scale(y)),
                AutoSize = true
            };
            parent.Controls.Add(lblLabel);

            Label lblValue = new Label
            {
                Text = value ?? "N/A",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),  // Fixed size
                ForeColor = themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                Location = new Point(Scale(x + 120), Scale(y)),
                AutoSize = true
            };
            parent.Controls.Add(lblValue);
        }

        /// <summary>
        /// Creates a badge label - FIXED font size
        /// </summary>
        protected Label CreateBadge(string text, Color backgroundColor, int x, int y)
        {
            // Calculate width based on text length (approximate)
            int textWidth = TextRenderer.MeasureText(text, new Font("Segoe UI", 8f, FontStyle.Bold)).Width;
            int badgeWidth = textWidth + Scale(16);

            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),  // Fixed size
                ForeColor = Color.White,
                BackColor = backgroundColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(badgeWidth, Scale(20)),
                Location = new Point(Scale(x), Scale(y))
            };
        }

        /// <summary>
        /// Creates a progress bar with background and fill
        /// </summary>
        protected (Panel background, Panel fill) CreateProgressBar(int x, int y, int width, int height,
            double percentage, Color fillColor)
        {
            Panel background = new Panel
            {
                Location = new Point(Scale(x), Scale(y)),
                Size = new Size(Scale(width), Scale(height)),
                BackColor = themeManager.IsDarkTheme
                    ? Color.FromArgb(60, 60, 60)
                    : Color.FromArgb(229, 231, 235)
            };

            int fillWidth = (int)(Scale(width) * Math.Min(percentage, 100) / 100);
            Panel fill = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(fillWidth, Scale(height)),
                BackColor = fillColor
            };

            background.Controls.Add(fill);
            return (background, fill);
        }

        protected Panel CreateErrorPanel(string message, int yPosition, int height = 100)
        {
            Panel panel = new Panel
            {
                Location = new Point(0, Scale(yPosition)),
                Size = new Size(ContentWidth, Scale(height)),
                BackColor = Color.FromArgb(40, themeManager.CurrentTheme.ErrorColor),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblError = new Label
            {
                Text = $"⚠️ {message}",
                Font = new Font("Segoe UI", 10f),  // Fixed size
                ForeColor = themeManager.CurrentTheme.ErrorColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(lblError);

            return panel;
        }

        protected Color GetUsageColor(double percentage)
        {
            if (percentage >= 90) return Color.FromArgb(239, 68, 68);   // Red
            if (percentage >= 70) return Color.FromArgb(245, 158, 11);  // Orange
            return Color.FromArgb(34, 197, 94);  // Green
        }

        protected string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        protected string FormatBytes(ulong bytes)
        {
            return FormatBytes((long)bytes);
        }

        #endregion
    }
}
