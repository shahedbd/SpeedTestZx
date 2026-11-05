using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TestNetSpeed.Helper
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

        /// <summary>The first up, non-virtual adapter with an IPv4 gateway. Never throws.</summary>
        public static Route Current()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                    var gateway = nic.GetIPProperties().GatewayAddresses
                        .Select(g => g.Address)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                             && !a.Equals(IPAddress.Any));
                    if (gateway == null) continue;

                    return new Route(TypeName(nic.NetworkInterfaceType), gateway);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Route lookup failed: {ex.Message}");
            }
            return None;
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
