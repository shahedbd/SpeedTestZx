namespace TestNetSpeed.Helper
{
    /// <summary>Display formatting shared by the Report Card and the Monitor.</summary>
    public static class Format
    {
        /// <summary>"45 s", "6 min", "2 h 5 min", "3 d 4 h".</summary>
        public static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalMinutes < 1) return $"{span.Seconds} s";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min";
            if (span.TotalDays < 1) return span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} min";
            return $"{(int)span.TotalDays} d {span.Hours} h";
        }

        /// <summary>"8 pm", "11 am" — hour-of-day labels.</summary>
        public static string Hour(int hour) => hour switch
        {
            0 => "12 am",
            12 => "12 pm",
            < 12 => $"{hour} am",
            _ => $"{hour - 12} pm",
        };

        /// <summary>"8–11 pm" style range for hours [start, endExclusive).</summary>
        public static string HourRange(int start, int endExclusive) =>
            $"{Hour(start)}–{Hour(endExclusive % 24)}";

        public static string Pct(double? value) => value is double v ? $"{v:0}%" : "—";

        public static string Mbps(double? value) => value is double v ? $"{v:0.#} Mbps" : "—";

        public static string Money(string currency, double amount) =>
            string.IsNullOrWhiteSpace(currency) ? $"{amount:0.00}" : $"{currency}{amount:0.00}";
    }
}
