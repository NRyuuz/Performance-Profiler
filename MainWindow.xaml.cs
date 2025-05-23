﻿using System.Diagnostics;
using System.Linq;
using System;
using System.Windows;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Controls;
using Microsoft.Win32;

namespace PerformanceProfilerApp
{
    public partial class MainWindow : Window
    {
        public ISeries[] CpuSeries { get; set; }
        public ISeries[] MemorySeries { get; set; }
        public ISeries[] DiskSeries { get; set; }
        public ISeries[] NetworkSeries { get; set; }    
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        private ChartDataManager chartData = new();
        private List<ProfilingDataStats> collectedSamples = new();
        private string selectedProcessName;

        private ObservableCollection<double> gpuValues = new();
        public ISeries[] GpuSeries { get; set; }
    

        public MainWindow()
        {
            InitializeComponent();
            LoadProcesses();
            InitializeCharts();
            DisplaySystemInfo();
            DataContext = this;
            ProfilerService.BindWindow(this);

        }

        private void InitializeCharts()
        {
            LogMessage("Intializing Charts.");
            CpuSeries = new ISeries[] { new LineSeries<double> { Values = chartData.CpuValues, Stroke = new SolidColorPaint(SKColors.LightSkyBlue, 2), GeometrySize = 0 } };
            MemorySeries = new ISeries[] { new LineSeries<double> { Values = chartData.MemoryValues, Stroke = new SolidColorPaint(SKColors.LightGreen, 2), GeometrySize = 0 } };
            DiskSeries = new ISeries[] { new LineSeries<double> { Values = chartData.DiskValues, Stroke = new SolidColorPaint(SKColors.Orange, 2), GeometrySize = 0 } };
            NetworkSeries = new ISeries[] { new LineSeries<double> { Values = chartData.NetworkValues, Stroke = new SolidColorPaint(SKColors.IndianRed, 2), GeometrySize = 0 } };
            GpuSeries = new ISeries[] { new LineSeries<double> {Values = gpuValues,Stroke = new SolidColorPaint(SKColors.MediumPurple, 2),GeometrySize = 0}};
            DataContext = this;

            XAxes = new Axis[] { new Axis { Labeler = value => $"{value}s", MinLimit = 0, MaxLimit = 60 } };
            YAxes = new Axis[] { new Axis { Labeler = value => $"{value}", MinLimit = 0 } };
        }

        private void DisplaySystemInfo()
        {
            var info = SystemInfoService.GetSystemInfo();
            CpuModel.Text = $"CPU: {info.CpuModel}";
            ClockSpeed.Text = $"Clock Speed: {info.ClockSpeed}";
            PhysicalCores.Text = $"Cores: {info.PhysicalCores}";
            LogicalCores.Text = $"Logical Processors: {info.LogicalCores}";
            OsVersion.Text = $"OS Version: {info.OsVersion}";
            Architecture.Text = $"Architecture: {info.Architecture}";
            Virtualization.Text = $"Virtualization: {info.Virtualization}";
            Uptime.Text = $"Uptime: {info.Uptime}";
            GpuName.Text = $"GPU: {info.GpuName}";
            DrivesList.ItemsSource = info.Drives;
            MotherboardInfo.Text = $"Motherboard: {info.Motherboard}";
            OsExtra.Text = info.OsExtra;

        }

        private void LoadProcesses()
        {
            LogMessage("Loading Processes.");
            var grouped = Process.GetProcesses()
                .Where(p => { try { return !p.HasExited && !string.IsNullOrWhiteSpace(p.ProcessName); } catch { return false; } })
                .GroupBy(p => p.ProcessName)
                .Select(g => g.OrderBy(p => p.Id).First())
                .OrderBy(p => p.ProcessName)
                .ToList();

            ProcessComboBox.ItemsSource = grouped;
            ProcessComboBox.DisplayMemberPath = "ProcessName";
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            selectedProcessName = (ProcessComboBox.SelectedItem as Process)?.ProcessName;
            if (selectedProcessName == null)
            {
                MessageBox.Show("Please select a process.");
                return;
            }
            ProfilerService.EnableCpu = EnableCpuBox.IsChecked == true;
            ProfilerService.EnableMemory = EnableMemoryBox.IsChecked == true;
            ProfilerService.EnableDisk = EnableDiskBox.IsChecked == true;
            ProfilerService.EnableNetwork = EnableNetworkBox.IsChecked == true;
            ProfilerService.EnableGpu = EnableGpuBox.IsChecked == true;

            ProfilerService.OnSampleReceived += ProfilerService_OnSampleReceived;
            ProfilerService.StartGroup(selectedProcessName);
            collectedSamples.Clear();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            LogMessage("Starting Profiler.");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            ProfilerService.Stop();
            ProfilerService.OnSampleReceived -= ProfilerService_OnSampleReceived;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            LogMessage("Stopping Profiler.");
        }

