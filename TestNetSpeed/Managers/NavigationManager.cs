using TestNetSpeed.Helper;
using TestNetSpeed.UserControls.Layout;
using TestNetSpeed.UserControls.Prompts;

namespace TestNetSpeed.Managers
{
    /// <summary>
    /// Owns sidebar nav-button rendering (from AppConfig.NavItems), the sidebar
    /// expand/collapse toggle, and content-panel page switching.
    /// </summary>
    public class NavigationManager
    {
        // ── Singleton — lets any page trigger a nav switch (e.g. Saved WiFi's
        // "Generate QR Code" jumping to the QR Code tab) without threading a
        // reference through every constructor. Mirrors ThemeManager.Instance.
        private static readonly Lazy<NavigationManager> _instance = new(() => new NavigationManager());
        public static NavigationManager Instance => _instance.Value;

        private UserControl _currentControl;
        private SidebarControl _sidebar;
        private Button _activePageButton;

        // ─────────────────────────────────────────────────────────────────────
        // SIDEBAR SETUP
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds one nav button per AppConfig.NavItems entry, wires each to
        /// either load its ControlType into contentPanel or invoke its
        /// ShowModal action (e.g. an About dialog), and wires the sidebar's
        /// "..." button to toggle collapse/expand. Call once during form load.
        /// </summary>
        public void BuildSidebar(SidebarControl sidebar, Panel contentPanel)
        {
            _sidebar = sidebar;
            Button firstPageButton = null;

            foreach (var item in AppConfig.NavItems)
            {
                var button = sidebar.AddNavButton(item);

                // "PRO" pill until the user has the feature (ProNavBadge hides itself).
                if (item.ProBadge is { } feature)
                    ProNavBadge.Attach(button, feature);

                button.Click += (s, e) =>
                {
                    if (item.ShowModal != null)
                    {
                        // A dialog, not a page — don't touch content or the
                        // active-page highlight.
                        item.ShowModal();
                        return;
                    }

                    var control = (UserControl)Activator.CreateInstance(item.ControlType);
                    ShowPage(control, contentPanel);
                    sidebar.SetActiveButton(button);
                    _activePageButton = button;
                };

                if (item.ShowModal == null)
                    firstPageButton ??= button;
            }

            sidebar.BtnToggle.Click += (s, e) => sidebar.ToggleCollapse();

            // Land on the first actual page on startup — never auto-open a
            // modal item even if one happens to be first in the list.
            firstPageButton?.PerformClick();
        }

        /// <summary>
        /// Switches to the page whose AppConfig.NavItems entry has this
        /// Label, as if its sidebar button were clicked. Used when one page
        /// needs to hand off to another (e.g. Saved WiFi → QR Code) rather
        /// than the user picking it from the sidebar directly.
        /// </summary>
        public void NavigateTo(string label)
        {
            if (_sidebar == null) return;

            foreach (Control ctrl in _sidebar.NavigationPanel.Controls)
            {
                if (ctrl is Button btn && btn.Tag is NavItem item &&
                    item.Label == label && item.ShowModal == null)
                {
                    btn.PerformClick();
                    return;
                }
            }
        }

        /// <summary>
        /// Re-clicks the currently active page's sidebar button, rebuilding
        /// it from scratch. Page controls set their theme colors once at
        /// construction and don't listen for ThemeManager.ThemeChanged
        /// themselves (unlike AboutForm/AlertDialog/the layout controls, which
        /// do) — reusing the exact same "construct fresh + ShowPage" path a
        /// real nav click takes is the simplest way to make the active page
        /// pick up a new theme immediately instead of only on the next manual
        /// tab switch. Call this from a ThemeChanged handler, not ApplyTheme()
        /// itself — it also fires on initial load, before any page is active.
        /// </summary>
        public void RefreshActivePage()
        {
            _activePageButton?.PerformClick();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONTENT SWITCHING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Swaps the content panel's page. The sidebar is a flat, single-level
        /// nav (no back stack), so the outgoing control is disposed rather
        /// than retained — otherwise every nav click would leak a control.
        /// </summary>
        public void ShowPage(UserControl control, Panel containerPanel)
        {
            // An open upgrade prompt belongs to the page being left.
            PromptManager.Current?.CloseAll();

            if (_currentControl != null)
            {
                containerPanel.Controls.Remove(_currentControl);
                _currentControl.Dispose();
            }

            _currentControl = control;
            control.Dock = DockStyle.Fill;
            containerPanel.Controls.Add(control);
        }
    }
}
