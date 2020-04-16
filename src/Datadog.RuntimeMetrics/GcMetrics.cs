namespace Datadog.RuntimeMetrics
{
    public struct GcMetrics
    {
        public long TotalAllocatedBytes;
        public long WorkingSetBytes;
        public long PrivateMemoryBytes;
        public int GcCountGen0;
        public int GcCountGen1;
        public int GcCountGen2;
        public double CpuTimeMs;
        public double CpuPercent;
    }
}
