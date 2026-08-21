using System.Text.RegularExpressions;

namespace TestNetSpeed.Helper
{
    /// <summary>
    /// Validates host/IP input before it's handed to Ping, DNS lookup, or a
    /// shelled-out process (tracert) — rejects anything outside a safe
    /// hostname/IP character set, including whitespace, so no extra
    /// arguments can be smuggled into the child process's argument list.
    /// </summary>
    public static class InputValidator
    {
        // Allow: letters, digits, dots, hyphens, underscores, colons/brackets (IPv6).
        // Reject everything else, including spaces and shell metacharacters.
        private static readonly Regex SafeHostPattern =
            new(@"^[a-zA-Z0-9.\-_:\[\]]+$", RegexOptions.Compiled);

        public static bool IsValidHost(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.Length > 253) return false;
            return SafeHostPattern.IsMatch(input.Trim());
        }
    }
}
