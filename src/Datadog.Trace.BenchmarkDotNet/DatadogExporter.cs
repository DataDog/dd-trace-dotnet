using System;
using System.Collections.Generic;
using System.Threading;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Datadog.Trace.Ci;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogExporter));

        static DatadogExporter()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
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
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            Exception exception = null;

            try
            {
                Tracer tracer = new Tracer();

                foreach (var report in summary.Reports)
                {
                    Span span = tracer.StartSpan("benchmarkdotnet.test", startTime: startTime);
                    double durationNanoseconds = 0;

                    span.SetMetric(Tags.Analytics, 1.0d);
                    span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
                    span.Type = SpanTypes.Test;
                    span.ResourceName = $"{report.BenchmarkCase.Descriptor.Type.FullName}.{report.BenchmarkCase.Descriptor.WorkloadMethod.Name}";
                    CIEnvironmentValues.DecorateSpan(span);

                    span.SetTag(TestTags.Name, report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo);
                    span.SetTag(TestTags.Type, TestTags.TypeBenchmark);
                    span.SetTag(TestTags.Suite, report.BenchmarkCase.Descriptor.Type.FullName);
                    span.SetTag(TestTags.Framework, $"BenchmarkDotNet {summary.HostEnvironmentInfo.BenchmarkDotNetVersion}");
                    span.SetTag(TestTags.Status, report.Success ? TestTags.StatusPass : TestTags.StatusFail);

                    if (summary.HostEnvironmentInfo != null)
                    {
                        span.SetTag("benchmark.host.processor.name", ProcessorBrandStringHelper.Prettify(summary.HostEnvironmentInfo.CpuInfo.Value));
                        span.SetMetric("benchmark.host.processor.physical_processor_count", summary.HostEnvironmentInfo.CpuInfo.Value.PhysicalProcessorCount);
                        span.SetMetric("benchmark.host.processor.physical_core_count", summary.HostEnvironmentInfo.CpuInfo.Value.PhysicalCoreCount);
                        span.SetMetric("benchmark.host.processor.logical_core_count", summary.HostEnvironmentInfo.CpuInfo.Value.LogicalCoreCount);
                        span.SetMetric("benchmark.host.processor.max_frequency_hertz", summary.HostEnvironmentInfo.CpuInfo.Value.MaxFrequency?.Hertz);
                        span.SetTag("benchmark.host.os_version", summary.HostEnvironmentInfo.OsVersion.Value);
                        span.SetTag("benchmark.host.runtime_version", summary.HostEnvironmentInfo.RuntimeVersion);
                        span.SetMetric("benchmark.host.chronometer.frequency_hertz", summary.HostEnvironmentInfo.ChronometerFrequency.Hertz);
                        span.SetMetric("benchmark.host.chronometer.resolution", summary.HostEnvironmentInfo.ChronometerResolution.Nanoseconds);
                    }

                    if (report.BenchmarkCase.Job != null)
                    {
                        var job = report.BenchmarkCase.Job;
                        span.SetTag("benchmark.job.description", job.DisplayInfo);

                        if (job.Environment != null)
                        {
                            var jobEnv = job.Environment;
                            span.SetTag("benchmark.job.environment.platform", jobEnv.Platform.ToString());

                            if (jobEnv.Runtime != null)
                            {
                                span.SetTag("benchmark.job.runtime.name", jobEnv.Runtime.Name);
                                span.SetTag("benchmark.job.runtime.moniker", jobEnv.Runtime.MsBuildMoniker);
                            }
                        }
                    }

                    if (report.ResultStatistics != null)
                    {
                        var stats = report.ResultStatistics;
                        span.SetMetric("benchmark.runs", stats.N);
                        span.SetMetric("benchmark.duration.mean", stats.Mean);

                        span.SetMetric("benchmark.statistics.n", stats.N);
                        span.SetMetric("benchmark.statistics.max", stats.Max);
                        span.SetMetric("benchmark.statistics.min", stats.Min);
                        span.SetMetric("benchmark.statistics.mean", stats.Mean);
                        span.SetMetric("benchmark.statistics.median", stats.Median);
                        span.SetMetric("benchmark.statistics.std_dev", stats.StandardDeviation);
                        span.SetMetric("benchmark.statistics.std_err", stats.StandardError);
                        span.SetMetric("benchmark.statistics.kurtosis", stats.Kurtosis);
                        span.SetMetric("benchmark.statistics.skewness", stats.Skewness);

                        if (stats.Percentiles != null)
                        {
                            span.SetMetric("benchmark.statistics.p90", stats.Percentiles.P90);
                            span.SetMetric("benchmark.statistics.p95", stats.Percentiles.P95);
                            span.SetMetric("benchmark.statistics.p99", stats.Percentiles.Percentile(99));
                        }

                        durationNanoseconds = stats.Mean;
                    }

                    if (report.Metrics != null)
                    {
                        foreach (var keyValue in report.Metrics)
                        {
                            if (keyValue.Value is null || keyValue.Value.Descriptor is null)
                            {
                                continue;
                            }

                            span.SetTag($"benchmark.metrics.{keyValue.Key}.displayName", keyValue.Value.Descriptor.DisplayName);
                            span.SetTag($"benchmark.metrics.{keyValue.Key}.legend", keyValue.Value.Descriptor.Legend);
                            span.SetTag($"benchmark.metrics.{keyValue.Key}.unit", keyValue.Value.Descriptor.Unit);
                            span.SetMetric($"benchmark.metrics.{keyValue.Key}.value", keyValue.Value.Value);
                        }
                    }

                    if (report.BenchmarkCase.Config?.HasMemoryDiagnoser() == true)
                    {
                        span.SetMetric("benchmark.memory.gen0Collections", report.GcStats.Gen0Collections);
                        span.SetMetric("benchmark.memory.gen1Collections", report.GcStats.Gen1Collections);
                        span.SetMetric("benchmark.memory.gen2Collections", report.GcStats.Gen2Collections);
                        span.SetMetric("benchmark.memory.total_operations", report.GcStats.TotalOperations);
                        span.SetMetric("benchmark.memory.mean_bytes_allocations", report.GcStats.BytesAllocatedPerOperation);
                        span.SetMetric("benchmark.memory.total_bytes_allocations", report.GcStats.GetTotalAllocatedBytes(false));
                    }

                    var duration = TimeSpan.FromTicks((long)(durationNanoseconds / TimeConstants.NanoSecondsPerTick));
                    span.Finish(startTime.Add(duration));
                }

                // Ensure all the spans gets flushed before we report the success.
                // In some cases the process finishes without sending the traces in the buffer.
                SynchronizationContext context = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    tracer.FlushAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                consoleLogger.WriteLine(LogKind.Error, ex.ToString());
            }

            if (exception is null)
            {
                return new string[] { "Datadog Exporter ran successfully." };
            }
            else
            {
                return new string[] { "Datadog Exporter error: " + exception.ToString() };
            }
        }
    }
}
