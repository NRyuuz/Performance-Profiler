using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Management;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;

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

        public static bool EnableCpu = true;
        public static bool EnableMemory = true;
        public static bool EnableDisk = true;
        public static bool EnableNetwork = true;
        public static bool EnableGpu = false;
        public static bool EnableAdvanced = false;

        private static Computer _hardwareMonitor;
        private static IHardware _gpuHardware;
        private static float _lastGpuTemp = 0;
        private static float _lastGpuClock = 0;
        private static float _lastGpuMemory = 0;
        private static MainWindow _mainWindow;

        public static void StartGroup(string processName)
        {
            _selectedProcessName = processName;
            _isRunning = true;

            var info = new ProfilingDataStats();
            PopulateSystemCpuInfo(ref info);
            _staticSystemCpuInfo = info;

            if (EnableGpu)
            {
                _hardwareMonitor = new Computer { IsGpuEnabled = true };
                _hardwareMonitor.Open();
                _gpuHardware = _hardwareMonitor.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel);
            }

            _samplingTimer = new System.Timers.Timer(1000);
            _samplingTimer.Elapsed += SampleGroupedPerformance;
            _samplingTimer.AutoReset = true;
            _samplingTimer.Start();
        }

        public static void BindWindow(MainWindow window) => _mainWindow = window;

        public static void Stop()
        {
            _isRunning = false;
            _samplingTimer?.Stop();
            _samplingTimer?.Dispose();
        }

        public static void SetSamplingInterval(int intervalMs)
        {
            if (_samplingTimer != null)
                _samplingTimer.Interval = intervalMs;
        }

        private static void SampleGroupedPerformance(object sender, ElapsedEventArgs e)
        {
            if (_isSampling || !_isRunning) return;
            _isSampling = true;

            try
            {
                var group = Process.GetProcessesByName(_selectedProcessName).Where(p => !p.HasExited).ToList();
                if (group.Count == 0) return;

                float cpuUsage = 0, memory = 0, privateMem = 0, diskUsage = 0, networkUsage = 0;
                int threadCount = 0;
                float gpuUsage = -1;

                if (EnableCpu)
                {
                    cpuUsage = PerformanceHelpers.GetCpuUsage(group, _lastCpuTime, _lastCpuCheckTime, out _lastCpuTime, out _lastCpuCheckTime);
                }

                if (EnableMemory)
                {
                    memory = PerformanceHelpers.GetMemoryUsage(group);
                    privateMem = PerformanceHelpers.GetPrivateMemoryUsage(group);
                    threadCount = PerformanceHelpers.GetThreadCount(group);
                }

                if (EnableDisk)
                    diskUsage = PerformanceHelpers.GetDiskIO(_selectedProcessName);

                if (EnableNetwork)
                    networkUsage = group.Sum(p => PerformanceHelpers.GetNetworkUsage(p.Id));

                if (EnableGpu)
                    gpuUsage = GetGpuUsage();

                var timestamp = DateTime.Now;

                var data = new ProfilingDataStats
                {
                    Timestamp = timestamp,
                    ProcessName = _selectedProcessName,
                    CpuUsage = cpuUsage,
                    MemoryUsage = memory,
                    PrivateMemoryUsage = privateMem,
                    DiskIO = diskUsage,
                    NetworkUsage = networkUsage,
                    ThreadCount = threadCount,
                    GpuUsagePercent = gpuUsage,
                    GpuTempC = _lastGpuTemp,
                    GpuClockMHz = _lastGpuClock,
                    GpuMemoryMB = _lastGpuMemory,
                    ProcessCount = Process.GetProcesses().Length,
                    GcGen0Collections = EnableAdvanced ? GC.CollectionCount(0) : 0,
                    GcGen1Collections = EnableAdvanced ? GC.CollectionCount(1) : 0,
                    GcGen2Collections = EnableAdvanced ? GC.CollectionCount(2) : 0,
                    LogicalProcessorCount = EnableAdvanced ? _staticSystemCpuInfo.LogicalProcessorCount : 0,
                    PhysicalCoreCount = EnableAdvanced ? _staticSystemCpuInfo.PhysicalCoreCount : 0,
                    ClockSpeedMHz = EnableAdvanced ? _staticSystemCpuInfo.ClockSpeedMHz : 0,
                    VirtualizationStatus = EnableAdvanced ? _staticSystemCpuInfo.VirtualizationStatus : "",
                    L1CacheKB = EnableAdvanced ? _staticSystemCpuInfo.L1CacheKB : 0,
                    L2CacheKB = EnableAdvanced ? _staticSystemCpuInfo.L2CacheKB : 0,
                    L3CacheKB = EnableAdvanced ? _staticSystemCpuInfo.L3CacheKB : 0
                };

                DataBuffer.Enqueue(data);
                if (DataBuffer.Count > _bufferSize)
                    DataBuffer.TryDequeue(out _);

                OnSampleReceived?.Invoke(data);
            }
            catch (Exception ex)
            {
                _mainWindow?.LogMessage("[ERROR] Group Sampling: " + ex.Message);
            }
            finally
            {
                _isSampling = false;
            }
        }

        private static float GetGpuUsage()
        {
            float usage = 0;

            try
            {
                if (_gpuHardware != null)
                {
                    _gpuHardware.Update();
                    foreach (var sensor in _gpuHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                            usage = sensor.Value ?? 0;
                        else if (sensor.Name == "GPU Temperature")
                            _lastGpuTemp = sensor.Value ?? 0;
                        else if (sensor.Name == "GPU Core Clock")
                            _lastGpuClock = sensor.Value ?? 0;
                        else if (sensor.Name == "GPU Memory Used")
                            _lastGpuMemory = sensor.Value ?? 0;
                    }
                }
                else
                {
                    _mainWindow?.LogMessage("[GPU] No sensors available. Skipping GPU monitoring.");
                }
            }
            catch { usage = 0; }

            return usage;
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
