using DeviceDataModule;
using SpeedTestZx.Managers;
using SpeedTestZx.UserControls.Lib;

namespace SpeedTestZx.UserControls.Pages.Settings
{
    /// <summary>
    /// Base for one Settings category (Appearance, Behavior, ...): title,
    /// subtitle, then section labels and cards of rows, stacked top to bottom.
    /// Ported from Net Speed Meter Plus, without the floating-widget plumbing.
    /// </summary>
    /// <remarks>
    /// Colors are read once at construction — a theme switch rebuilds the
    /// whole Settings page (NavigationManager.RefreshActivePage). Every row
    /// added through <see cref="AddRow"/> is also registered for the page's
    /// search box, so a new setting is searchable without extra work.
    /// </remarks>
    internal abstract class SettingsCategory : FlowLayoutPanel
    {
        protected static AppTheme Theme => ThemeManager.Instance.CurrentTheme;
        protected static AppSettingsVm Current => SettingsService.Current;
        protected static int S(int value) => SettingsStyle.Scale(value);

        /// <summary>Set while controls are filled from code, so their change events don't write back.</summary>
        protected bool IsLoading;

        private readonly List<SettingsCard> _cards = new();

        /// <summary>One searchable row: what it's called, plus extra words people might type.</summary>
        public sealed record SearchEntry(SettingsCategory Category, Control Target, string Title, string Description, string[] Keywords);

        public List<SearchEntry> SearchEntries { get; } = new();

        public abstract string Title { get; }
        public abstract string Subtitle { get; }

        /// <summary>False hides the footer's "Reset page".</summary>
        public virtual bool CanReset => true;

        /// <summary>Restores this category's settings to their defaults. The page rebuilds the category afterwards.</summary>
        public virtual void ResetToDefaults() { }

        protected SettingsCategory()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.ContentBackground;
            Padding = new Padding(S(24), S(4), S(24), S(12));
        }

        /// <summary>Called once by the page after construction; subclasses add their sections here.</summary>
        public void Build()
        {
            Controls.Add(MakeLabel(Title, 16f, FontStyle.Bold, Theme.HeadingColor, bottom: 2));
            Controls.Add(MakeLabel(Subtitle, 9.5f, FontStyle.Regular, Theme.SecondaryTextColor, bottom: 18));
            BuildContent();
        }

        protected abstract void BuildContent();

        // ── Building blocks ───────────────────────────────────────────────────

        protected void AddSection(string text) =>
            Controls.Add(MakeLabel(text.ToUpperInvariant(), 7.5f, FontStyle.Bold, Theme.SecondaryTextColor, bottom: 6));

        protected SettingsCard AddCard()
        {
            var card = new SettingsCard();
            _cards.Add(card);
            Controls.Add(card);
            return card;
        }

        /// <summary>Adds a row to <paramref name="card"/> and registers it for search.</summary>
        protected SettingsRowControl AddRow(SettingsCard card, string title, Control input,
            string description = null, params string[] keywords)
        {
            var row = card.AddRow(new SettingsRowControl(title, input, description));
            input.AccessibleName = title;
            SearchEntries.Add(new SearchEntry(this, row, title, description ?? "", keywords));
            return row;
        }

        /// <summary>Registers a non-row control (theme cards) for search.</summary>
        protected void Register(Control target, string title, string description, params string[] keywords) =>
            SearchEntries.Add(new SearchEntry(this, target, title, description, keywords));

        protected static Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                UseMnemonic = false, // "&" is literal text, not an access key
                Font = new Font("Segoe UI", 9f),
                Height = S(30),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.Width = TextRenderer.MeasureText(text, button.Font).Width + S(28);
            SettingsStyle.ThemeGhostButton(button);
            button.Click += onClick;
            return button;
        }

        protected static Label MakeNote(string text) => new()
        {
            Text = text,
            UseMnemonic = false, // "&" is literal text, not an access key
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Theme.SecondaryTextColor,
            BackColor = Theme.CardBackground,
            AutoSize = true
        };

        // ── Persistence ───────────────────────────────────────────────────────

        /// <summary>Saves, and tells the user (without crashing) when the file couldn't be written.</summary>
        protected static void Save()
        {
            try
            {
                SettingsService.Save();
            }
            catch
            {
                NotificationManager.Instance.ShowError(
                    "Couldn't save settings — the settings file is in use. The change applies until you close the app.");
            }
        }

        /// <summary>Writes one field and saves.</summary>
        protected void Commit(Action<AppSettingsVm> write)
        {
            if (IsLoading) return;
            write(Current);
            Save();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        /// <summary>Called by the page on resize with the width available to this category.</summary>
        public void SetContentWidth(int width)
        {
            int inner = Math.Max(S(320), width - Padding.Horizontal);
            foreach (var card in _cards) card.SetWidth(inner);
            MinimumSize = new Size(width, 0);
            MaximumSize = new Size(width, 0);
        }

        private static Label MakeLabel(string text, float size, FontStyle style, Color color, int bottom) => new()
        {
            Text = text,
            UseMnemonic = false, // "&" is literal text, not an access key
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, S(bottom))
        };
    }
}
