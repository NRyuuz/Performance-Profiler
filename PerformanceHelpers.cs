using System;
using System.Diagnostics;
using System.Linq;

namespace PerformanceProfilerApp
{
    public static class PerformanceHelpers
    {
        private static PerformanceCounter _diskCounter;

        public static float GetDiskIO(string processName)
        {
            try
            {
                if (_diskCounter == null || _diskCounter.InstanceName != processName)
                {
                    _diskCounter = new PerformanceCounter("Process", "IO Data Bytes/sec", processName);
                }
                return _diskCounter.NextValue() / (1024 * 1024); // MB/s
            }
            catch
            {
                return 0;
            }
        }

        public static float GetNetworkUsage(int processId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var netstat = Process.Start(psi);
                var output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int connectionCount = lines.Count(line =>
                    line.StartsWith("  TCP", StringComparison.OrdinalIgnoreCase) &&
                    line.Trim().EndsWith(processId.ToString()));

                return connectionCount * 10f; // basic approximation
            }
            catch
            {
                return 0;
            }
        }
    }
}
