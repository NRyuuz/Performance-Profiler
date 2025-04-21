using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PerformanceProfilerApp
{
    public static class PerformanceHelpers
    {
        private static PerformanceCounter _diskCounter;

        public static float GetCpuUsage(List<Process> processes, TimeSpan lastCpuTime, DateTime lastCheckTime, out TimeSpan newCpuTime, out DateTime newCheckTime)
        {
            double cpuTime = 0;
            foreach (var p in processes)
            {
                try { cpuTime += p.TotalProcessorTime.TotalMilliseconds; } catch { }
            }

            var currentCpuTime = TimeSpan.FromMilliseconds(cpuTime);
            var currentTime = DateTime.Now;

            if (lastCheckTime == default)
            {
                newCpuTime = currentCpuTime;
                newCheckTime = currentTime;
                return 0;
            }

            var cpuUsedMs = (currentCpuTime - lastCpuTime).TotalMilliseconds;
            var elapsedMs = (currentTime - lastCheckTime).TotalMilliseconds;
            float usage = (float)((cpuUsedMs / (elapsedMs * Environment.ProcessorCount)) * 100);

            newCpuTime = currentCpuTime;
            newCheckTime = currentTime;
            return usage;
        }

        public static float GetMemoryUsage(List<Process> processes)
        {
            long memory = 0;
            foreach (var p in processes)
            {
                try { memory += p.WorkingSet64; } catch { }
            }
            return memory / (1024f * 1024f);
        }

        public static float GetPrivateMemoryUsage(List<Process> processes)
        {
            long memory = 0;
            foreach (var p in processes)
            {
                try { memory += p.PrivateMemorySize64; } catch { }
            }
            return memory / (1024f * 1024f);
        }

        public static int GetThreadCount(List<Process> processes)
        {
            int threads = 0;
            foreach (var p in processes)
            {
                try { threads += p.Threads.Count; } catch { }
            }
            return threads;
        }

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