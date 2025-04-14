using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Management;

namespace PerformanceProfilerApp
{
    public static class ProfilerService
    {
        public static event Action<ProfilingDataStats> OnSampleReceived;

        private static ConcurrentQueue<ProfilingDataStats> DataBuffer = new ConcurrentQueue<ProfilingDataStats>();
        private static System.Timers.Timer _samplingTimer;
        private static bool _isRunning = false;
        private static bool _isSampling = false;
        private static int _bufferSize = 100;

        private static TimeSpan _lastCpuTime;
        private static DateTime _lastCpuCheckTime;
        private static ProfilingDataStats _staticSystemCpuInfo;
        private static string _selectedProcessName;

        public static void StartGroup(string processName)
        {
            _selectedProcessName = processName;
            _isRunning = true;

            var info = new ProfilingDataStats();
            PopulateSystemCpuInfo(ref info);
            _staticSystemCpuInfo = info;

            _samplingTimer = new System.Timers.Timer(1000);
            _samplingTimer.Elapsed += SampleGroupedPerformance;
            _samplingTimer.AutoReset = true;
            _samplingTimer.Start();
        }

        public static void Stop()
        {
            _isRunning = false;
            _samplingTimer?.Stop();
            _samplingTimer?.Dispose();
        }

        public static void SetSamplingInterval(int intervalMs)
        {
            if (_samplingTimer != null)
            {
                _samplingTimer.Interval = intervalMs;
            }
        }

        private static void SampleGroupedPerformance(object sender, ElapsedEventArgs e)
        {
            if (_isSampling || !_isRunning) return;
            _isSampling = true;

            try
            {
                var group = Process.GetProcessesByName(_selectedProcessName)
                    .Where(p => !p.HasExited).ToList();

                if (group.Count == 0) return;

                double cpuTime = 0;
                long memory = 0;
                long privateMem = 0;
                int threadCount = 0;
                float diskUsage = 0;
                float networkUsage = 0;

                foreach (var p in group)
                {
                    try
                    {
                        cpuTime += p.TotalProcessorTime.TotalMilliseconds;
                        memory += p.WorkingSet64;
                        privateMem += p.PrivateMemorySize64;
                        threadCount += p.Threads.Count;

                        diskUsage += PerformanceHelpers.GetDiskIO(p.ProcessName);
                        networkUsage += PerformanceHelpers.GetNetworkUsage(p.Id);
                    }
                    catch { }
                }

                var timestamp = DateTime.Now;
                var currentTime = DateTime.Now;

                if (_lastCpuCheckTime == default)
                {
                    _lastCpuTime = TimeSpan.FromMilliseconds(cpuTime);
                    _lastCpuCheckTime = currentTime;
                    return;
                }

                var cpuUsedMs = cpuTime - _lastCpuTime.TotalMilliseconds;
                var elapsedMs = (currentTime - _lastCpuCheckTime).TotalMilliseconds;
                float cpuUsage = (float)((cpuUsedMs / (elapsedMs * Environment.ProcessorCount)) * 100);

                _lastCpuTime = TimeSpan.FromMilliseconds(cpuTime);
                _lastCpuCheckTime = currentTime;

                var data = new ProfilingDataStats
                {
                    Timestamp = timestamp,
                    ProcessName = _selectedProcessName,
                    CpuUsage = cpuUsage,
                    MemoryUsage = memory / (1024f * 1024f),
                    PrivateMemoryUsage = privateMem / (1024f * 1024f),
                    DiskIO = diskUsage,
                    NetworkUsage = networkUsage,
                    ThreadCount = threadCount,
                    ProcessCount = Process.GetProcesses().Length,
                    GcGen0Collections = GC.CollectionCount(0),
                    GcGen1Collections = GC.CollectionCount(1),
                    GcGen2Collections = GC.CollectionCount(2),
                    LogicalProcessorCount = _staticSystemCpuInfo.LogicalProcessorCount,
                    PhysicalCoreCount = _staticSystemCpuInfo.PhysicalCoreCount,
                    ClockSpeedMHz = _staticSystemCpuInfo.ClockSpeedMHz,
                    VirtualizationStatus = _staticSystemCpuInfo.VirtualizationStatus,
                    L1CacheKB = _staticSystemCpuInfo.L1CacheKB,
                    L2CacheKB = _staticSystemCpuInfo.L2CacheKB,
                    L3CacheKB = _staticSystemCpuInfo.L3CacheKB
                };

                DataBuffer.Enqueue(data);
                if (DataBuffer.Count > _bufferSize)
                    DataBuffer.TryDequeue(out _);

                OnSampleReceived?.Invoke(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Group Sampling: " + ex.Message);
            }
            finally
            {
                _isSampling = false;
            }
        }

        private static void PopulateSystemCpuInfo(ref ProfilingDataStats data)
        {
            try
            {
                var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    data.PhysicalCoreCount = Convert.ToInt32(obj["NumberOfCores"]);
                    data.ClockSpeedMHz = Convert.ToInt32(obj["CurrentClockSpeed"]);
                    data.VirtualizationStatus = obj["VirtualizationFirmwareEnabled"]?.ToString() == "True" ? "Enabled" : "Disabled";
                }

                var cacheSearcher = new ManagementObjectSearcher("select * from Win32_CacheMemory");
                foreach (var obj in cacheSearcher.Get())
                {
                    ushort level = (ushort)obj["Level"];
                    int size = Convert.ToInt32(obj["InstalledSize"]);
                    switch (level)
                    {
                        case 1: data.L1CacheKB = size; break;
                        case 2: data.L2CacheKB = size; break;
                        case 3: data.L3CacheKB = size; break;
                    }
                }

                data.LogicalProcessorCount = Environment.ProcessorCount;
            }
            catch { }
        }
    }
}