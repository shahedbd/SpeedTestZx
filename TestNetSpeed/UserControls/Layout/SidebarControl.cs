// UserControls/Layout/SidebarControl.cs
using DeviceDataModule;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using DpiCommon.Helper;
using FontAwesome.Sharp;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// Part 03 — collapsible sidebar. Top-most: expand/collapse toggle icon,
    /// top-right. Top: app icon + AppConfig.AppName. Main nav: rendered from
    /// AppConfig.NavItems by NavigationManager via AddNavButton.
    /// </summary>
    public class SidebarControl : UserControl
    {
        public FlowLayoutPanel NavigationPanel { get; private set; }
        public Button BtnToggle { get; private set; }

        public bool IsCollapsed { get; private set; }

        private readonly ThemeManager _themeManager;
        private Panel _togglePanel;
        private Panel _logoPanel;
        private Panel _logoBottomBorder;
        private PictureBox _pbAppIcon;
        private Label _lblLogo;
        private Button _activeButton;

        public SidebarControl()
        {
            _themeManager = ThemeManager.Instance;

            Dock = DockStyle.Left;
            Width = DpiAwareService.Scale(AppConfig.SidebarWidth);

            BuildLayout();
            ApplyTheme();

            // Restore the user's last collapsed/expanded state. Applied
            // after BuildLayout so the toggle/logo panels already exist;
            // nav buttons haven't been added yet (NavigationManager does
            // that afterward), but AddNavButton already checks IsCollapsed
            // for each new button's label visibility, so they come in
            // correctly regardless of order.
            if (SettingsService.Current.IsSidebarCollapsed)
                SetCollapsed(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BUILD
        // ─────────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            NavigationPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(
                    DpiAwareService.Scale(8), DpiAwareService.Scale(5),
                    DpiAwareService.Scale(8), DpiAwareService.Scale(5))
            };

            Controls.Add(NavigationPanel);
            Controls.Add(BuildLogoPanel());
            Controls.Add(BuildTogglePanel());
        }

        private Panel BuildTogglePanel()
        {
            _togglePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = DpiAwareService.Scale(40),
                // Small margin so the icon doesn't sit flush against the
                // panel's own edges.
                Padding = new Padding(0, DpiAwareService.Scale(4), DpiAwareService.Scale(6), DpiAwareService.Scale(4))
            };

            var toggleIcon = new IconButton
            {
                IconChar = IconChar.Bars,
                IconSize = DpiAwareService.Scale(16),
                Text = string.Empty,
                Dock = DockStyle.Right,
                Width = DpiAwareService.Scale(32),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            toggleIcon.FlatAppearance.BorderSize = 0;
            // Click wiring is owned by NavigationManager.BuildSidebar (per
            // Doc/Project UI Structure.md's Architecture Notes) — not wired here.

            BtnToggle = toggleIcon;
            _togglePanel.Controls.Add(BtnToggle);
            return _togglePanel;
        }

        private Panel BuildLogoPanel()
        {
            int logoHeight = DpiAwareService.Scale(100);
            int iconSize = DpiAwareService.Scale(45);

            _logoPanel = new Panel { Height = logoHeight, Dock = DockStyle.Top };

            _pbAppIcon = new PictureBox
            {
                Image = File.Exists(AppConfig.AppIconPath) ? Image.FromFile(AppConfig.AppIconPath) : null,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                Location = new Point((Width - iconSize) / 2, DpiAwareService.Scale(12)),
                BackColor = Color.Transparent
            };

            _lblLogo = new Label
            {
                Text = AppConfig.AppName,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(Width, DpiAwareService.Scale(28)),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, DpiAwareService.Scale(64)),
                BackColor = Color.Transparent
            };

            // Separates the branding block (toggle + icon + name) from the
            // nav list below.
            _logoBottomBorder = new Panel { Dock = DockStyle.Bottom, Height = DpiAwareService.Scale(1) };

            _logoPanel.Controls.Add(_pbAppIcon);
            _logoPanel.Controls.Add(_lblLogo);
            _logoPanel.Controls.Add(_logoBottomBorder);
            return _logoPanel;
        }

        // ─────────────────────────────────────────────────────────────────────
        // NAV BUTTONS (driven by AppConfig.NavItems via NavigationManager)
        // ─────────────────────────────────────────────────────────────────────

        public Button AddNavButton(NavItem item)
        {
            int buttonWidth = Width - DpiAwareService.Scale(16);
            int buttonHeight = DpiAwareService.Scale(44);

            var btn = new Button
            {
                Text = string.Empty,
                Size = new Size(buttonWidth, buttonHeight),
                Margin = new Padding(0, 0, 0, DpiAwareService.Scale(2)),
                FlatStyle = FlatStyle.Flat,
                BackColor = _themeManager.CurrentTheme.SidebarBackground,
                ForeColor = _themeManager.CurrentTheme.TextColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Tag = item
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = GetHoverColor();

            int iconSize = DpiAwareService.Scale(22);
            var iconBox = new IconPictureBox
            {
                Name = "icon",
                IconChar = item.Icon,
                // Each nav item's own brand color — shown always, active or
                // not, so the sidebar reads as colorful rather than
                // monochrome. Never overridden elsewhere (see SetActiveButton
                // / ApplyTheme).
                IconColor = item.IconColor,
                IconSize = iconSize,
                Location = new Point(DpiAwareService.Scale(12), (buttonHeight - iconSize) / 2),
                Size = new Size(iconSize, iconSize),
                BackColor = Color.Transparent,
                // Disabled so it never captures the click itself — same as
                // lblText below. Collapsed, the icon is the only visible
                // part of the button, so without this the whole nav item
                // becomes unclickable.
                Enabled = false
            };

            int textX = DpiAwareService.Scale(44);
            var lblText = new Label
            {
                Name = "label",
                Text = item.Label,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = _themeManager.CurrentTheme.TextColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(buttonWidth - textX - DpiAwareService.Scale(8), buttonHeight),
                Location = new Point(textX, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Enabled = false,
                Visible = !IsCollapsed
            };

            btn.Controls.Add(iconBox);
            btn.Controls.Add(lblText);
            NavigationPanel.Controls.Add(btn);

            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ACTIVE BUTTON STATE
        // ─────────────────────────────────────────────────────────────────────

        public void SetActiveButton(Button button)
        {
            foreach (Control ctrl in NavigationPanel.Controls)
            {
                if (ctrl is not Button btn) continue;

                btn.BackColor = _themeManager.CurrentTheme.SidebarBackground;

                foreach (Control child in btn.Controls)
                {
                    if (child is Label lbl)
                    {
                        lbl.ForeColor = _themeManager.CurrentTheme.TextColor;
                        lbl.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
                    }
                    // Icon color is each item's own brand color and is never
                    // reset here — only the label/background indicate state.
                }
            }

            if (button is null) return;

            _activeButton = button;
            Color accent = _themeManager.CurrentTheme.AccentColor;
            button.BackColor = Color.FromArgb(40, accent);

            foreach (Control child in button.Controls)
            {
                if (child is Label lbl)
                {
                    lbl.ForeColor = accent;
                    lbl.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // COLLAPSE / EXPAND
        // ─────────────────────────────────────────────────────────────────────

        public void ToggleCollapse() => SetCollapsed(!IsCollapsed);

        public void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
            Width = DpiAwareService.Scale(collapsed ? AppConfig.SidebarCollapsedWidth : AppConfig.SidebarWidth);

            _lblLogo.Visible = !collapsed;

            foreach (Control ctrl in NavigationPanel.Controls)
            {
                if (ctrl is not Button btn) continue;
                foreach (Control child in btn.Controls)
                {
                    if (child is Label lbl)
                        lbl.Visible = !collapsed;
                }
            }

            // Buttons were sized for the previous (expanded/collapsed) width —
            // without this they stay oversized, overflow the narrower
            // container, and the FlowLayoutPanel grows a horizontal scrollbar.
            ResizeNavButtons();

            SettingsService.Current.IsSidebarCollapsed = collapsed;
            SettingsService.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            BackColor = _themeManager.CurrentTheme.SidebarBackground;
            NavigationPanel.BackColor = _themeManager.CurrentTheme.SidebarBackground;

            _togglePanel.BackColor = GetLogoPanelColor();
            BtnToggle.BackColor = GetLogoPanelColor();
            BtnToggle.FlatAppearance.MouseOverBackColor = GetHoverColor();
            if (BtnToggle is IconButton toggleIcon)
                toggleIcon.IconColor = _themeManager.CurrentTheme.TextColor;

            _logoPanel.BackColor = GetLogoPanelColor();
            _lblLogo.ForeColor = _themeManager.CurrentTheme.HeadingColor;
            _logoBottomBorder.BackColor = _themeManager.CurrentTheme.BorderColor;

            foreach (Control ctrl in NavigationPanel.Controls)
            {
                if (ctrl is not Button btn) continue;

                btn.BackColor = _themeManager.CurrentTheme.SidebarBackground;
                btn.FlatAppearance.MouseOverBackColor = GetHoverColor();

                foreach (Control child in btn.Controls)
                {
                    if (child is Label lbl)
                        lbl.ForeColor = _themeManager.CurrentTheme.TextColor;
                    // Icon color is each item's own brand color and is
                    // theme-independent — left untouched here.
                }
            }

            if (_activeButton is not null)
                SetActiveButton(_activeButton);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DPI REFRESH
        // ─────────────────────────────────────────────────────────────────────

        public void RefreshForDpi()
        {
            Width = DpiAwareService.Scale(IsCollapsed ? AppConfig.SidebarCollapsedWidth : AppConfig.SidebarWidth);

            _togglePanel.Height = DpiAwareService.Scale(40);
            _togglePanel.Padding = new Padding(0, DpiAwareService.Scale(4), DpiAwareService.Scale(6), DpiAwareService.Scale(4));

            if (BtnToggle is IconButton toggleIcon)
            {
                toggleIcon.Width = DpiAwareService.Scale(32);
                toggleIcon.IconSize = DpiAwareService.Scale(16);
            }

            int iconSizeLogo = DpiAwareService.Scale(45);
            _logoPanel.Height = DpiAwareService.Scale(100);
            _pbAppIcon.Size = new Size(iconSizeLogo, iconSizeLogo);
            _pbAppIcon.Location = new Point((Width - iconSizeLogo) / 2, DpiAwareService.Scale(12));
            _lblLogo.Size = new Size(Width, DpiAwareService.Scale(28));
            _lblLogo.Location = new Point(0, DpiAwareService.Scale(64));

            ResizeNavButtons();
        }

        /// <summary>
        /// Resizes every nav button (and its icon/label) to fit the sidebar's
        /// current Width. Shared by RefreshForDpi and SetCollapsed — both
        /// change the effective button width and need the same math.
        /// </summary>
        private void ResizeNavButtons()
        {
            int buttonWidth = Width - DpiAwareService.Scale(16);
            int buttonHeight = DpiAwareService.Scale(44);
            int iconSize = DpiAwareService.Scale(22);
            int iconX = DpiAwareService.Scale(12);
            int textX = DpiAwareService.Scale(44);

            foreach (Control ctrl in NavigationPanel.Controls)
            {
                if (ctrl is not Button btn) continue;

                btn.Size = new Size(buttonWidth, buttonHeight);
                btn.Margin = new Padding(0, 0, 0, DpiAwareService.Scale(2));

                foreach (Control child in btn.Controls)
                {
                    if (child is IconPictureBox iconBox)
                    {
                        iconBox.IconSize = iconSize;
                        iconBox.Size = new Size(iconSize, iconSize);
                        iconBox.Location = new Point(iconX, (buttonHeight - iconSize) / 2);
                    }
                    else if (child is Label lbl)
                    {
                        lbl.Size = new Size(buttonWidth - textX - DpiAwareService.Scale(8), buttonHeight);
                        lbl.Location = new Point(textX, 0);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private Color GetHoverColor() =>
            _themeManager.IsDarkTheme ? Color.FromArgb(50, 50, 50) : Color.FromArgb(230, 235, 240);

        private Color GetLogoPanelColor() =>
            _themeManager.IsDarkTheme ? Color.FromArgb(30, 30, 30) : Color.FromArgb(245, 247, 250);
    }
}
