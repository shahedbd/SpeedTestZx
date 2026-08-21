using System.Runtime.InteropServices;

namespace DpiCommon.Helper;

/// <summary>
/// Enhanced DPI-Aware UI Service for Windows Forms Applications
/// 
/// This service automatically handles DPI scaling for high-resolution displays.
/// It ensures your application looks correct on all screen resolutions and scale factors.
/// 
/// KEY IMPROVEMENTS:
/// - Per-monitor DPI awareness v2 support
/// - Dynamic DPI change handling
/// - Proper font scaling
/// - Layout-aware scaling methods
/// 
/// USAGE:
/// 1. Call DpiAwareService.Initialize() at the start of your application (in Program.cs)
/// 2. Call DpiAwareService.ApplyDpiScaling(this) in your Form's constructor
/// 3. Use DpiAwareService.Scale(value) for any hardcoded pixel values
/// 4. For UserControls, call DpiAwareService.ScaleControl(this) in constructor
/// </summary>
public static class DpiAwareService
{
    #region Fields

    private static float _dpiScale = 1.0f;
    private static int _dpi = 96;
    private static bool _initialized = false;
    private static float _baseFontSize = 9f;

    // Thread-safe scale factor for current context
    [ThreadStatic]
    private static float? _contextScale;

    #endregion

    #region Properties

    /// <summary>
    /// Current DPI scale factor (1.0 = 100%, 1.5 = 150%, 2.0 = 200%)
    /// Uses context-specific scale if available, otherwise global scale
    /// </summary>
    public static float ScaleFactor => _contextScale ?? _dpiScale;

    /// <summary>
    /// Current DPI value (96 = 100%, 144 = 150%, 192 = 200%)
    /// </summary>
    public static int Dpi => _dpi;

    /// <summary>
    /// Whether the service has been initialized
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Whether running on high DPI display (> 100%)
    /// </summary>
    public static bool IsHighDpi => _dpiScale > 1.0f;

