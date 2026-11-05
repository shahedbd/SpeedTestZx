using System.Text.Json;

namespace TestNetSpeed.Service
{
    /// <summary>ISP and test-location details for the current connection (T05).</summary>
    public record ConnectionInfo(string Isp, string City, string Country, string Colo)
    {
        public static readonly ConnectionInfo Empty = new("", "", "", "");

        /// <summary>"Cloudflare · SIN", or empty when the data centre is unknown.</summary>
        public string ServerText => Colo.Length > 0 ? $"Cloudflare · {Colo}" : string.Empty;
    }

    /// <summary>
    /// Looks up the ISP and the Cloudflare data centre that will serve the
    /// test. The same host the speed test already talks to answers this, so
    /// no extra third-party service is involved:
    ///
    ///   1. speed.cloudflare.com/meta  — JSON with asOrganization (ISP),
    ///      city, country and colo.
    ///   2. speed.cloudflare.com/cdn-cgi/trace — plain "key=value" lines;
    ///      gives colo and country only. Used when /meta fails.
    ///
    /// Never throws: a failed lookup returns <see cref="ConnectionInfo.Empty"/>
    /// and the test result simply has no ISP.
    /// </summary>
    public class ConnectionInfoService
    {
        private const string MetaUrl = "https://speed.cloudflare.com/meta";
        private const string TraceUrl = "https://speed.cloudflare.com/cdn-cgi/trace";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

        public async Task<ConnectionInfo> GetAsync(CancellationToken ct = default)
        {
            var fromMeta = await TryMetaAsync(ct);
            if (fromMeta != null && fromMeta.Isp.Length > 0)
                return fromMeta;

            var fromTrace = await TryTraceAsync(ct);
            if (fromTrace != null)
            {
                // Keep anything /meta did give us; trace fills the gaps.
                return fromMeta == null ? fromTrace : fromMeta with
                {
                    Colo = fromMeta.Colo.Length > 0 ? fromMeta.Colo : fromTrace.Colo,
                    Country = fromMeta.Country.Length > 0 ? fromMeta.Country : fromTrace.Country,
                };
            }

            return fromMeta ?? ConnectionInfo.Empty;
        }

        private static async Task<ConnectionInfo?> TryMetaAsync(CancellationToken ct)
        {
            try
            {
                using var res = await _http.GetAsync(MetaUrl, ct);
                if (!res.IsSuccessStatusCode) return null;

                await using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                return new ConnectionInfo(
                    Isp: Str(root, "asOrganization"),
                    City: Str(root, "city"),
                    Country: Str(root, "country"),
                    Colo: Str(root, "colo"));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return null; }
        }

        private static async Task<ConnectionInfo?> TryTraceAsync(CancellationToken ct)
        {
            try
            {
                string text = await _http.GetStringAsync(TraceUrl, ct);
                var map = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .GroupBy(p => p[0].Trim())
                    .ToDictionary(g => g.Key, g => g.First()[1].Trim());

                return new ConnectionInfo(
                    Isp: "",
                    City: "",
                    Country: map.GetValueOrDefault("loc", ""),
                    Colo: map.GetValueOrDefault("colo", ""));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return null; }
        }

        /// <summary>String property, or "" when missing / not a string (asn etc. are numbers).</summary>
        private static string Str(JsonElement root, string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()?.Trim() ?? ""
                : "";
    }
}
