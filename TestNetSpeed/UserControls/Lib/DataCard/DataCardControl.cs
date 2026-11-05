// UserControls/Lib/DataCard/DataCardControl.cs
using FontAwesome.Sharp;
using System.ComponentModel;

namespace TestNetSpeed.UserControls.Lib.DataCard
{
    /// <summary>
    /// Reusable dashboard data card: accent strip, gradient icon badge,
    /// caption / value / sub-text, optional status pill, hover lift and a
    /// loading skeleton. Fully owner-drawn (no child controls), so it is
    /// flicker-free, DPI-aware and safe to update before it is laid out.
    ///
    /// Dependencies: WinForms + FontAwesome.Sharp.
    /// </summary>
    [DesignerCategory("Code")]
    public partial class DataCardControl : Control
    {
        // ── Content ───────────────────────────────────────────────────────────
        private readonly IconBadgeRenderer _iconRenderer = new();
        private string _caption = "";
        private string _value = "";
        private string _subText = "";
        private string _badge = "";
        private Color _accent = DataCardAccents.Blue;
        private Color? _valueColor;
        private Color? _badgeColor;
        private bool _isLoading;
        private DataCardPalette _palette = DataCardPalette.Dark;

        // ── Hover / loading animation ─────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };
        private float _hover;          // 0 → 1
        private bool _mouseInside;
        private float _pulse;          // 0 → 2π, drives the skeleton shimmer

        public DataCardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8f);
            Size = new Size(240, 90);
            _animTimer.Tick += OnAnimTick;
        }

        public DataCardControl(IconChar icon, string caption, Color accent) : this()
        {
            _iconRenderer.Icon = icon;
            _iconRenderer.Accent = accent;
            _caption = caption;
            _accent = accent;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Raised when the card is clicked and <see cref="Clickable"/> is true.</summary>
        public event EventHandler? CardClick;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IconChar Icon
        {
            get => _iconRenderer.Icon;
            set { _iconRenderer.Icon = value; SafeInvalidate(); }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption { get => _caption; set => Set(ref _caption, value ?? ""); }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Value { get => _value; set => Set(ref _value, value ?? ""); }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SubText { get => _subText; set => Set(ref _subText, value ?? ""); }

        /// <summary>Short pill text in the top-right corner (e.g. "LIVE"). Empty hides it.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Badge { get => _badge; set => Set(ref _badge, value ?? ""); }

        /// <summary>Main color: strip, icon gradient, value text and hover border.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Accent
        {
            get => _accent;
            set { _iconRenderer.Accent = value; Set(ref _accent, value); }
        }

        /// <summary>Icon badge look: Circle, Squircle, Soft or Outline.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IconBadgeStyle IconStyle
        {
            get => _iconRenderer.Style;
            set { _iconRenderer.Style = value; SafeInvalidate(); }
        }

        /// <summary>Icon badge diameter at 96 DPI (shrinks to fit short cards).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int IconSize { get; set; } = 50;

        /// <summary>Glyph size relative to the badge (0.3 – 0.8).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float IconGlyphScale
        {
            get => _iconRenderer.GlyphScale;
            set { _iconRenderer.GlyphScale = value; SafeInvalidate(); }
        }

        /// <summary>Overrides the value text color (defaults to <see cref="Accent"/>).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color? ValueColor { get => _valueColor; set => Set(ref _valueColor, value); }

        /// <summary>Overrides the badge color (defaults to <see cref="Accent"/>).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color? BadgeColor { get => _badgeColor; set => Set(ref _badgeColor, value); }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataCardPalette Palette
        {
            get => _palette;
            set => Set(ref _palette, value ?? DataCardPalette.Dark);
        }

        /// <summary>Shows a shimmering placeholder instead of value / sub-text.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                UpdateTimer();
                Invalidate();
            }
        }

        /// <summary>Hand cursor + <see cref="CardClick"/> when true.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Clickable { get; set; }

        /// <summary>Corner radius at 96 DPI.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 8;

        /// <summary>Updates value and sub-text in one repaint; clears loading.</summary>
        public void SetData(string value, string? subText = null, Color? valueColor = null)
        {
            _value = value ?? "";
            if (subText != null) _subText = subText;
            if (valueColor.HasValue) _valueColor = valueColor;
            _isLoading = false;
            UpdateTimer();
            SafeInvalidate();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  INTERACTION
        // ══════════════════════════════════════════════════════════════════════

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _mouseInside = true;
            Cursor = Clickable ? Cursors.Hand : Cursors.Default;
            UpdateTimer();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseInside = false;
            UpdateTimer();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Clickable) CardClick?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            Invalidate();
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            float target = _mouseInside ? 1f : 0f;
            _hover += (target - _hover) * 0.25f;
            if (Math.Abs(target - _hover) < 0.01f) _hover = target;

            if (_isLoading)
                _pulse = (_pulse + 0.08f) % (MathF.PI * 2);

            UpdateTimer();
            Invalidate();
        }

        private void UpdateTimer()
        {
            bool hoverSettled = _hover == (_mouseInside ? 1f : 0f);
            bool needed = _isLoading || !hoverSettled;

            if (needed && !_animTimer.Enabled) _animTimer.Start();
            else if (!needed && _animTimer.Enabled) _animTimer.Stop();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            SafeInvalidate();
        }

        /// <summary>Invalidate that tolerates calls from worker threads.</summary>
        private void SafeInvalidate()
        {
            if (IsDisposed) return;
            if (InvokeRequired && IsHandleCreated) BeginInvoke(Invalidate);
            else Invalidate();
        }

        private float DpiScale => DeviceDpi / 96f;

        private int S(int value) => (int)Math.Round(value * DpiScale);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer.Dispose();
                _iconRenderer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
