// UserControls/Layout/HeaderControl.Pro.cs
using DpiCommon.Helper;
using TestNetSpeed.Helper;
using TestNetSpeed.Managers;
using TestNetSpeed.Model;
using TestNetSpeed.UserControls.Lib;

namespace TestNetSpeed.UserControls.Layout
{
    /// <summary>
    /// HeaderControl — the Go Pro button: a gradient pill with a gold crown
    /// (UserControls/Lib/PremiumButton.cs), left of About. Free: "Go Pro" opens the
    /// upgrade prompt. Pro: the same pill reads "Pro" and is a passive badge.
    /// Fixed width, so the switch never shifts the header.
    ///
    /// Kept out of ActionButtons on purpose: those are icon-only squares that
    /// RefreshForDpi resizes as a group.
    /// </summary>
    public partial class HeaderControl
    {
        private Button _btnGoPro = null!;

        /// <summary>
        /// Width fixed to the "Go Pro" label, padded for the crown and the pill's
        /// rounded ends, so switching to "Pro" never makes the header jump.
        /// </summary>
        private static int GoProWidth =>
            HeaderButtonService.MeasureButtonWidth(PremiumButton.Label) + DpiAwareService.Scale(16);

        /// <summary>Everything in the button bar, right to left: Theme → About → Settings → Go Pro.</summary>
        private Button[] BarButtons => new[] { BtnTheme, BtnAbout, BtnSettings, _btnGoPro };

        private void CreateProButton()
        {
            _btnGoPro = PremiumButton.Create(
                new Size(GoProWidth, HeaderButtonService.ButtonHeight),
                DpiAwareService.Scale(15),
                fontSize: 9.5f);
            _btnGoPro.Click += BtnGoPro_Click;

            EntitlementManager.Instance.EntitlementChanged += OnEntitlementChanged;
            UpdateProButton();
        }

        private void UpdateProButton()
        {
            bool isPro = EntitlementManager.Instance.HasAccess(UserTier.Pro);

            _btnGoPro.Text = "  " + (isPro ? "Pro" : PremiumButton.Label);
            _btnGoPro.Cursor = isPro ? Cursors.Default : Cursors.Hand;

            _toolTip.SetToolTip(_btnGoPro, isPro
                ? $"{AppConfig.AppName} Pro · lifetime license"
                : "Unlock ISP Report Card and Connection Monitor with a one-time purchase");

            StyleProButton();
        }

        /// <summary>
        /// Re-applies the gradient pill. Theme-independent by design, but the
        /// gradient is a bitmap sized to the button, so it is rebuilt on DPI too.
        /// </summary>
        private void StyleProButton() => PremiumButton.Apply(_btnGoPro);

        private void RefreshProButtonForDpi()
        {
            _btnGoPro.Size = new Size(GoProWidth, HeaderButtonService.ButtonHeight);
            _btnGoPro.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            StyleProButton();
        }

        private void OnEntitlementChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;

            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(UpdateProButton)); }
            else UpdateProButton();
        }

        private void BtnGoPro_Click(object? sender, EventArgs e)
        {
            if (!EntitlementManager.Instance.HasAccess(UserTier.Pro))
                PromptManager.Current?.ShowPro();
        }

        private void DisposeProButton() =>
            EntitlementManager.Instance.EntitlementChanged -= OnEntitlementChanged;
    }
}