    /// <summary>
    /// Base font size at 96 DPI
    /// </summary>
    public static float BaseFontSize => _baseFontSize;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize DPI awareness for the application.
    /// Call this ONCE at the start of your application in Program.cs BEFORE Application.Run()
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Try to set Per-Monitor DPI awareness V2 (Windows 10 1703+)
            // This is the best mode - supports per-monitor DPI and automatic rescaling
            if (Environment.OSVersion.Version >= new Version(10, 0, 15063))
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            // Fall back to Per-Monitor DPI awareness (Windows 8.1+)
            else if (Environment.OSVersion.Version >= new Version(6, 3))
            {
                SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
            }
            // Fall back to System DPI awareness (Windows Vista+)
            else
            {
                SetProcessDPIAware();
            }
        }
        catch
        {
            // Ignore errors - DPI awareness may already be set via manifest
        }

        // Get the initial DPI from primary monitor
        UpdateGlobalDpi();

        _initialized = true;
    }

    /// <summary>
    /// Update global DPI from system
    /// </summary>
    private static void UpdateGlobalDpi()
    {
        try
        {
            using var g = Graphics.FromHwnd(nint.Zero);
            _dpi = (int)g.DpiX;
            _dpiScale = _dpi / 96f;
        }
        catch
        {
            _dpi = 96;
            _dpiScale = 1.0f;
        }
    }

    #endregion

    #region Scaling Methods

    /// <summary>
    /// Scale a pixel value based on current DPI
    /// </summary>
    public static int Scale(int value) => (int)Math.Round(value * ScaleFactor);

    /// <summary>
    /// Scale a pixel value based on current DPI
    /// </summary>
    public static float Scale(float value) => value * ScaleFactor;

    /// <summary>
    /// Scale a pixel value based on a specific scale factor
    /// </summary>
    public static int Scale(int value, float scaleFactor) => (int)Math.Round(value * scaleFactor);

    /// <summary>
    /// Scale a Size based on current DPI
    /// </summary>
    public static Size Scale(Size size) => new Size(Scale(size.Width), Scale(size.Height));

    /// <summary>
    /// Scale a Size based on specific scale factor
    /// </summary>
    public static Size Scale(Size size, float scaleFactor) => new Size(
        Scale(size.Width, scaleFactor),
        Scale(size.Height, scaleFactor));

    /// <summary>
    /// Scale a Point based on current DPI
    /// </summary>
    public static Point Scale(Point point) => new Point(Scale(point.X), Scale(point.Y));

    /// <summary>
    /// Scale a Point based on specific scale factor
    /// </summary>
    public static Point Scale(Point point, float scaleFactor) => new Point(
        Scale(point.X, scaleFactor),
        Scale(point.Y, scaleFactor));

    /// <summary>
    /// Scale a Padding based on current DPI
    /// </summary>
    public static Padding Scale(Padding padding) => new Padding(
        Scale(padding.Left),
        Scale(padding.Top),
        Scale(padding.Right),
        Scale(padding.Bottom)
    );

    /// <summary>
    /// Scale a Padding based on specific scale factor
    /// </summary>
    public static Padding Scale(Padding padding, float scaleFactor) => new Padding(
        Scale(padding.Left, scaleFactor),
        Scale(padding.Top, scaleFactor),
        Scale(padding.Right, scaleFactor),
        Scale(padding.Bottom, scaleFactor)
    );

    /// <summary>
    /// Scale a Rectangle based on current DPI
    /// </summary>
    public static Rectangle Scale(Rectangle rect) => new Rectangle(
        Scale(rect.X),
        Scale(rect.Y),
        Scale(rect.Width),
        Scale(rect.Height)
    );

    /// <summary>
    /// Scale a SizeF based on current DPI
    /// </summary>
    public static SizeF Scale(SizeF size) => new SizeF(Scale(size.Width), Scale(size.Height));

    /// <summary>
    /// Get a DPI-scaled font
    /// </summary>
    public static Font ScaleFont(Font font)
    {
        if (ScaleFactor == 1.0f) return font;
        return new Font(font.FontFamily, font.Size * ScaleFactor, font.Style, font.Unit);
    }
    /// <summary>
    /// Create a DPI-aware font by name, size, and optional style.
    /// Fonts in Windows Forms are already point-based and DPI-independent,
    /// so this simply creates the font without multiplying by scale —
    /// matching the behaviour of every other control that uses point fonts.
    /// Use <see cref="ScaleFont(Font)"/> if you need a pixel-scaled font instead.
    /// </summary>
    public static Font ScaledFont(string familyName, float emSize,
        FontStyle style = FontStyle.Regular)
    {
        return new Font(familyName, emSize, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// Create a scaled font with the given parameters
    /// </summary>
    public static Font CreateScaledFont(string familyName, float baseSize, FontStyle style = FontStyle.Regular)
    {
        return new Font(familyName, baseSize, style);
    }

    /// <summary>
    /// Get DPI scale factor for a specific control (use when control is already created)
    /// </summary>
    public static float GetControlScale(Control control)
    {
        if (control is Form form)
            return form.DeviceDpi / 96f;

        // Walk up to find the containing form
        var parentForm = control.FindForm();
        if (parentForm != null)
            return parentForm.DeviceDpi / 96f;

        return ScaleFactor;
    }

    /// <summary>
    /// Set context-specific scale factor (for UserControl initialization)
    /// </summary>
    public static void SetContextScale(float scale)
    {
        _contextScale = scale;
    }

    /// <summary>
    /// Clear context-specific scale factor
    /// </summary>
    public static void ClearContextScale()
    {
        _contextScale = null;
    }

    /// <summary>
    /// Update the global scale factor from a form's DPI
    /// </summary>
    public static void UpdateScaleFromForm(Form form)
    {
        _dpi = form.DeviceDpi;
        _dpiScale = _dpi / 96f;
    }

    #endregion

    #region Form Scaling

    /// <summary>
    /// Apply DPI scaling to a form and all its controls.
    /// Call this in your form's constructor BEFORE InitializeComponent().
    /// </summary>
    public static void ApplyDpiScaling(Form form)
    {
        if (form == null) return;

        // Set form's auto-scale properties FIRST
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96F, 96F);

        // Update scale factor from form's actual DPI
        float formScale = form.DeviceDpi / 96f;

        // Update global scale if needed
        if (form.DeviceDpi != _dpi)
        {
            _dpi = form.DeviceDpi;
            _dpiScale = formScale;
        }

        // Handle DPI changed event for per-monitor DPI awareness
        form.DpiChanged += Form_DpiChanged;
    }

    /// <summary>
    /// Apply DPI scaling to a form with custom base size.
    /// The form will be sized appropriately for the current DPI.
    /// </summary>
    public static void ApplyDpiScaling(Form form, int baseWidth, int baseHeight, int minWidth = 0, int minHeight = 0)
    {
        if (form == null) return;

        ApplyDpiScaling(form);

        float formScale = form.DeviceDpi / 96f;

        // Scale the form size
        form.ClientSize = new Size(
            (int)Math.Round(baseWidth * formScale),
            (int)Math.Round(baseHeight * formScale)
        );

        // Scale minimum size if specified
        if (minWidth > 0 && minHeight > 0)
        {
            form.MinimumSize = new Size(
                (int)Math.Round(minWidth * formScale),
                (int)Math.Round(minHeight * formScale)
            );
        }
    }

    /// <summary>
    /// Handle DPI changes when form moves between monitors
    /// </summary>
    private static void Form_DpiChanged(object? sender, DpiChangedEventArgs e)
    {
        if (sender is not Form form) return;

        // Update global scale
        _dpi = e.DeviceDpiNew;
        _dpiScale = _dpi / 96f;

        // The form will automatically rescale with AutoScaleMode.Dpi
        // But we need to update any custom scaling

        // Optionally trigger a layout refresh
        form.PerformLayout();
    }

    /// <summary>
    /// Apply DPI scaling to a UserControl
    /// Call this in the UserControl constructor
    /// </summary>
    public static void ApplyDpiScaling(UserControl control)
    {
        if (control == null) return;

        control.AutoScaleMode = AutoScaleMode.Dpi;
        control.AutoScaleDimensions = new SizeF(96F, 96F);
    }

    /// <summary>
    /// Scale all controls in a container recursively.
    /// Use this for containers that were created with fixed pixel values.
    /// </summary>
    public static void ScaleControls(Control container, float scaleFactor)
    {
        if (container == null || Math.Abs(scaleFactor - 1.0f) < 0.01f) return;

        container.SuspendLayout();

        try
        {
            foreach (Control control in container.Controls)
            {
                // Scale location and size
                control.Location = new Point(
                    (int)Math.Round(control.Location.X * scaleFactor),
                    (int)Math.Round(control.Location.Y * scaleFactor)
                );
                control.Size = new Size(
                    (int)Math.Round(control.Size.Width * scaleFactor),
                    (int)Math.Round(control.Size.Height * scaleFactor)
                );

                // Scale padding and margin
                control.Padding = Scale(control.Padding, scaleFactor);
                control.Margin = Scale(new Padding(
                    control.Margin.Left,
                    control.Margin.Top,
                    control.Margin.Right,
                    control.Margin.Bottom), scaleFactor);

                // Recurse into child controls
                if (control.Controls.Count > 0)
                {
                    ScaleControls(control, scaleFactor);
                }
            }
        }
        finally
        {
            container.ResumeLayout(true);
        }
    }

    /// <summary>
    /// Apply DPI-aware styling to a DataGridView
    /// </summary>
    public static void ApplyDataGridViewScaling(DataGridView dgv)
    {
        if (dgv == null) return;

        float scale = ScaleFactor;

        dgv.RowTemplate.Height = Scale(25);
        dgv.ColumnHeadersHeight = Scale(30);

        foreach (DataGridViewColumn col in dgv.Columns)
        {
            if (col.Width > 0)
            {
                col.Width = (int)Math.Round(col.Width * scale);
            }
        }
    }

    #endregion

    #region Helper Methods for UI Creation

    /// <summary>
    /// Create a scaled location point from base coordinates
    /// </summary>
    public static Point ScaledLocation(int baseX, int baseY, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        return new Point((int)Math.Round(baseX * scale), (int)Math.Round(baseY * scale));
    }

    /// <summary>
    /// Create a scaled size from base dimensions
    /// </summary>
    public static Size ScaledSize(int baseWidth, int baseHeight, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        return new Size((int)Math.Round(baseWidth * scale), (int)Math.Round(baseHeight * scale));
    }

    /// <summary>
    /// Create scaled padding from base values
    /// </summary>
    public static Padding ScaledPadding(int all, float? customScale = null)
    {
        int scaled = Scale(all, customScale ?? ScaleFactor);
        return new Padding(scaled);
    }

    /// <summary>
    /// Create scaled padding from base values
    /// </summary>
    public static Padding ScaledPadding(int left, int top, int right, int bottom, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        return new Padding(
            Scale(left, scale),
            Scale(top, scale),
            Scale(right, scale),
            Scale(bottom, scale));
    }

    #endregion

    #region Button Styling

    /// <summary>
    /// Create a DPI-scaled modern button
    /// </summary>
    public static Button CreateScaledButton(string text, int baseWidth, int baseHeight, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;

        var button = new Button
        {
            Text = text,
            Size = ScaledSize(baseWidth, baseHeight, scale),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 120, 215)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 248, 255);

        return button;
    }

    /// <summary>
    /// Create a DPI-scaled primary (filled) button
    /// </summary>
    public static Button CreateScaledPrimaryButton(string text, int baseWidth, int baseHeight, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;

        var button = new Button
        {
            Text = text,
            Size = ScaledSize(baseWidth, baseHeight, scale),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 180);

        return button;
    }

    #endregion

    #region Label Styling

    /// <summary>
    /// Create a DPI-scaled label
    /// </summary>
    public static Label CreateScaledLabel(string text, float fontSize, int baseX, int baseY, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;

        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", fontSize),
            AutoSize = true,
            Location = ScaledLocation(baseX, baseY, scale),
            ForeColor = Color.FromArgb(50, 50, 50)
        };
    }

    /// <summary>
    /// Create a DPI-scaled title label
    /// </summary>
    public static Label CreateScaledTitleLabel(string text, int baseX, int baseY, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;

        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular),
            AutoSize = true,
            Location = ScaledLocation(baseX, baseY, scale),
            ForeColor = Color.FromArgb(50, 50, 50)
        };
    }

    #endregion

    #region Panel Styling

    /// <summary>
    /// Create a DPI-scaled card panel with border
    /// </summary>
    public static Panel CreateScaledCardPanel(int baseX, int baseY, int baseWidth, int baseHeight, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;

        var panel = new Panel
        {
            Location = ScaledLocation(baseX, baseY, scale),
            Size = ScaledSize(baseWidth, baseHeight, scale),
            BackColor = Color.White
        };

        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(225, 225, 225), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        return panel;
    }

    #endregion

    #region Icon Scaling

    /// <summary>
    /// Get a scaled icon size (16x16 at 100%, scales with DPI)
    /// </summary>
    public static Size GetSmallIconSize(float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        int size = (int)Math.Round(16 * scale);
        return new Size(size, size);
    }

    /// <summary>
    /// Get a scaled icon size (32x32 at 100%, scales with DPI)
    /// </summary>
    public static Size GetLargeIconSize(float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        int size = (int)Math.Round(32 * scale);
        return new Size(size, size);
    }

    /// <summary>
    /// Scale an icon to appropriate size for current DPI
    /// </summary>
    public static Image ScaleIcon(Icon icon, int baseSize, float? customScale = null)
    {
        float scale = customScale ?? ScaleFactor;
        int size = (int)Math.Round(baseSize * scale);

        try
        {
            var scaledIcon = new Icon(icon, size, size);
            return scaledIcon.ToBitmap();
        }
        catch
        {
            return icon.ToBitmap();
        }
    }

    #endregion

    #region Screen Information

    /// <summary>
    /// Get recommended window size for current screen
    /// </summary>
    public static Size GetRecommendedWindowSize(int baseWidth, int baseHeight)
    {
        var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

        int scaledWidth = Scale(baseWidth);
        int scaledHeight = Scale(baseHeight);

        // Ensure window fits on screen (max 90% of screen size)
        int maxWidth = (int)(screenBounds.Width * 0.9);
        int maxHeight = (int)(screenBounds.Height * 0.9);

        return new Size(
            Math.Min(scaledWidth, maxWidth),
            Math.Min(scaledHeight, maxHeight)
        );
    }

    /// <summary>
    /// Get screen DPI information
    /// </summary>
    public static string GetDpiInfo()
    {
        return $"DPI: {_dpi}, Scale: {_dpiScale:P0} ({_dpiScale * 100:F0}%)";
    }

    #endregion

    #region P/Invoke

    private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    private enum PROCESS_DPI_AWARENESS
    {
        Process_DPI_Unaware = 0,
        Process_System_DPI_Aware = 1,
        Process_Per_Monitor_DPI_Aware = 2
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDPIAware();

    [DllImport("shcore.dll", SetLastError = true)]
    private static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetProcessDpiAwarenessContext(int value);

    #endregion
}

