using TestNetSpeed.UserControls.Pages.Settings;

namespace TestNetSpeed.UserControls.Pages
{
    /// <summary>
    /// The "Settings" sidebar page — its own category column (with APP / DATA
    /// group headings), a search box and a scrolling content pane, laid out
    /// like Net Speed Meter Plus's Settings page. Every change applies and
    /// saves at once, so there is no Apply button.
    /// </summary>
    /// <remarks>
    /// This file owns state and navigation between categories and search;
    /// <c>Settings/SettingsPageControl.Layout.cs</c> builds the chrome.
    /// Categories are built the first time they're opened; typing in search
    /// builds the rest, since search spans every category. Requires a public
    /// parameterless constructor (NavigationManager creates pages via
    /// Activator.CreateInstance).
    /// </remarks>
    public partial class SettingsPageControl : DpiAwareUserControl
    {
        /// <summary>
        /// Survives the page being rebuilt — a theme switch rebuilds the page
        /// (NavigationManager.RefreshActivePage), and picking Light on
        /// Appearance must leave the user on Appearance.
        /// </summary>
        private static class LastState
        {
            public static int Category;
            public static string Search = "";
        }

        private readonly Func<SettingsCategory>[] _factories =
        {
            () => new AppearanceSettings(),
            () => new BehaviorSettings(),
            () => new DataSettings(),
        };

        private static readonly string[] CategoryNames = { "Appearance", "Behavior", "Test History" };

        private static string NavLabel(int index) => CategoryNames[index];

        /// <summary>Group heading shown above the category at that index.</summary>
        private static readonly Dictionary<int, string> GroupHeadings = new()
        {
            [0] = "APP",
            [2] = "DATA"
        };

        private readonly SettingsCategory[] _categories;
        private Control _shown;
        private int _current = -1;

        public SettingsPageControl()
        {
            _categories = new SettingsCategory[_factories.Length];
            BackColor = themeManager.CurrentTheme.ContentBackground;

            BuildChrome();

            if (LastState.Search.Length > 0)
                _txtSearch.Text = LastState.Search; // TextChanged runs the search
            else
                ShowCategory(Math.Clamp(LastState.Category, 0, _factories.Length - 1));
        }

        // ── Categories ────────────────────────────────────────────────────────

        private SettingsCategory GetCategory(int index)
        {
            if (_categories[index] is { IsDisposed: false } built) return built;

            var category = _factories[index]();
            category.Build();
            return _categories[index] = category;
        }

        private void ShowCategory(int index, Control scrollTo = null)
        {
            _current = index;
            LastState.Category = index;
            SetActiveNav(index);

            var category = GetCategory(index);
            ShowContent(category);
            _btnReset.Visible = category.CanReset;

            if (scrollTo != null && !scrollTo.IsDisposed)
            {
                _host.ScrollControlIntoView(scrollTo);
                Flash(scrollTo);
            }
        }

        /// <summary>Swaps the pane's content. Categories are kept (cached); search results are thrown away.</summary>
        private void ShowContent(Control content)
        {
            _host.SuspendLayout();
            if (_shown != null && _shown != content)
            {
                _host.Controls.Remove(_shown);
                if (_shown is SettingsSearchResults) _shown.Dispose();
            }

            _shown = content;
            content.Location = Point.Empty;
            if (content.Parent != _host) _host.Controls.Add(content);
            SizeContent();
            _host.ResumeLayout(true);
            _host.AutoScrollPosition = Point.Empty;
        }

        private void RebuildCategory(int index)
        {
            if (IsDisposed) return;
            var old = _categories[index];
            _categories[index] = null;
            if (old != null)
            {
                if (_shown == old) { _host.Controls.Remove(old); _shown = null; }
                old.Dispose();
            }
            ShowCategory(index);
        }

        private void ResetCurrentPage()
        {
            int index = _current;
            var category = GetCategory(index);

            // Deferred: resetting Appearance switches the theme, which rebuilds
            // this whole page — not something to do inside the button's Click.
            BeginInvoke(() =>
            {
                category.ResetToDefaults();
                RebuildCategory(index);
            });
        }

        // ── Search ────────────────────────────────────────────────────────────

        private void RunSearch()
        {
            string query = _txtSearch.Text.Trim();
            LastState.Search = query;

            if (query.Length == 0)
            {
                ShowCategory(LastState.Category);
                return;
            }

            var entries = Enumerable.Range(0, _factories.Length).SelectMany(i => GetCategory(i).SearchEntries);
            var results = new SettingsSearchResults(query, entries,
                entry => BeginInvoke(() => OpenResult(entry)));

            SetActiveNav(-1);
            _btnReset.Visible = false;
            ShowContent(results);
        }

        private void OpenResult(SettingsCategory.SearchEntry entry)
        {
            if (IsDisposed) return;
            int index = Array.IndexOf(_categories, entry.Category);
            if (index < 0) return;

            // Clearing the box without re-running the search: we're leaving it on purpose.
            ClearSearchSilently();
            ShowCategory(index, entry.Target);
        }

        private void ClearSearchSilently()
        {
            _searchTimer.Stop();
            _suppressSearch = true;
            _txtSearch.Text = "";
            _suppressSearch = false;
            LastState.Search = "";
        }

        /// <summary>Ctrl+F anywhere on the page focuses search; Esc in the box clears it.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                return true;
            }
            if (keyData == Keys.Escape && _txtSearch.Focused && _txtSearch.TextLength > 0)
            {
                _txtSearch.Clear();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Highlight ─────────────────────────────────────────────────────────

        /// <summary>A one-second tint so the eye finds the row a search result jumped to.</summary>
        private void Flash(Control target)
        {
            var normal = target.BackColor;
            var tint = themeManager.CurrentTheme.HoverColor;
            var tinted = new List<Control> { target };
            tinted.AddRange(target.Controls.Cast<Control>().Where(c => c is Label));

            foreach (var c in tinted) c.BackColor = tint;

            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                timer.Dispose();
                foreach (var c in tinted.Where(c => !c.IsDisposed)) c.BackColor = normal;
            };
            timer.Start();
        }
    }
}
