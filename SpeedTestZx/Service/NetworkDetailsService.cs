using SpeedTestZx.Model;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SpeedTestZx.Service
{
    public class NetworkDetailsService
    {
        // ── Shared HTTP client — never recreate per call ──────────────────────
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        // ══════════════════════════════════════════════════════════════════════
        //  MAIN ENTRY — returns local details instantly, public IP async
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns local network details immediately (synchronous read from OS).
        /// Public IP is populated separately via GetPublicIpAsync() so the UI
        /// doesn't wait for an HTTP call before showing anything.
        /// </summary>
        public Task<NetworkDetails> GetLocalDetailsAsync()
        {
            // All local data comes from NetworkInterface — no I/O, instant
            var details = new NetworkDetails();

            try
            {
                var adapter = GetActiveWirelessAdapter()
                           ?? GetAnyActiveAdapter();

                if (adapter == null) return Task.FromResult(details);

                details.AdapterName = adapter.Name;
                details.MacAddress = FormatMac(
                    adapter.GetPhysicalAddress().ToString());
                details.ConnectionType = adapter.NetworkInterfaceType.ToString();

                details.LinkSpeed = adapter.Speed > 0
                    ? $"{adapter.Speed / 1_000_000} Mbps"
                    : "Unknown";

                var ipProps = adapter.GetIPProperties();

                // Local IPv4
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a =>
                        a.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(a.Address));

                if (ipv4 != null)
                {
                    details.LocalIpAddress = ipv4.Address.ToString();
                    details.SubnetMask = ipv4.IPv4Mask?.ToString()
                                          ?? string.Empty;
                }

                // IPv6 link-local
                var ipv6 = ipProps.UnicastAddresses
                    .FirstOrDefault(a =>
                        a.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                        a.Address.IsIPv6LinkLocal);

                if (ipv6 != null)
                    details.IPv6Address = ipv6.Address.ToString();

                // Gateway
                var gateway = ipProps.GatewayAddresses
                    .FirstOrDefault(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork);

                if (gateway != null)
                    details.Gateway = gateway.Address.ToString();

                // DNS
                details.DnsServers = string.Join(", ",
                    ipProps.DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                        .Select(d => d.ToString()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDetailsService] Local: {ex.Message}");
            }

            return Task.FromResult(details);
        }

        /// <summary>
        /// Fetches public IP with a short timeout and two fallback endpoints.
        /// Call this separately so local details render first.
        /// </summary>
        public async Task<string> GetPublicIpAsync(
            CancellationToken cancellationToken = default)
        {
            string[] endpoints =
            [
                "https://api.ipify.org",
                "https://checkip.amazonaws.com"
            ];

            foreach (var url in endpoints)
            {
                try
                {
                    var ip = await _http.GetStringAsync(url, cancellationToken);
                    return ip.Trim();
                }
                catch { /* try next */ }
            }

            return "Unavailable";
        }

        /// <summary>
        /// Convenience method: returns full details including public IP.
        /// Use GetLocalDetailsAsync() + GetPublicIpAsync() separately for
        /// faster perceived UI load.
        /// </summary>
        public async Task<NetworkDetails> GetNetworkDetailsAsync(
            bool includePublicIp = true)
        {
            var details = await GetLocalDetailsAsync();

            if (includePublicIp)
                details.PublicIpAddress = await GetPublicIpAsync();

            return details;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ADAPTER HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static NetworkInterface? GetActiveWirelessAdapter() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

        private static NetworkInterface? GetAnyActiveAdapter() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

        private static string FormatMac(string raw) =>
            raw.Length == 12
                ? string.Join("-",
                    Enumerable.Range(0, 6)
                        .Select(i => raw.Substring(i * 2, 2)))
                : raw;
    }
}
