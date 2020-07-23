using BenchmarkDotNet.Attributes;

namespace Datadog.Trace.BenchmarkDotNet
{
    /// <summary>
    /// Datadog BenchmarkDotNet exporter
    /// </summary>
    public class DatadogExporterAttribute : ExporterConfigBaseAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DatadogExporterAttribute"/> class.
        /// </summary>
        public DatadogExporterAttribute()
            : base(DatadogExporter.Default)
        {
        }
    }
}
