// <copyright file="DatadogExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Datadog.Trace.Ci;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.BenchmarkDotNet;

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
        var testSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "BenchmarkDotNet", startTime, false);
        var offsetTimeSpan = TimeSpan.Zero;

        Dictionary<Assembly, TestModuleWithDuration> testModules = new();
        Dictionary<Type, TestSuiteWithDuration> testSuites = new();
        Exception exception = null;

        try
        {
            var json = JsonConvert.SerializeObject(summary, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            File.WriteAllText("output.json", json);
            foreach (var report in summary.Reports)
            {
                startTime = startTime.Add(offsetTimeSpan);

                if (report?.BenchmarkCase?.Descriptor is { Type: { } type } descriptor && summary.HostEnvironmentInfo is { CpuInfo.Value: { } cpuInfo } hostEnvironmentInfo)
                {
                    if (!testModules.TryGetValue(type.Assembly, out var testModuleWithDuration))
                    {
                        testModuleWithDuration = new TestModuleWithDuration { Module = testSession.CreateModule(type.Assembly.GetName().Name, "BenchmarkDotNet", hostEnvironmentInfo.BenchmarkDotNetVersion, startTime), Duration = TimeSpan.Zero, };
                        testModules[type.Assembly] = testModuleWithDuration;
                    }

                    if (!testSuites.TryGetValue(type, out var testSuiteWithDuration))
                    {
                        testSuiteWithDuration = new TestSuiteWithDuration { Suite = testModuleWithDuration.Module.GetOrCreateSuite(type.FullName ?? "Suite", startTime), Duration = TimeSpan.Zero, };
                        testSuites[type] = testSuiteWithDuration;
                    }

                    var testMethod = testSuiteWithDuration.Suite.CreateTest(descriptor.WorkloadMethod.Name, startTime);
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
                        new BenchmarkJobInfo { Description = report.BenchmarkCase?.Job?.DisplayInfo, Platform = report.BenchmarkCase?.Job?.Environment?.Platform.ToString(), RuntimeName = report.BenchmarkCase?.Job?.Environment?.Runtime?.Name, RuntimeMoniker = report.BenchmarkCase?.Job?.Environment?.Runtime?.MsBuildMoniker });

                    double durationInNanoseconds = 0;
                    if (report.ResultStatistics is { } statistics)
                    {
                        durationInNanoseconds = statistics.Mean;

                        double p90 = 0, p95 = 0, p99 = 0;
                        if (statistics.Percentiles is { } percentiles)
                        {
                            p90 = percentiles.P90;
                            p95 = percentiles.P95;
                            p99 = percentiles.Percentile(99);
                        }

                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.Duration,
                            "Duration of the benchmark",
                            new BenchmarkDiscreteStats(
                                statistics.N,
                                statistics.Max,
                                statistics.Min,
                                statistics.Mean,
                                statistics.Median,
                                statistics.StandardDeviation,
                                statistics.StandardError,
                                statistics.Kurtosis,
                                statistics.Skewness,
                                p99,
                                p95,
                                p90));

                        testMethod.SetTag("benchmark.runs", statistics.N);
                    }

                    if (report.BenchmarkCase?.Config?.HasMemoryDiagnoser() == true)
                    {
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.GarbageCollectorGen0,
                            "Garbage collector Gen0 count",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.Gen0Collections }));
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.GarbageCollectorGen1,
                            "Garbage collector Gen1 count",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.Gen1Collections }));
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.GarbageCollectorGen2,
                            "Garbage collector Gen2 count",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.Gen2Collections }));
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.MemoryTotalOperations,
                            "Memory total operations count",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.TotalOperations }));
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.MeanHeapAllocations,
                            "Bytes allocated per operation",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) }));
                        testMethod.AddBenchmarkData(
                            BenchmarkMeasureType.TotalHeapAllocations,
                            "Total Bytes allocated",
                            BenchmarkDiscreteStats.GetFrom(new double[] { report.GcStats.GetTotalAllocatedBytes(true) }));
                    }

                    var duration = TimeSpan.FromTicks((long)(durationInNanoseconds / TimeConstants.NanoSecondsPerTick));

                    testMethod.Close(report.Success ? TestStatus.Pass : TestStatus.Fail, duration);
                    duration += TimeSpan.FromTicks(100);
                    testSuiteWithDuration.Duration += duration;
                    testModuleWithDuration.Duration += duration;
                    offsetTimeSpan += duration;
                }
            }

            foreach (var item in testSuites)
            {
                item.Value.Suite.Close(item.Value.Duration);
            }

            foreach (var item in testModules)
            {
                item.Value.Module.Close(item.Value.Duration);
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

        testSession.Close(exception is null ? TestStatus.Pass : TestStatus.Fail, offsetTimeSpan);

        if (exception is not null)
        {
            return new[] { "Datadog Exporter error: " + exception };
        }

        return new[] { "Datadog Exporter ran successfully." };
    }

    private class TestModuleWithDuration
    {
        public TestModule Module { get; set; }

        public TimeSpan Duration { get; set; }
    }

    private class TestSuiteWithDuration
    {
        public TestSuite Suite { get; set; }

        public TimeSpan Duration { get; set; }
    }
}
