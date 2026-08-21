namespace TestNetSpeed.Model;

public class NetworkDetails
{
    public string LocalIpAddress { get; set; } = string.Empty;
    public string PublicIpAddress { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string LinkSpeed { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string IPv6Address { get; set; } = string.Empty;
}
