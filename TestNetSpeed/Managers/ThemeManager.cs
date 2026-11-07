using System;
using System.Drawing;
using DeviceDataModule;
using SpeedTestZx.Helper;

namespace SpeedTestZx.Managers
{
    /// <summary>
    /// Manages application-wide theme settings using the Singleton pattern.
    /// Provides dark and light theme configurations with event-driven theme changes.
    /// </summary>
    public sealed class ThemeManager
    {
        #region Singleton Implementation

        private static readonly Lazy<ThemeManager> _instance =
            new Lazy<ThemeManager>(() => new ThemeManager());

        /// <summary>
        /// Gets the singleton instance of ThemeManager.
        /// </summary>
        public static ThemeManager Instance => _instance.Value;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently active theme configuration.
        /// </summary>
        public AppTheme CurrentTheme { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the dark theme is currently active.
        /// </summary>
        public bool IsDarkTheme { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the application theme is changed.
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        #endregion

        #region Constructor

        private ThemeManager()
        {
            IsDarkTheme = SettingsService.Current.IsDarkMode;
            CurrentTheme = IsDarkTheme ? CreateDarkTheme() : CreateLightTheme();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles between dark and light themes.
        /// </summary>
        public void ToggleTheme()
        {
            SetTheme(!IsDarkTheme);
        }

        /// <summary>
        /// Sets the application theme to dark or light mode.
        /// </summary>
        /// <param name="isDark">True for dark theme, false for light theme.</param>
        public void SetTheme(bool isDark)
        {
            if (IsDarkTheme == isDark)
                return; // No change needed

            var previousTheme = IsDarkTheme;
            IsDarkTheme = isDark;
            CurrentTheme = isDark ? CreateDarkTheme() : CreateLightTheme();

            SettingsService.Current.IsDarkMode = isDark;
            SettingsService.Save();

            OnThemeChanged(new ThemeChangedEventArgs(previousTheme, IsDarkTheme));
        }

        /// <summary>
        /// Gets a theme configuration by type.
        /// </summary>
        /// <param name="isDark">True for dark theme, false for light theme.</param>
        /// <returns>The requested theme configuration.</returns>
        public AppTheme GetTheme(bool isDark)
        {
            return isDark ? CreateDarkTheme() : CreateLightTheme();
        }

        #endregion

        #region Private Methods - Theme Creation

        private AppTheme CreateDarkTheme()
        {
            return new AppTheme
            {
                // Background Colors — seeded from AppConfig
                PrimaryBackground = AppConfig.BackgroundColor,
                SecondaryBackground = ColorHelper.FromRgb(25, 25, 25),
                SidebarBackground = ColorHelper.FromRgb(22, 22, 22),
                HeaderBackground = ColorHelper.FromRgb(28, 28, 28),
                ContentBackground = ColorHelper.FromRgb(30, 30, 30),
                CardBackground = AppConfig.SurfaceColor,
                // Seeded from AppConfig — deliberately a fixed "chrome" color
                // across both themes so the title bar reads as its own
                // distinct strip, not a blend with the rest of the window.
                TitleBarBackground = AppConfig.TitleBarColor,

                // Text Colors — seeded from AppConfig
                TextColor = AppConfig.TextPrimaryColor,
                SecondaryTextColor = AppConfig.TextSecondaryColor,
                HeadingColor = ColorHelper.FromRgb(255, 255, 255),

                // Accent Colors — seeded from AppConfig
                AccentColor = AppConfig.PrimaryColor,
                AccentHoverColor = ColorHelper.FromRgb(0, 100, 190),
                SuccessColor = ColorHelper.FromRgb(16, 185, 129),
                WarningColor = ColorHelper.FromRgb(245, 158, 11),
                ErrorColor = ColorHelper.FromRgb(239, 68, 68),
                InfoColor = ColorHelper.FromRgb(59, 130, 246),

                // UI Element Colors
                BorderColor = ColorHelper.FromRgb(45, 45, 45),
                HoverColor = ColorHelper.FromRgb(40, 40, 40),
                SelectedColor = ColorHelper.FromRgb(0, 120, 212),
                DisabledColor = ColorHelper.FromRgb(100, 100, 100),

                // Graph Colors
                GraphLineColor = ColorHelper.FromRgb(0, 120, 212),
                GraphFillColor = ColorHelper.FromArgb(50, 0, 120, 212),
                GraphGridColor = ColorHelper.FromRgb(50, 50, 50),
                GraphBackgroundColor = ColorHelper.FromRgb(25, 25, 25),

                // Theme Metadata
                Name = "Dark",
                IsDark = true
            };
        }

        private AppTheme CreateLightTheme()
        {
            return new AppTheme
            {
                // Background Colors
                PrimaryBackground = ColorHelper.FromRgb(255, 255, 255),
                SecondaryBackground = ColorHelper.FromRgb(248, 249, 250),
                SidebarBackground = ColorHelper.FromRgb(245, 245, 245),
                HeaderBackground = ColorHelper.FromRgb(250, 250, 250),
                ContentBackground = ColorHelper.FromRgb(255, 255, 255),
                CardBackground = ColorHelper.FromRgb(255, 255, 255),
                // Seeded from AppConfig — same fixed "chrome" color as the
                // dark theme (see CreateDarkTheme).
                TitleBarBackground = AppConfig.TitleBarColor,

                // Text Colors
                TextColor = ColorHelper.FromRgb(33, 33, 33),
                SecondaryTextColor = ColorHelper.FromRgb(100, 100, 100),
                HeadingColor = ColorHelper.FromRgb(0, 0, 0),

                // Accent Colors — seeded from AppConfig
                AccentColor = AppConfig.PrimaryColor,
                AccentHoverColor = ColorHelper.FromRgb(0, 100, 190),
                SuccessColor = ColorHelper.FromRgb(16, 185, 129),
                WarningColor = ColorHelper.FromRgb(245, 158, 11),
                ErrorColor = ColorHelper.FromRgb(239, 68, 68),
                InfoColor = ColorHelper.FromRgb(59, 130, 246),

                // UI Element Colors
                BorderColor = ColorHelper.FromRgb(220, 220, 220),
                HoverColor = ColorHelper.FromRgb(240, 240, 240),
                SelectedColor = ColorHelper.FromRgb(0, 120, 212),
                DisabledColor = ColorHelper.FromRgb(180, 180, 180),

                // Graph Colors
                GraphLineColor = ColorHelper.FromRgb(0, 120, 212),
                GraphFillColor = ColorHelper.FromArgb(50, 0, 120, 212),
                GraphGridColor = ColorHelper.FromRgb(230, 230, 230),
                GraphBackgroundColor = ColorHelper.FromRgb(250, 250, 250),

                // Theme Metadata
                Name = "Light",
                IsDark = false
            };
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Raises the ThemeChanged event.
        /// </summary>
        /// <param name="e">Event arguments containing theme change information.</param>
        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ThemeChanged?.Invoke(this, e);
        }

        #endregion
    }

    #region AppTheme Class

    /// <summary>
    /// Represents a complete theme configuration for the application.
    /// </summary>
    public sealed class AppTheme
    {
        #region Background Colors

        /// <summary>Gets or sets the primary background color.</summary>
        public Color PrimaryBackground { get; set; }

        /// <summary>Gets or sets the secondary background color.</summary>
        public Color SecondaryBackground { get; set; }

        /// <summary>Gets or sets the sidebar background color.</summary>
        public Color SidebarBackground { get; set; }

        /// <summary>Gets or sets the header background color.</summary>
        public Color HeaderBackground { get; set; }

        /// <summary>Gets or sets the title bar background color — deliberately
        /// distinct from HeaderBackground/ContentBackground so it reads as
        /// its own chrome strip.</summary>
        public Color TitleBarBackground { get; set; }

        /// <summary>Gets or sets the content area background color.</summary>
        public Color ContentBackground { get; set; }

        /// <summary>Gets or sets the card/panel background color.</summary>
        public Color CardBackground { get; set; }

        #endregion

        #region Text Colors

        /// <summary>Gets or sets the primary text color.</summary>
        public Color TextColor { get; set; }

        /// <summary>Gets or sets the secondary text color.</summary>
        public Color SecondaryTextColor { get; set; }

        /// <summary>Gets or sets the heading text color.</summary>
        public Color HeadingColor { get; set; }

        #endregion

        #region Accent Colors

        /// <summary>Gets or sets the primary accent color.</summary>
        public Color AccentColor { get; set; }

        /// <summary>Gets or sets the accent hover color.</summary>
        public Color AccentHoverColor { get; set; }

        /// <summary>Gets or sets the success state color.</summary>
        public Color SuccessColor { get; set; }

        /// <summary>Gets or sets the warning state color.</summary>
        public Color WarningColor { get; set; }

        /// <summary>Gets or sets the error state color.</summary>
        public Color ErrorColor { get; set; }

        /// <summary>Gets or sets the information state color.</summary>
        public Color InfoColor { get; set; }

        #endregion

        #region UI Element Colors

        /// <summary>Gets or sets the border color.</summary>
        public Color BorderColor { get; set; }

        /// <summary>Gets or sets the hover state color.</summary>
        public Color HoverColor { get; set; }

        /// <summary>Gets or sets the selected state color.</summary>
        public Color SelectedColor { get; set; }

        /// <summary>Gets or sets the disabled state color.</summary>
        public Color DisabledColor { get; set; }

        #endregion

        #region Graph Colors

        /// <summary>Gets or sets the graph line color.</summary>
        public Color GraphLineColor { get; set; }

        /// <summary>Gets or sets the graph fill color.</summary>
        public Color GraphFillColor { get; set; }

        /// <summary>Gets or sets the graph grid color.</summary>
        public Color GraphGridColor { get; set; }

        /// <summary>Gets or sets the graph background color.</summary>
        public Color GraphBackgroundColor { get; set; }

        #endregion

        #region Metadata

        /// <summary>Gets or sets the theme name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets whether this is a dark theme.</summary>
        public bool IsDark { get; set; }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a deep copy of the current theme.
        /// </summary>
        /// <returns>A new AppTheme instance with copied values.</returns>
        public AppTheme Clone()
        {
            return (AppTheme)this.MemberwiseClone();
        }

        #endregion
    }

    #endregion

    #region ThemeChangedEventArgs

    /// <summary>
    /// Provides data for the ThemeChanged event.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>Gets whether the previous theme was dark.</summary>
        public bool WasDarkTheme { get; }

        /// <summary>Gets whether the new theme is dark.</summary>
        public bool IsDarkTheme { get; }

        /// <summary>
        /// Initializes a new instance of the ThemeChangedEventArgs class.
        /// </summary>
        /// <param name="wasDark">Whether the previous theme was dark.</param>
        /// <param name="isDark">Whether the new theme is dark.</param>
        public ThemeChangedEventArgs(bool wasDark, bool isDark)
        {
            WasDarkTheme = wasDark;
            IsDarkTheme = isDark;
        }
    }

    #endregion

    #region ColorHelper

    /// <summary>
    /// Provides helper methods for color creation.
    /// </summary>
    internal static class ColorHelper
    {
        /// <summary>
        /// Creates a color from RGB values.
        /// </summary>
        public static Color FromRgb(int r, int g, int b)
        {
            return Color.FromArgb(255, r, g, b);
        }

        /// <summary>
        /// Creates a color from ARGB values.
        /// </summary>
        public static Color FromArgb(int a, int r, int g, int b)
        {
            return Color.FromArgb(a, r, g, b);
        }
    }

    #endregion
}
