using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet
{
    /// <summary>
    /// Datadog BenchmarkDotNet Exporter
    /// </summary>
    public class DatadogExporter : IExporter
    {
        /// <summary>
        /// Default DatadogExporter instance
        /// </summary>
        public static readonly IExporter Default = new DatadogExporter();

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(DatadogExporter));
        private static readonly bool _inContainer;

        private Tracer _tracer = null;

        static DatadogExporter()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);

            _inContainer = EnvironmentHelpers.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || ContainerMetadata.GetContainerId() != null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatadogExporter"/> class.
        /// </summary>
        public DatadogExporter()
        {
            _tracer = Tracer.Instance;
        }

        /// <inheritdoc />
        public string Name => nameof(DatadogExporter);

        /// <inheritdoc />
        public void ExportToLog(Summary summary, ILogger logger)
        {
        }

        /// <inheritdoc />
        public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
        {
            try
            {
                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                foreach (var report in summary.Reports)
                {
                    Span span = _tracer.StartSpan("benchmark.test", startTime: startTime);
                    span.SetMetric(Tags.Analytics, 1.0d);
                    span.SetTraceSamplingPriority(SamplingPriority.UserKeep);
                    span.Type = "test";
                    span.SetTag(TestTags.Name, report.BenchmarkCase.Descriptor.WorkloadMethod.Name);
                    span.SetTag(TestTags.Type, TestTags.TypeBenchmark);
                    span.SetTag(TestTags.Suite, report.BenchmarkCase.Descriptor.Type.FullName);
                    span.SetTag(TestTags.Framework, $"BenchmarkDotNet {summary.HostEnvironmentInfo.BenchmarkDotNetVersion}");

                    span.SetTag("test.description", report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo);
                    span.SetTag("benchmark.host.processor.name", summary.HostEnvironmentInfo.CpuInfo.Value.ProcessorName);
                    span.SetMetric("benchmark.host.processor.physical_processor_count", summary.HostEnvironmentInfo.CpuInfo.Value.PhysicalProcessorCount);
                    span.SetMetric("benchmark.host.processor.physical_core_count", summary.HostEnvironmentInfo.CpuInfo.Value.PhysicalCoreCount);
                    span.SetMetric("benchmark.host.processor.logical_core_count", summary.HostEnvironmentInfo.CpuInfo.Value.LogicalCoreCount);
                    span.SetMetric("benchmark.host.processor.max_frequency_hertz", summary.HostEnvironmentInfo.CpuInfo.Value.MaxFrequency?.Hertz);
                    span.SetMetric("benchmark.host.processor.min_frequency_hertz", summary.HostEnvironmentInfo.CpuInfo.Value.MinFrequency?.Hertz);
                    span.SetMetric("benchmark.host.processor.nominal_frequency_hertz", summary.HostEnvironmentInfo.CpuInfo.Value.NominalFrequency?.Hertz);
                    span.SetTag("benchmark.host.os.version", summary.HostEnvironmentInfo.OsVersion.Value);
                    span.SetTag("benchmark.host.dotnet.version", summary.HostEnvironmentInfo.RuntimeVersion);
                    span.SetMetric("benchmark.host.chronometer.frequency_hertz", summary.HostEnvironmentInfo.ChronometerFrequency.Hertz);
                    span.SetMetric("benchmark.host.chronometer.resolution", summary.HostEnvironmentInfo.ChronometerResolution.Nanoseconds);
                    span.SetTag("benchmark.host.hardware_timer_kind", summary.HostEnvironmentInfo.HardwareTimerKind.ToString());
                    span.SetTag(TestTags.Status, report.Success ? TestTags.StatusPass : TestTags.StatusFail);

                    if (report.BenchmarkCase.HasParameters)
                    {
                        span.SetTag("benchmark.params.count", report.BenchmarkCase.Parameters.ValueInfo);
                    }

                    if (report.ResultStatistics != null)
                    {
                        var stats = report.ResultStatistics;
                        span.SetMetric("benchmark.runs", stats.N);
                        span.SetMetric("benchmark.duration.max", stats.Max);
                        span.SetMetric("benchmark.duration.mean", stats.Mean);
                        span.SetMetric("benchmark.duration.median", stats.Median);
                        span.SetMetric("benchmark.duration.min", stats.Min);
                        span.SetMetric("benchmark.duration.q1", stats.Q1);
                        span.SetMetric("benchmark.duration.q3", stats.Q3);
                        span.SetMetric("benchmark.duration.kurtosis", stats.Kurtosis);
                        span.SetMetric("benchmark.duration.skewness", stats.Skewness);
                        span.SetMetric("benchmark.duration.std_dev", stats.StandardDeviation);
                        span.SetMetric("benchmark.duration.variance", stats.Variance);
                        span.SetMetric("benchmark.duration.std_err", stats.StandardError);
                        span.SetMetric("benchmark.duration.p00", stats.Percentiles.P0);
                        span.SetMetric("benchmark.duration.p25", stats.Percentiles.P25);
                        span.SetMetric("benchmark.duration.p50", stats.Percentiles.P50);
                        span.SetMetric("benchmark.duration.p67", stats.Percentiles.P67);
                        span.SetMetric("benchmark.duration.p80", stats.Percentiles.P80);
                        span.SetMetric("benchmark.duration.p85", stats.Percentiles.P85);
                        span.SetMetric("benchmark.duration.p90", stats.Percentiles.P90);
                        span.SetMetric("benchmark.duration.p95", stats.Percentiles.P95);
                        span.SetMetric("benchmark.duration.p99", stats.Percentiles.Percentile(99));
                        span.SetMetric("benchmark.duration.p100", stats.Percentiles.P100);

                        if (report.BenchmarkCase.Config.HasMemoryDiagnoser())
                        {
                            span.SetMetric("benchmark.memory.gen0", report.GcStats.Gen0Collections);
                            span.SetMetric("benchmark.memory.gen1", report.GcStats.Gen1Collections);
                            span.SetMetric("benchmark.memory.gen2", report.GcStats.Gen2Collections);
                            span.SetMetric("benchmark.memory.total_operations", report.GcStats.TotalOperations);
                            span.SetMetric("benchmark.memory.mean_bytes_allocations", report.GcStats.BytesAllocatedPerOperation);
                            span.SetMetric("benchmark.memory.total_bytes_allocations", report.GcStats.GetTotalAllocatedBytes(false));
                        }
                    }

                    span.Finish(startTime.Add(TimeSpan.FromSeconds((report.ResultStatistics?.N ?? 0) * (report.ResultStatistics?.Mean ?? 0) / 1_000_000_000)));
                }
            }
            catch (Exception ex)
            {
                consoleLogger.WriteLine(ex.ToString());
            }

            return Enumerable.Empty<string>();
        }
    }
}
