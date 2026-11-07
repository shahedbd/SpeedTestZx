using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Managers;
using SpeedTestZx.Model;
using SpeedTestZx.UserControls.Lib;

namespace SpeedTestZx.UserControls.About
{
    /// <summary>One card on the About screen, as described by the tier plan.</summary>
    public sealed class AboutCardSpec
    {
        public IconChar Icon { get; init; }
        public Color Accent { get; init; }
        public string Title { get; init; } = string.Empty;
        public (IconChar Icon, string Text, Color Accent)[] Rows { get; init; } = [];
        public string? Note { get; init; }

        /// <summary>Null for an informational card; set to show the Go Pro CTA.</summary>
        public string? CtaText { get; init; }
    }

    /// <summary>
    /// What the About screen shows for the user's current tier — edition badge
    /// and cards. Layout ported from CPU ZX Pro's About screen.
    ///
    /// Free → key features, then the Go Pro offer (AppConfig.ProBenefits).
    /// Pro  → the Pro licence (with its last Store verification), then the
    ///        key features as "Also included". No upsell.
    ///
    /// Content comes from AppConfig (AboutFeatures, ProBenefits) — edit those,
    /// not this file.
    /// </summary>
    public sealed class AboutTierPlan
    {
        private static readonly Color FreeAccent = Color.FromArgb(34, 197, 94);
        private static Color ProAccent => AppConfig.ProAccentStart;

        public string EditionLabel { get; private init; } = string.Empty;
        public Color EditionColor { get; private init; }
        public AboutCardSpec[] Cards { get; private init; } = [];

        /// <summary>Builds the plan for the tier the current entitlement resolves to.</summary>
        public static AboutTierPlan ForCurrentTier() =>
            EntitlementManager.Instance.HasAccess(UserTier.Pro) ? Pro() : Free();

        // ─────────────────────────────────────────────────────────────────────
        // PER-TIER PLANS
        // ─────────────────────────────────────────────────────────────────────

        private static AboutTierPlan Free() => new()
        {
            EditionLabel = "FREE",
            EditionColor = FreeAccent,
            Cards =
            [
                new AboutCardSpec
                {
                    Icon = IconChar.Star,
                    Accent = FreeAccent,
                    Title = "Key Features",
                    Rows = FeatureRows(FreeAccent)
                },
                new AboutCardSpec
                {
                    Icon = IconChar.Crown,
                    Accent = ProAccent,
                    Title = $"Go Pro — unlock everything in {AppConfig.AppName}",
                    Rows = AppConfig.ProBenefits,
                    Note = "One-time purchase through the Microsoft Store — no subscription, yours for good.",
                    CtaText = PremiumButton.Label
                }
            ]
        };

        private static AboutTierPlan Pro() => new()
        {
            EditionLabel = "PRO",
            EditionColor = ProAccent,
            Cards =
            [
                new AboutCardSpec
                {
                    Icon = IconChar.Crown,
                    Accent = ProAccent,
                    Title = "Pro is active — lifetime licence",
                    Rows = AppConfig.ProBenefits,
                    Note = ProLicenceNote()
                },
                new AboutCardSpec
                {
                    Icon = IconChar.CircleCheck,
                    Accent = FreeAccent,
                    Title = "Also included",
                    Rows = FeatureRows(FreeAccent)
                }
            ]
        };

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>AppConfig.AboutFeatures as check-marked card rows.</summary>
        private static (IconChar Icon, string Text, Color Accent)[] FeatureRows(Color accent) =>
            (AppConfig.AboutFeatures ?? new List<string>())
                .Select(text => (IconChar.CircleCheck, text, accent))
                .ToArray();

        /// <summary>
        /// Thank-you line plus when the Store last confirmed the licence
        /// (EntitlementManager.LastVerifiedUtc, shown in local time).
        /// </summary>
        private static string ProLicenceNote()
        {
            var verified = EntitlementManager.Instance.LastVerifiedUtc;
            string when = verified.HasValue
                ? $"Licence last verified {verified.Value.ToLocalTime():d MMM yyyy, HH:mm}."
                : "Licence verified through the Microsoft Store.";

            return $"Thank you for supporting {AppConfig.AppName}. {when}";
        }
    }
}