/// <summary>
/// Extension methods for easy DPI scaling
/// </summary>
public static class DpiAwareExtensions
{
    /// <summary>
    /// Scale a value using DpiAwareService
    /// </summary>
    public static int ScaleDpi(this int value) => DpiAwareService.Scale(value);

    /// <summary>
    /// Scale a value using DpiAwareService
    /// </summary>
    public static float ScaleDpi(this float value) => DpiAwareService.Scale(value);

    /// <summary>
    /// Scale a Size using DpiAwareService
    /// </summary>
    public static Size ScaleDpi(this Size size) => DpiAwareService.Scale(size);

    /// <summary>
    /// Scale a Point using DpiAwareService
    /// </summary>
    public static Point ScaleDpi(this Point point) => DpiAwareService.Scale(point);

    /// <summary>
    /// Scale a Padding using DpiAwareService
    /// </summary>
    public static Padding ScaleDpi(this Padding padding) => DpiAwareService.Scale(padding);

    /// <summary>
    /// Apply DPI scaling to a form
    /// </summary>
    public static void ApplyDpiScaling(this Form form) => DpiAwareService.ApplyDpiScaling(form);

    /// <summary>
    /// Apply DPI scaling to a form with size
    /// </summary>
    public static void ApplyDpiScaling(this Form form, int baseWidth, int baseHeight, int minWidth = 0, int minHeight = 0)
        => DpiAwareService.ApplyDpiScaling(form, baseWidth, baseHeight, minWidth, minHeight);

    /// <summary>
    /// Apply DPI-aware styling to a DataGridView
    /// </summary>
    public static void ApplyDpiScaling(this DataGridView dgv) => DpiAwareService.ApplyDataGridViewScaling(dgv);

    /// <summary>
    /// Apply DPI scaling to a UserControl
    /// </summary>
    public static void ApplyDpiScaling(this UserControl control) => DpiAwareService.ApplyDpiScaling(control);

    /// <summary>
    /// Get the scale factor for a control
    /// </summary>
    public static float GetDpiScale(this Control control) => DpiAwareService.GetControlScale(control);
}