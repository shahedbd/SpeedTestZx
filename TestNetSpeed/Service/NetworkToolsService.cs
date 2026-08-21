using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using TestNetSpeed.Helper;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service
{
    public class NetworkToolsService
    {
        // ── Ping ──────────────────────────────────────────────────────────────
        public async Task<NetworkToolResult> PingAsync(
            string host, int count = 4,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!InputValidator.IsValidHost(host))
                return Fail("Invalid host. Please enter a valid domain or IP address.");

            var sb = new StringBuilder();
            sb.AppendLine($"Pinging {host} ({count} times)...\n");

            try
            {
                using var ping = new Ping();
                int success = 0;
                long totalMs = 0;
                long minMs = long.MaxValue;
                long maxMs = 0;

                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var reply = await ping.SendPingAsync(host, 4000);

                        if (reply.Status == IPStatus.Success)
                        {
                            var line = $"Reply from {reply.Address}: " +
                                       $"bytes=32  time={reply.RoundtripTime}ms  " +
                                       $"TTL={reply.Options?.Ttl}";
                            sb.AppendLine(line);
                            progress?.Report(line);

                            success++;
                            totalMs += reply.RoundtripTime;
                            minMs = Math.Min(minMs, reply.RoundtripTime);
                            maxMs = Math.Max(maxMs, reply.RoundtripTime);
                        }
                        else
                        {
                            var line = $"Request timed out.  Status: {reply.Status}";
                            sb.AppendLine(line);
                            progress?.Report(line);
                        }
                    }
                    catch (Exception ex)
                    {
                        var line = $"Error: {ex.Message}";
                        sb.AppendLine(line);
                        progress?.Report(line);
                    }

                    if (i < count - 1)
                        await Task.Delay(200, cancellationToken);
                }

                // ── Statistics ──────────────────────────────────────────────
                int lost = count - success;
                int lossPct = (int)Math.Round(lost * 100.0 / count);

                sb.AppendLine();
                sb.AppendLine($"Ping statistics for {host}:");
                sb.AppendLine($"  Packets: Sent={count}, Received={success}, " +
                              $"Lost={lost} ({lossPct}% loss)");

                if (success > 0)
                {
                    sb.AppendLine("Approximate round trip times:");
                    sb.AppendLine($"  Min={minMs}ms  Max={maxMs}ms  " +
                                  $"Avg={totalMs / success}ms");
                }

                return new NetworkToolResult
                {
                    Success = success > 0,
                    Output = sb.ToString()
                };
            }
            catch (OperationCanceledException)
            {
                sb.AppendLine("\n[Cancelled by user]");
                return new NetworkToolResult
                {
                    Success = false,
                    Output = sb.ToString(),
                    ErrorMessage = "Cancelled."
                };
            }
            catch (Exception ex)
            {
                return Fail($"Ping failed: {ex.Message}");
            }
        }

        // ── DNS Lookup ───────────────────────────────────────────────────────
        public async Task<NetworkToolResult> DnsLookupAsync(
            string domain,
            CancellationToken cancellationToken = default)
        {
            if (!InputValidator.IsValidHost(domain))
                return Fail("Invalid domain name.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var addresses = await Dns.GetHostAddressesAsync(domain, cancellationToken);

                var sb = new StringBuilder();
                sb.AppendLine($"DNS Lookup for: {domain}\n");

                if (!addresses.Any())
                {
                    sb.AppendLine("No records found.");
                }
                else
                {
                    foreach (var addr in addresses.OrderBy(a => a.AddressFamily))
                    {
                        var type = addr.AddressFamily == AddressFamily.InterNetworkV6
                            ? "IPv6" : "IPv4";
                        sb.AppendLine($"  {type,-6}: {addr}");
                    }
                }

                return new NetworkToolResult
                {
                    Success = true,
                    Output = sb.ToString()
                };
            }
            catch (OperationCanceledException)
            {
                return Fail("Cancelled.");
            }
            catch (Exception ex)
            {
                return Fail($"DNS lookup failed: {ex.Message}");
            }
        }

        // ── IP Lookup ────────────────────────────────────────────────────────
        public Task<NetworkToolResult> IpLookupAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(Fail("Please enter an IP address or domain."));

            var sb = new StringBuilder();
            sb.AppendLine($"IP Lookup: {input}\n");

            if (IPAddress.TryParse(input, out var ip))
            {
                sb.AppendLine($"Type:       {(ip.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4")}");
                sb.AppendLine($"Loopback:   {IPAddress.IsLoopback(ip)}");
                sb.AppendLine($"Multicast:  {ip.IsIPv6Multicast}");

                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    sb.AppendLine($"Private:    {IsPrivateIpv4(ip)}");
                    sb.AppendLine($"Class:      {GetIpv4Class(ip)}");
                }

                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    sb.AppendLine($"Link-Local: {ip.IsIPv6LinkLocal}");
                    sb.AppendLine($"Site-Local: {ip.IsIPv6SiteLocal}");
                }

                return Task.FromResult(new NetworkToolResult
                {
                    Success = true,
                    Output = sb.ToString()
                });
            }

            // Treat as domain → DNS lookup
            return DnsLookupAsync(input, cancellationToken);
        }

        // ── Traceroute ───────────────────────────────────────────────────────
        public async Task<NetworkToolResult> TraceRouteAsync(
            string host,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!InputValidator.IsValidHost(host))
                return Fail("Invalid host. Only domains and IP addresses are allowed.");

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

                var psi = new ProcessStartInfo
                {
                    FileName = "tracert",
                    Arguments = $"-d -h 30 {host}",   // -d skips DNS, -h 30 caps hops
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };
                var sb = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    sb.AppendLine(e.Data);
                    progress?.Report(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();

                await process.WaitForExitAsync(cts.Token);

                return new NetworkToolResult
                {
                    Success = true,
                    Output = sb.ToString()
                };
            }
            catch (OperationCanceledException)
            {
                return new NetworkToolResult
                {
                    Success = false,
                    Output = "[Traceroute cancelled]",
                    ErrorMessage = "Traceroute timed out after 60 seconds."
                };
            }
            catch (Exception ex)
            {
                return Fail($"Traceroute failed: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static NetworkToolResult Fail(string msg) =>
            new() { Success = false, ErrorMessage = msg, Output = msg };

        private static bool IsPrivateIpv4(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 ||
                   (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                   (b[0] == 192 && b[1] == 168);
        }

        private static string GetIpv4Class(IPAddress ip)
        {
            var first = ip.GetAddressBytes()[0];
            return first switch
            {
                < 128 => "Class A",
                < 192 => "Class B",
                < 224 => "Class C",
                < 240 => "Class D (Multicast)",
                _ => "Class E (Reserved)"
            };
        }
    }
}
