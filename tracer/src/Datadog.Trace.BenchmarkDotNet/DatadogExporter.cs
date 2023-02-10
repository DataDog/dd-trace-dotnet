// <copyright file="DatadogExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog BenchmarkDotNet Exporter
/// </summary>
internal class DatadogExporter : IExporter
{
    /// <summary>
    /// Default DatadogExporter instance
    /// </summary>
    public static readonly DatadogExporter Default = new();

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

    private DatadogExporter()
    {
        TestSession = TestSession.GetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, "BenchmarkDotNet", DateTime.UtcNow, false);
        var version = typeof(IDiagnoser).Assembly?.GetName().Version?.ToString() ?? "unknown";
        TestModule = TestSession.CreateModule(
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Session",
            "BenchmarkDotNet",
            version);
        LifetimeManager.Instance.AddAsyncShutdownTask(
            async () =>
            {
                await TestModule.CloseAsync().ConfigureAwait(false);
                TestSession.Tags.CommandExitCode = Environment.ExitCode.ToString();
                await TestSession.CloseAsync(Environment.ExitCode == 0 ? TestStatus.Pass : TestStatus.Fail).ConfigureAwait(false);
            });
    }

    /// <inheritdoc />
    public string Name => nameof(DatadogExporter);

    internal TestSession TestSession { get; }

    internal TestModule TestModule { get; }

    /// <inheritdoc />
    public void ExportToLog(Summary summary, ILogger logger)
    {
    }

    /// <inheritdoc />
    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var testSession = TestSession;
        Dictionary<Type, TestSuiteWithEndDate> testSuites = new();
        Exception? exception = null;

        try
        {
            foreach (var report in summary.Reports)
            {
                var benchmarkStartDate = DateTime.MinValue;
                var benchmarkEndDate = DateTime.MinValue;

                if (report?.BenchmarkCase?.Descriptor is { Type: { } type } descriptor && summary.HostEnvironmentInfo is { CpuInfo.Value: { } cpuInfo } hostEnvironmentInfo)
                {
                    if (report.Metrics is { } metrics)
                    {
                        foreach (var metric in metrics)
                        {
                            if (metric.Key == "StartDate")
                            {
                                benchmarkStartDate = new DateTime((long)metric.Value.Value, DateTimeKind.Utc);
                            }
                            else if (metric.Key == "EndDate")
                            {
                                benchmarkEndDate = new DateTime((long)metric.Value.Value, DateTimeKind.Utc);
                            }
                        }
                    }

                    if (!testSuites.TryGetValue(type, out var testSuiteWithEndDate))
                    {
                        testSuiteWithEndDate = new TestSuiteWithEndDate(TestModule.GetOrCreateSuite(type.FullName ?? "Suite", benchmarkStartDate), benchmarkEndDate);
                        testSuites[type] = testSuiteWithEndDate;
                    }

                    var testName = descriptor.WorkloadMethod.Name;
                    if (report.BenchmarkCase.HasParameters)
                    {
                        testName += report.BenchmarkCase.Parameters.DisplayInfo;
                    }

                    var testMethod = testSuiteWithEndDate.Suite.CreateTest(testName, benchmarkStartDate);
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
                        new BenchmarkJobInfo { Description = report.BenchmarkCase.Job?.DisplayInfo, Platform = report.BenchmarkCase.Job?.Environment?.Platform.ToString(), RuntimeName = report.BenchmarkCase.Job?.Environment?.Runtime?.Name, RuntimeMoniker = report.BenchmarkCase.Job?.Environment?.Runtime?.MsBuildMoniker });

                    if (report.BenchmarkCase.HasParameters)
                    {
                        var testParameters = new TestParameters
                        {
                            Arguments = new Dictionary<string, object>(),
                            Metadata = new Dictionary<string, object>()
                        };
                        foreach (var parameter in report.BenchmarkCase.Parameters.Items)
                        {
                            var parameterValue = ClrProfiler.AutoInstrumentation.Testing.Common.GetParametersValueData(parameter.Value);
                            if (testParameters.Arguments.TryGetValue(parameter.Name, out var currentValue))
                            {
                                testParameters.Arguments[parameter.Name] += $"{currentValue}, {parameterValue}";
                            }
                            else
                            {
                                testParameters.Arguments[parameter.Name] = parameterValue;
                            }
                        }

                        testParameters.Metadata[TestTags.MetadataTestName] = report.BenchmarkCase.DisplayInfo ?? string.Empty;
                        testMethod.SetParameters(testParameters);
                    }

                    if (report.ResultStatistics is { } statistics)
                    {
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

                    testMethod.Close(report.Success ? TestStatus.Pass : TestStatus.Fail, benchmarkEndDate - benchmarkStartDate);
                    if (testSuiteWithEndDate.EndDate < benchmarkEndDate)
                    {
                        testSuiteWithEndDate.EndDate = benchmarkEndDate;
                    }
                }
            }

            foreach (var item in testSuites)
            {
                item.Value.Suite.Close(item.Value.EndDate - item.Value.Suite.StartTime);
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

        if (exception is not null)
        {
            return new[] { "Datadog Exporter error: " + exception };
        }

        return new[] { "Datadog Exporter ran successfully." };
    }

    private class TestSuiteWithEndDate
    {
        public TestSuiteWithEndDate(TestSuite suite, DateTime endDate)
        {
            Suite = suite;
            EndDate = endDate;
        }

        public TestSuite Suite { get; }

        public DateTime EndDate { get; set; }
    }
}
