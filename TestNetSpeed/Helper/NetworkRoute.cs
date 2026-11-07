using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SpeedTestZx.Helper
{
    /// <summary>
    /// The adapter that carries internet traffic right now: its type
    /// ("Ethernet" / "Wi-Fi", recorded on every test for the report's
    /// "wired tests only" option) and its default gateway (the router the
    /// Monitor probes to tell a router outage from an ISP one).
    /// </summary>
    public static class NetworkRoute
    {
        public readonly record struct Route(string AdapterType, IPAddress? Gateway);

        public static readonly Route None = new(string.Empty, null);

        /// <summary>
        /// The adapter Windows actually routes 1.1.1.1 through (GetBestInterface),
        /// so a Hyper-V / VMware / VPN adapter that also has a gateway is never
        /// mistaken for the internet connection. Falls back to the first up
        /// adapter with an IPv4 gateway. Never throws.
        /// </summary>
        public static Route Current()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                    .Select(n => (Nic: n, Gateway: GatewayOf(n)))
                    .Where(c => c.Gateway != null)
                    .ToList();

                if (BestInterfaceIndex() is int best)
                {
                    foreach (var c in candidates)
                        if (IndexOf(c.Nic) == best)
                            return new Route(TypeName(c.Nic.NetworkInterfaceType), c.Gateway);
                }

                if (candidates.Count > 0)
                    return new Route(TypeName(candidates[0].Nic.NetworkInterfaceType), candidates[0].Gateway);
            }
            catch (Exception ex)
            {
                Logger.Log($"Route lookup failed: {ex.Message}");
            }
            return None;
        }

        private static IPAddress? GatewayOf(NetworkInterface nic) =>
            nic.GetIPProperties().GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

        private static int? IndexOf(NetworkInterface nic)
        {
            try { return nic.GetIPProperties().GetIPv4Properties()?.Index; }
            catch { return null; }
        }

        [System.Runtime.InteropServices.DllImport("iphlpapi.dll")]
        private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);

        /// <summary>Interface index of the route to 1.1.1.1, or null.</summary>
        private static int? BestInterfaceIndex()
        {
            uint dest = BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes(), 0);
            return GetBestInterface(dest, out uint index) == 0 ? (int)index : null;
        }

        private static string TypeName(NetworkInterfaceType type) => type switch
        {
            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.GigabitEthernet => "Ethernet",
            _ => "Other",
        };
    }
}
