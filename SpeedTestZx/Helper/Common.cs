using System.Diagnostics;

namespace SpeedTestZx.Helper
{
    public static class Common
    {
        public static Process PriorProcess()
        {
            Process curr = Process.GetCurrentProcess();
            Process[] procs = Process.GetProcessesByName(curr.ProcessName);
            foreach (Process p in procs)
            {
                if (p.Id == curr.Id) continue;

                try
                {
                    // Access to another process's MainModule can throw
                    // (Win32Exception) if it's elevated or owned by another
                    // user — that's not "another instance", just skip it
                    // rather than letting startup crash on the check itself.
                    if (p.MainModule.FileName == curr.MainModule.FileName)
                        return p;
                }
                catch
                {
                    // Ignore — treat as "can't confirm it's us", not a match.
                }
            }
            return null;
        }
    }
}
