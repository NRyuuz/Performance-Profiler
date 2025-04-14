using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace PerformanceProfilerApp
{
    public static class ExportService
    {
        public static void ExportProfilingSamplesToCsv(List<ProfilingDataStats> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                MessageBox.Show("No samples collected yet.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = "ProfilerData",
                DefaultExt = ".csv",
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName);
                writer.WriteLine("Timestamp,CPU (%),Memory (MB),Disk (MB/s),Network (KB/s),Threads,Processes");

                foreach (var sample in samples)
                {
                    writer.WriteLine(sample.ToCsv());
                }

                writer.Flush();
                MessageBox.Show("Performance data exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static void ExportSystemInfoToCsv(SystemInfoService.SystemInfo info)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "SystemInfo",
                DefaultExt = ".csv",
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName);
                writer.WriteLine("Category,Property,Value");

                void Write(string category, string prop, string val)
                    => writer.WriteLine($"{category},{prop},\"{val}\"");

                Write("CPU", "Model", info.CpuModel);
                Write("CPU", "Clock Speed", info.ClockSpeed);
                Write("CPU", "Cores", info.PhysicalCores);
                Write("CPU", "Logical Processors", info.LogicalCores);
                Write("Memory", "Total RAM", info.TotalRam);
                Write("OS", "Version", info.OsVersion);
                Write("OS", "Architecture", info.Architecture);
                Write("OS", "Virtualization", info.Virtualization);
                Write("OS", "Uptime", info.Uptime);
                Write("OS", "Extras", info.OsExtra);
                Write("GPU", "Name", info.GpuName);
                Write("Motherboard", "Info", info.Motherboard);

                foreach (var drive in info.Drives)
                    Write("Drive", "Detail", drive);

                writer.Flush();
                MessageBox.Show("System information exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
