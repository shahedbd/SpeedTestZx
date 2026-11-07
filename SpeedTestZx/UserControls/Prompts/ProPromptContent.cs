using FontAwesome.Sharp;
using SpeedTestZx.Helper;
using SpeedTestZx.Model;

namespace SpeedTestZx.UserControls.Prompts
{
    /// <summary>
    /// The copy the upgrade card renders:
    ///   Benefits — what Pro unlocks (from AppConfig.ProBenefits).
    ///   Perks    — what the one-time purchase promises (no renewals,
    ///              updates and support for life), shown as a 2x2 grid.
    ///
    /// The subhead names the feature the user just tried to use, or reads
    /// generically when the card was opened from the header's Go Pro button.
    /// Ported from WiFiPasswordViewer.
    /// </summary>
    public sealed class ProPromptContent
    {
        public required string Headline { get; init; }
        public required string Subhead { get; init; }
        public (IconChar Icon, string Text, Color Accent)[] Benefits { get; init; } = [];
        public (IconChar Icon, string Title, string Detail)[] Perks { get; init; } = [];

        /// <summary>Purchase promises, in reading order.</summary>
        public static readonly (IconChar Icon, string Title, string Detail)[] LifetimePerks =
        [
            (IconChar.CircleCheck, "One-time payment", "No subscription. No renewals."),
            (IconChar.Lock, "Data stays on this PC", "Logs and reports never leave it."),
            (IconChar.ArrowsRotate, "Lifetime updates", "Every future version included."),
            (IconChar.Envelope, "Lifetime support", "Priority email support from the developer."),
        ];

        public static ProPromptContent ForFeature(ProFeature? feature) => new()
        {
            Headline = $"Unlock {AppConfig.AppName} Pro",
            Subhead = feature is { } f
                ? $"{FeatureName(f)} is part of {AppConfig.AppName} Pro.\nOne purchase, yours for life:"
                : "One purchase, yours for life:",
            Benefits = AppConfig.ProBenefits,
            Perks = LifetimePerks
        };

        /// <summary>User-facing name of a gated feature, for the subhead.</summary>
        public static string FeatureName(ProFeature feature) => feature switch
        {
            ProFeature.ReportLongRange => "30 / 90-day reports",
            ProFeature.ReportPdf => "The PDF complaint report",
            ProFeature.ReportCost => "The cost view",
            ProFeature.ReportWorstHours => "The worst-hours breakdown",
            ProFeature.FrequentTests => "Testing every 1, 3 or 6 hours",
            ProFeature.LatencyLog => "The full latency log",
            ProFeature.OutageHistory => "The 90-day outage history",
            ProFeature.Heatmap => "The time-of-day heatmap",
            ProFeature.SmartAlerts => "Smart alerts",
            _ => "This feature"
        };

        /// <summary>One-line pitch for a gated feature, shown on its inline upsell card.</summary>
        public static string FeatureDescription(ProFeature feature) => feature switch
        {
            ProFeature.ReportLongRange => "Grade your ISP over 30 or 90 days, or any range you pick.",
            ProFeature.ReportPdf => "A PDF with charts, outages and every test, ready to send to your ISP.",
            ProFeature.ReportCost => "See what the speed you paid for but didn't get costs you each month.",
            ProFeature.ReportWorstHours => "Find the hours of the day when your internet is slowest.",
            ProFeature.FrequentTests => "Automatic tests every 1, 3 or 6 hours instead of once a day.",
            ProFeature.LatencyLog => "Keep 90 days of per-minute latency and loss, not just 24 hours.",
            ProFeature.OutageHistory => "Every outage for 90 days, with its cause.",
            ProFeature.Heatmap => "A weekday × hour map of when your internet is good or bad.",
            ProFeature.SmartAlerts => "Alerts for slow speed, high latency and when you're back online.",
            _ => "One purchase unlocks every Pro feature, for life."
        };
    }
}
