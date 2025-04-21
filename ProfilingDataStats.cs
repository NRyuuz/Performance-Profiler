using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PerformanceProfilerApp
{
    public class ProfilingDataStats
    {
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public DateTime Timestamp { get; set; }
        public float CpuUsage { get; set; }
        public int LogicalProcessorCount { get; set; }
        public int PhysicalCoreCount { get; set; }
        public int ClockSpeedMHz { get; set; }
        public string VirtualizationStatus { get; set; }
        public int L1CacheKB { get; set; }
        public int L2CacheKB { get; set; }
        public int L3CacheKB { get; set; }

        public float MemoryUsage { get; set; }
        public int GcGen0Collections { get; set; }
        public int GcGen1Collections { get; set; }
        public int GcGen2Collections { get; set; }
        public float PrivateMemoryUsage { get; set; }

        public float GpuUsagePercent { get; set; }
        public float GpuTempC { get; set; }
        public float GpuClockMHz { get; set; }
        public float GpuMemoryMB { get; set; }


        public float DiskIO { get; set; }
        public float NetworkUsage { get; set; }
        public int ThreadCount { get; set; }
        public int ProcessCount { get; set; }

        /*public string ToCsv()
        {
            return $"{Timestamp},{CpuUsage},{MemoryUsage},{DiskIO},{NetworkUsage},{ThreadCount},{ProcessCount}";
        }*/

        public string ToCsv()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss},{CpuUsage},{MemoryUsage},{DiskIO},{NetworkUsage},{ThreadCount},{ProcessCount}";
        }

        /*public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }*/



        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Converters = { new DateTimeConverterUsingCustomFormat() }
            };
            return JsonSerializer.Serialize(this, options);
        }


        public static void SaveToCsv(ProfilingDataStats data, string csvFilePath)
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, true))
            {
                writer.WriteLine(data.ToCsv());
                writer.Flush();
            }
        }

        public static void SaveToJson(ProfilingDataStats data, string jsonFilePath)
        {
            using (StreamWriter writer = new StreamWriter(jsonFilePath, true))
            {
                writer.WriteLine(data.ToJson() + ",");
                writer.Flush();
            }
        }
    }



    public class DateTimeConverterUsingCustomFormat : JsonConverter<DateTime>
    {
        private readonly string _format = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), _format, null);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_format));
        }
    }

}
