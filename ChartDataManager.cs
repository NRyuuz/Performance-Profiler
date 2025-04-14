using System.Collections.ObjectModel;

namespace PerformanceProfilerApp
{
    public class ChartDataManager
    {
        public ObservableCollection<double> CpuValues { get; } = new();
        public ObservableCollection<double> MemoryValues { get; } = new();
        public ObservableCollection<double> DiskValues { get; } = new();
        public ObservableCollection<double> NetworkValues { get; } = new();

        private const int MaxPoints = 60;

        public void Update(double cpu, double memory, double disk, double network)
        {
            UpdateSeries(CpuValues, cpu);
            UpdateSeries(MemoryValues, memory);
            UpdateSeries(DiskValues, disk);
            UpdateSeries(NetworkValues, network);
        }

        private void UpdateSeries(ObservableCollection<double> series, double value)
        {
            if (series.Count > MaxPoints)
                series.RemoveAt(0);
            series.Add(value);
        }
    }
}
