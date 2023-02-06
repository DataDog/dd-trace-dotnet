// <copyright file="DatadogExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.BenchmarkDotNet
{
    /// <summary>
    /// Datadog BenchmarkDotNet Exporter
    /// </summary>
    public class DatadogExporter : IExporter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogExporter));

        /// <summary>
        /// Default DatadogExporter instance
        /// </summary>
        public static readonly IExporter Default = new DatadogExporter();

        static DatadogExporter()
        {
            try
            {
                Environment.SetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.Enabled, "1", EnvironmentVariableTarget.Process);
            }
            catch
            {
                // .
            }
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
            var startTime = DateTimeOffset.UtcNow;

            CIVisibility.Initialize();
            var testSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "BenchmarkDotNet", startTime, false);

            Dictionary<Assembly, Tuple<TestModule, double>> testModules = new();
            Dictionary<Type, Tuple<TestSuite, double>> testSuites = new();
            double maxDurationInNanoseconds = 0;
            Exception exception = null;

            try
            {
                /*
                var json = JsonConvert.SerializeObject(summary, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                File.WriteAllText("c:\\temp\\output.json", json);
                Tracer tracer = Tracer.Instance;
                */
                foreach (var report in summary.Reports)
                {
                    if (report?.BenchmarkCase?.Descriptor is { Type: { } type } descriptor && summary.HostEnvironmentInfo is { CpuInfo.Value: { } cpuInfo } hostEnvironmentInfo)
                    {
                        TestModule testModule;
                        if (!testModules.TryGetValue(type.Assembly, out var testModulePair))
                        {
                            testModule = testSession.CreateModule(type.Assembly.GetName().Name, "BenchmarkDotNet", hostEnvironmentInfo.BenchmarkDotNetVersion, startTime);
                            testModules[type.Assembly] = Tuple.Create(testModule, 0d);
                        }
                        else
                        {
                            testModule = testModulePair.Item1;
                        }

                        var testSuite = testModule.GetOrCreateSuite(type.FullName ?? "Suite", startTime);
                        testSuites[type] = Tuple.Create(testSuite, 0d);
                        var testMethod = testSuite.CreateTest(descriptor.WorkloadMethod.Name, startTime);
                        testMethod.SetTestMethodInfo(descriptor.WorkloadMethod);

                        testMethod.SetBenchmarkMetadata(
                            new BenchmarkHostInfo
                            {
                                ProcessorName = ProcessorBrandStringHelper.Prettify(cpuInfo),
                                ProcessorCount = cpuInfo.PhysicalProcessorCount,
                                PhysicalCoreCount = cpuInfo.PhysicalCoreCount,
                                LogicalCoreCount = cpuInfo.LogicalCoreCount,
                                ProcessorMaxFrequencyHertz = cpuInfo.MaxFrequency?.Hertz,
                                OsVersion = hostEnvironmentInfo.OsVersion?.Value,
                                RuntimeVersion = hostEnvironmentInfo.RuntimeVersion,
                                ChronometerFrequencyHertz = hostEnvironmentInfo.ChronometerFrequency.Hertz,
                                ChronometerResolution = hostEnvironmentInfo.ChronometerResolution.Nanoseconds
                            },
                            new BenchmarkJobInfo
                            {
                                Description = report.BenchmarkCase?.Job?.DisplayInfo,
                                Platform = report.BenchmarkCase?.Job?.Environment?.Platform.ToString(),
                                RuntimeName = report.BenchmarkCase?.Job?.Environment?.Runtime?.Name,
                                RuntimeMoniker = report.BenchmarkCase?.Job?.Environment?.Runtime?.MsBuildMoniker
                            });

                        double durationInNanoseconds = 0;
                        if (report.ResultStatistics is { } statistics)
                        {
                            durationInNanoseconds = statistics.Mean;
                        }

                        if (!testSuites.TryGetValue(type, out var suiteTuple) || suiteTuple.Item2 < durationInNanoseconds)
                        {
                            testSuites[type] = Tuple.Create(suiteTuple.Item1, durationInNanoseconds);
                        }

                        if (!testModules.TryGetValue(type.Assembly, out var moduleTuple) || moduleTuple.Item2 < durationInNanoseconds)
                        {
                            testModules[type.Assembly] = Tuple.Create(moduleTuple.Item1, durationInNanoseconds);
                        }

                        if (maxDurationInNanoseconds < durationInNanoseconds)
                        {
                            maxDurationInNanoseconds = durationInNanoseconds;
                        }

                        testMethod.Close(report.Success ? TestStatus.Pass : TestStatus.Fail, TimeSpan.FromTicks((long)(durationInNanoseconds / TimeConstants.NanoSecondsPerTick)));
                    }

                    /*
                    Span span = tracer.StartSpan("benchmarkdotnet.test", startTime: startTime);
                    double durationNanoseconds = 0;

                    span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
                    span.Type = SpanTypes.Test;
                    span.ResourceName = $"{report.BenchmarkCase.Descriptor.Type.FullName}.{report.BenchmarkCase.Descriptor.WorkloadMethod.Name}";
                    CIEnvironmentValues.Instance.DecorateSpan(span);

                    span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
                    span.SetTag(TestTags.Name, report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo);
                    span.SetTag(TestTags.Type, TestTags.TypeBenchmark);
                    span.SetTag(TestTags.Suite, report.BenchmarkCase.Descriptor.Type.FullName);
                    span.SetTag(TestTags.Bundle, report.BenchmarkCase.Descriptor.Type.Assembly?.GetName().Name);
                    span.SetTag(TestTags.Framework, $"BenchmarkDotNet {summary.HostEnvironmentInfo.BenchmarkDotNetVersion}");
                    span.SetTag(TestTags.Status, report.Success ? TestTags.StatusPass : TestTags.StatusFail);
                    span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);

                    if (summary.HostEnvironmentInfo is { } hostEnvironmentInfo2)
                    {
                        if (hostEnvironmentInfo2.CpuInfo?.Value is { } cpuInfo2)
                        {
                            span.SetTag(BenchmarkTestTags.HostProcessorName, ProcessorBrandStringHelper.Prettify(cpuInfo2));
                            span.SetMetric(BenchmarkTestTags.HostProcessorPhysicalProcessorCount, cpuInfo2.PhysicalProcessorCount);
                            span.SetMetric(BenchmarkTestTags.HostProcessorPhysicalCoreCount, cpuInfo2.PhysicalCoreCount);
                            span.SetMetric(BenchmarkTestTags.HostProcessorLogicalCoreCount, cpuInfo2.LogicalCoreCount);
                            span.SetMetric(BenchmarkTestTags.HostProcessorMaxFrequencyHertz, cpuInfo2.MaxFrequency?.Hertz);
                        }

                        if (hostEnvironmentInfo2.OsVersion?.Value is { } osVersion)
                        {
                            span.SetTag(BenchmarkTestTags.HostOsVersion, osVersion);
                        }

                        if (hostEnvironmentInfo2.RuntimeVersion is { } runtimeVersion)
                        {
                            span.SetTag(BenchmarkTestTags.HostRuntimeVersion, runtimeVersion);
                        }

                        span.SetMetric(BenchmarkTestTags.HostChronometerFrequencyHertz, hostEnvironmentInfo2.ChronometerFrequency.Hertz);
                        span.SetMetric(BenchmarkTestTags.HostChronometerResolution, hostEnvironmentInfo2.ChronometerResolution.Nanoseconds);
                    }

                    if (report.BenchmarkCase.Job is { } job)
                    {
                        span.SetTag(BenchmarkTestTags.JobDescription, job.DisplayInfo);

                        if (job.Environment is { } jobEnvironment)
                        {
                            span.SetTag(BenchmarkTestTags.JobPlatform, jobEnvironment.Platform.ToString());

                            if (jobEnvironment.Runtime is { } jobEnvironmentRuntime)
                            {
                                span.SetTag(BenchmarkTestTags.JobRuntimeName, jobEnvironmentRuntime.Name);
                                span.SetTag(BenchmarkTestTags.JobRuntimeMoniker, jobEnvironmentRuntime.MsBuildMoniker);
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
                CIVisibility.Flush();
                */
                }

                foreach (var item in testSuites)
                {
                    item.Value.Item1.Close(TimeSpan.FromTicks((long)(item.Value.Item2 / TimeConstants.NanoSecondsPerTick)));
                }

                foreach (var item in testModules)
                {
                    item.Value.Item1.Close(TimeSpan.FromTicks((long)(item.Value.Item2 / TimeConstants.NanoSecondsPerTick)));
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                consoleLogger.WriteLine(LogKind.Error, ex.ToString());
            }

            if (exception is not null)
            {
                testSession.SetErrorInfo(exception);
            }

            testSession.Close(exception is null ? TestStatus.Pass : TestStatus.Fail, TimeSpan.FromTicks((long)(maxDurationInNanoseconds / TimeConstants.NanoSecondsPerTick)));

            if (exception is not null)
            {
                return new[] { "Datadog Exporter error: " + exception };
            }

            return new[] { "Datadog Exporter ran successfully." };
        }
    }
}
