using DpiCommon.Helper;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.UserControls.About;

namespace SpeedTestZx.Forms
{
    /// <summary>
    /// The About dialog, for both tiers — layout ported from CPU ZX Pro's About
    /// screen: a scrolling stack of header (icon, name, version + edition
    /// badges, subtitle, description), tier cards and footer (website, support
    /// email, Close, developer/copyright).
    ///
    /// What the cards show comes from AboutTierPlan.ForCurrentTier(): Free sees
    /// the key features and the Go Pro offer; Pro sees the licence (with its
    /// last Store verification) and everything included. Content lives in
    /// AppConfig's "About dialog content" fields — nothing here should need
    /// editing when bootstrapping a new app.
    ///
    /// The Go Pro CTA closes this dialog and opens the shared upgrade prompt
    /// (PromptManager), so the purchase stays in one place.
    /// </summary>
    public class AboutForm : Form
    {
        private readonly ThemeManager _theme;
        private readonly AboutTierPlan _plan;

        private FlowLayoutPanel? _stack;
        private AboutHeaderControl? _header;
        private AboutFooterControl? _footer;
        private readonly List<AboutFeatureCard> _cards = new();

        private bool _goProRequested;

        public AboutForm()
        {
            _theme = ThemeManager.Instance;
            _plan = AboutTierPlan.ForCurrentTier();

            InitializeComponent();
            BuildLayout();
            ApplyTheme();

            _theme.ThemeChanged += OnThemeChanged;
        }

        private static int S(int value) => DpiAwareService.Scale(value);

        // ─────────────────────────────────────────────────────────────────────
        // FORM
        // ─────────────────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            SuspendLayout();

            // Capped to the screen so a high-DPI laptop never pushes the
            // bottom of the dialog off-screen; the stack scrolls instead.
            int maxHeight = (int)(Screen.FromPoint(Cursor.Position).WorkingArea.Height * 0.9);
            ClientSize = new Size(S(520), Math.Min(S(720), maxHeight));

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = File.Exists(AppConfig.FaviconPath) ? new Icon(AppConfig.FaviconPath) : null;
            Name = "AboutForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = $"About {AppConfig.AppName}";
            BackColor = _theme.CurrentTheme.ContentBackground;
            AutoScaleMode = AutoScaleMode.Dpi;

            ResumeLayout(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // LAYOUT
        // ─────────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            _stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = _theme.CurrentTheme.ContentBackground,
                Padding = new Padding(S(24), S(16), S(24), S(16))
            };
            Controls.Add(_stack);

            // Content width leaves room for the scrollbar so nothing reflows
            // when the stack overflows.
            int width = ClientSize.Width - S(48) - SystemInformation.VerticalScrollBarWidth;

            _header = new AboutHeaderControl(_theme, width, _plan.EditionLabel, _plan.EditionColor);
            _stack.Controls.Add(_header);

            foreach (var spec in _plan.Cards)
                AddCard(spec, width);

            _footer = new AboutFooterControl(_theme, width, (s, e) => Close());
            _stack.Controls.Add(_footer);

            CancelButton = _footer.CloseButton;
        }

        private void AddCard(AboutCardSpec spec, int width)
        {
            var card = new AboutFeatureCard(_theme, width, spec);

            if (!string.IsNullOrWhiteSpace(spec.CtaText))
            {
                card.CtaClicked += (s, e) =>
                {
                    _goProRequested = true;
                    Close();
                };
            }

            _cards.Add(card);
            _stack!.Controls.Add(card);
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            var background = _theme.CurrentTheme.ContentBackground;

            BackColor = background;
            if (_stack != null) _stack.BackColor = background;

            _header?.ApplyTheme();
            _footer?.ApplyTheme();

            foreach (var card in _cards)
                card.ApplyTheme();
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            ApplyTheme();
            Invalidate(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // LIFETIME
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens at the top. WinForms scrolls an AutoScroll container to
        /// whichever child holds focus, and the first selectable control is the
        /// Go Pro CTA (or Close) further down — which landed the dialog mid-page.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            ActiveControl = null;

            if (_stack != null)
            {
                _stack.AutoScrollPosition = Point.Empty;
                _stack.PerformLayout();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _theme.ThemeChanged -= OnThemeChanged;
            base.OnFormClosed(e);

            // After the modal is gone, so the prompt overlays the main window.
            if (_goProRequested)
                PromptManager.Current?.ShowPro();
        }
    }
}
