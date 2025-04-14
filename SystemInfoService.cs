using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Collections.Generic;

namespace PerformanceProfilerApp
{
    public class SystemInfoService
    {
        public class SystemInfo
        {
            public string CpuModel { get; set; }
            public string ClockSpeed { get; set; }
            public string PhysicalCores { get; set; }
            public string LogicalCores { get; set; }
            public string TotalRam { get; set; }
            public string OsVersion { get; set; }
            public string Architecture { get; set; }
            public string Virtualization { get; set; }
            public string Uptime { get; set; }
            public string GpuName { get; set; }
            public List<string> Drives { get; set; } = new();
            public string Motherboard { get; set; }
            public string OsExtra { get; set; }
        }

        public static SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo();

            try
            {
                var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (var item in searcher.Get())
                {
                    info.CpuModel = item["Name"].ToString();
                    info.ClockSpeed = item["MaxClockSpeed"] + " MHz";
                    info.PhysicalCores = item["NumberOfCores"].ToString();
                }

                info.LogicalCores = Environment.ProcessorCount.ToString();

                var os = Environment.OSVersion;
                info.OsVersion = os.VersionString;
                info.Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                var ramQuery = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                ulong totalRam = 0;
                foreach (var obj in ramQuery.Get())
                {
                    totalRam += Convert.ToUInt64(obj["Capacity"]);
                }
                info.TotalRam = Math.Round(totalRam / 1024f / 1024f / 1024f, 2) + " GB";

                var sys = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem").Get().Cast<ManagementObject>().FirstOrDefault();
                info.Virtualization = sys?["HypervisorPresent"]?.ToString() == "True" ? "Enabled" : "Disabled";

                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                info.Uptime = uptime.ToString(@"dd\.hh\:mm\:ss");


                var gpuSearcher = new ManagementObjectSearcher("select * from Win32_VideoController");
                foreach (var gpu in gpuSearcher.Get())
                {
                    info.GpuName = gpu["Name"].ToString();
                    break;
                }

                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        string type = drive.DriveType.ToString();
                        string format = drive.DriveFormat;
                        string size = Math.Round(drive.TotalSize / (1024f * 1024 * 1024), 2) + " GB";
                        info.Drives.Add($"{drive.Name} ({type}) - {format} - {size}");
                    }
                }

                var boardSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (var board in boardSearcher.Get())
                {
                    info.Motherboard = $"{board["Manufacturer"]} - {board["Product"]}";
                    break;
                }

                var osQuery = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (var result in osQuery.Get())
                {
                    var caption = result["Caption"]?.ToString()?.Trim();
                    var build = result["BuildNumber"];
                    var installDateRaw = result["InstallDate"]?.ToString();
                    var installDate = DateTime.MinValue;

                    if (!string.IsNullOrEmpty(installDateRaw) && installDateRaw.Length >= 8)
                    {
                        installDate = DateTime.ParseExact(installDateRaw.Substring(0, 8), "yyyyMMdd", null);
                    }

                    info.OsExtra = $"OS: {caption} (Build {build}), Installed: {installDate:yyyy-MM-dd}";
                }
            }
            catch (Exception ex)
            {
                info.CpuModel = "System info error: " + ex.Message;
            }

            return info;
        }
    }
}