        private void SamplingRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SamplingRateComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string content = selectedItem.Content.ToString();
                if (content.Contains("500")) ProfilerService.SetSamplingInterval(500);
                else if (content.Contains("1000")) ProfilerService.SetSamplingInterval(1000);
                else if (content.Contains("2000")) ProfilerService.SetSamplingInterval(2000);
            }
        }

        public void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                OutputLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                OutputLog.ScrollToEnd();
            });
        }

        private void ProfilerService_OnSampleReceived(ProfilingDataStats data)
        {
            Dispatcher.Invoke(() =>
            {
                collectedSamples.Add(data);
                chartData.Update(data.CpuUsage, data.MemoryUsage, data.DiskIO, data.NetworkUsage);

                CpuValue.Text = $"{data.CpuUsage:F2} %";
                MemoryValue.Text = $"{data.MemoryUsage:F2} MB";
                DiskValue.Text = $"{data.DiskIO:F2} MB/s";
                NetValue.Text = $"{data.NetworkUsage:F2} KB/s";

                PrivateMemoryValue.Text = $"Private Memory: {data.PrivateMemoryUsage:F2} MB";
                GcGen0Value.Text = $"GC Gen0 Collections: {data.GcGen0Collections}";
                GcGen1Value.Text = $"GC Gen1 Collections: {data.GcGen1Collections}";
                GcGen2Value.Text = $"GC Gen2 Collections: {data.GcGen2Collections}";
                ThreadCountValue.Text = $"Threads: {data.ThreadCount}";
                ProcessCountValue.Text = $"Running Processes: {data.ProcessCount}";

                GpuValue.Text = $"GPU: {data.GpuUsagePercent:F2} %";
                GpuTempValue.Text = $"Temp: {data.GpuTempC:F0} °C";
                GpuClockValue.Text = $"Clock: {data.GpuClockMHz:F0} MHz";
                GpuMemoryUsedValue.Text = $"Memory: {data.GpuMemoryMB:F0} MB";
                
                UpdateSeries(gpuValues, data.GpuUsagePercent);

                VirtualizationValue.Text = $"Virtualization: {data.VirtualizationStatus}";
                CacheL1Value.Text = $"L1 Cache: {data.L1CacheKB} KB";
                CacheL2Value.Text = $"L2 Cache: {data.L2CacheKB} KB";
                CacheL3Value.Text = $"L3 Cache: {data.L3CacheKB} KB";
            });
        }

        private void ExportSamples_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Exported Samples.");
            ExportService.ExportProfilingSamplesToCsv(collectedSamples);
        }

        private void AdvancedToggle_Checked(object sender, RoutedEventArgs e)
        {
            LogMessage("Advanced Mode Enabled.");
            MemoryAdvancedPanel.Visibility = Visibility.Visible;
            CpuAdvancedPanel.Visibility = Visibility.Visible;
            GpuAdvancedPanel.Visibility = Visibility.Visible;

        }

        private void AdvancedToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            LogMessage("Advanced Mode Disabled.");
            MemoryAdvancedPanel.Visibility = Visibility.Collapsed;
            CpuAdvancedPanel.Visibility = Visibility.Collapsed;
            GpuAdvancedPanel.Visibility = Visibility.Collapsed;

        }

        private void UpdateSeries(ObservableCollection<double> series, double value)
        {
            if (series.Count > 60) series.RemoveAt(0);
            series.Add(value);
        }

    }
}