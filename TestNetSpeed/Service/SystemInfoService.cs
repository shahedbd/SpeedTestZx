using System.Management;
using TestNetSpeed.Model;

namespace TestNetSpeed.Service
{
    /// <summary>
    /// Shared service backing the header bar's system summary — PC name,
    /// processor, and total RAM.
    /// </summary>
    public class SystemInfoService
    {
        public SystemInfo GetOperatingSystemInfo()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return new SystemInfo
                        {
                            Caption = GetStringValue(obj, "Caption"),
                            Version = GetStringValue(obj, "Version"),
                            BuildNumber = GetStringValue(obj, "BuildNumber"),
                            OSArchitecture = GetStringValue(obj, "OSArchitecture"),
                            SerialNumber = GetStringValue(obj, "SerialNumber"),
                            RegisteredUser = GetStringValue(obj, "RegisteredUser"),
                            Organization = GetStringValue(obj, "Organization"),
                            InstallDate = GetDateTimeValue(obj, "InstallDate"),
                            LastBootUpTime = GetDateTimeValue(obj, "LastBootUpTime"),
                            SystemDirectory = GetStringValue(obj, "SystemDirectory"),
                            WindowsDirectory = GetStringValue(obj, "WindowsDirectory"),
                            Manufacturer = GetStringValue(obj, "Manufacturer"),
                            CSName = GetStringValue(obj, "CSName")
                        };
                    }
                }
            }
            catch
            {
                // Consistent with GetProcessorName/GetTotalPhysicalMemory —
                // fail soft, let the caller decide how to present "unavailable".
            }

            return null;
        }

        public string GetProcessorName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                        return GetStringValue(obj, "Name");
                }
            }
            catch { }

            return "N/A";
        }

        public ulong GetTotalPhysicalMemory()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out var bytes))
                            return bytes;
                    }
                }
            }
            catch { }

            return 0;
        }

        private string GetStringValue(ManagementObject obj, string propertyName)
        {
            try
            {
                return obj[propertyName]?.ToString()?.Trim() ?? "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private DateTime? GetDateTimeValue(ManagementObject obj, string propertyName)
        {
            try
            {
                string value = obj[propertyName]?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return ManagementDateTimeConverter.ToDateTime(value);
                }
            }
            catch { }
            return null;
        }
    }
}
