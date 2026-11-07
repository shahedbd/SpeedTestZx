namespace SpeedTestZx.Model
{
    public class SystemInfo
    {
        public string Caption { get; set; }
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public string OSArchitecture { get; set; }
        public string SerialNumber { get; set; }
        public string RegisteredUser { get; set; }
        public string Organization { get; set; }
        public DateTime? InstallDate { get; set; }
        public DateTime? LastBootUpTime { get; set; }
        public string SystemDirectory { get; set; }
        public string WindowsDirectory { get; set; }
        public string Manufacturer { get; set; }
        public string CSName { get; set; }
    }
}
