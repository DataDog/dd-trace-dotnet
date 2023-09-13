// <copyright file="DatadogExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    private readonly Dictionary<Assembly, TestModule> _testModules = new();

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
        TestSession = TestSession.InternalGetOrCreate(Environment.CommandLine, Environment.CurrentDirectory, CommonTags.TestingFrameworkNameBenchmarkDotNet, DateTime.UtcNow, false);
    }

    /// <inheritdoc />
    public string Name => nameof(DatadogExporter);

    internal TestSession TestSession { get; }

    /// <inheritdoc />
    public void ExportToLog(Summary summary, ILogger logger)
    {
    }

    /// <inheritdoc />
    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var version = typeof(IDiagnoser).Assembly?.GetName().Version?.ToString() ?? "unknown";
        Exception? exception = null;
        if (summary.HostEnvironmentInfo is { CpuInfo.Value: { } cpuInfo } hostEnvironmentInfo)
        {
            var groupedReports = summary.Reports
                                        .Where(r => r?.BenchmarkCase?.Descriptor?.Type is not null)
                                        .GroupBy(i => i.BenchmarkCase.Descriptor.Type)
                                        .GroupBy(t => t.Key.Assembly);

            foreach (var benchmarkModule in groupedReports)
            {
                // This method is called multiple times, so it's possible that the test module has been already created.
                if (!_testModules.TryGetValue(benchmarkModule.Key, out var testModule))
                {
                    BenchmarkMetadata.GetTimes(benchmarkModule.Key, out var moduleStartTime, out var moduleEndTime);
                    var moduleName = benchmarkModule.Key.GetName().Name ?? "Module";
                    var framework = CommonTags.TestingFrameworkNameBenchmarkDotNet;
                    testModule = moduleStartTime is null ?
                                     TestSession.InternalCreateModule(moduleName, framework, version) :
                                     TestSession.InternalCreateModule(moduleName, framework, version, startDate: moduleStartTime.Value);
                    _testModules[benchmarkModule.Key] = testModule;
                }

                foreach (var benchmarkSuite in benchmarkModule)
                {
                    BenchmarkMetadata.GetTimes(benchmarkSuite.Key, out var suiteStartTime, out var suiteEndTime);
                    var testSuite = testModule.InternalGetOrCreateSuite(benchmarkSuite.Key.FullName ?? "Suite", suiteStartTime);

                    foreach (var benchmarkTest in benchmarkSuite)
                    {
                        try
                        {
                            var benchmarkCase = benchmarkTest.BenchmarkCase;
                            BenchmarkMetadata.GetTimes(benchmarkCase, out var testStartTime, out var testEndTime);
                            var descriptor = benchmarkCase.Descriptor;

                            var testName = descriptor.WorkloadMethod.Name;
                            if (benchmarkCase.HasParameters)
                            {
                                testName += benchmarkCase.Parameters.DisplayInfo;
                            }

                            // The Job Id can contain random values: https://github.com/dotnet/BenchmarkDotNet/blob/ec429af22e3c03aedb4c1b813287ef330aed50a2/src/BenchmarkDotNet/Jobs/JobIdGenerator.cs#L18
                            // Example:
                            //      Job-WISXTD(Runtime=.NET Framework 4.7.2, Toolchain=net472, IterationTime=2.0000 s)
                            // We use the description as part of the configuration facets.
                            // The configuration facets are used by CI Visibility to create a fingerprint of the test.
                            // Random values here means a new test fingerprint every time, so there's no way to track the same
                            // test in multiple executions. So because of that, we need to remove the random part of the description.
                            // In this case we remove the job name and keep the information between the parethesis.
                            var description = benchmarkCase.Job?.DisplayInfo;
                            if (description is not null)
                            {
                                var matchCollections = new Regex(@"\(([^\(\)]+)\)").Matches(description);
                                if (matchCollections.Count > 0)
                                {
                                    // In the example: Runtime=.NET Framework 4.7.2, Toolchain=net472, IterationTime=2.0000 s
                                    description = string.Join(", ", matchCollections.OfType<Match>().Select(x => x.Groups[1].Value));
                                }
                                else if (description?.StartsWith("Job-") == true)
                                {
                                    // If we cannot extract the description but we know there's a random Id, we prefer to skip the description value.
                                    description = null;
                                }
                            }

                            BenchmarkMetadata.GetIds(benchmarkCase, out var traceId, out var spanId);

                            var test = testSuite.InternalCreateTest(testName, testStartTime, traceId, spanId);
                            test.SetTestMethodInfo(descriptor.WorkloadMethod);
                            test.SetBenchmarkMetadata(
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
                                new BenchmarkJobInfo { Description = description, Platform = benchmarkCase.Job?.Environment?.Platform.ToString(), RuntimeName = benchmarkCase.Job?.Environment?.Runtime?.Name, RuntimeMoniker = benchmarkCase.Job?.Environment?.Runtime?.MsBuildMoniker });

                            if (benchmarkCase.HasParameters)
                            {
                                var testParameters = new TestParameters { Arguments = new Dictionary<string, object>(), Metadata = new Dictionary<string, object>() };
                                foreach (var parameter in benchmarkCase.Parameters.Items)
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

                                testParameters.Metadata[TestTags.MetadataTestName] = benchmarkCase.DisplayInfo ?? string.Empty;
                                test.SetParameters(testParameters);
                            }

                            if (benchmarkTest.ResultStatistics is { } statistics)
                            {
                                double p90 = 0, p95 = 0, p99 = 0;
                                if (statistics.Percentiles is { } percentiles)
                                {
                                    p90 = percentiles.P90;
                                    p95 = percentiles.P95;
                                    p99 = percentiles.Percentile(99);
                                }

                                test.AddBenchmarkData(
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

                                test.SetTag("benchmark.runs", statistics.N);
                            }

                            if (benchmarkCase?.Config?.HasMemoryDiagnoser() == true)
                            {
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.GarbageCollectorGen0,
                                    "Garbage collector Gen0 count",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.Gen0Collections }));
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.GarbageCollectorGen1,
                                    "Garbage collector Gen1 count",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.Gen1Collections }));
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.GarbageCollectorGen2,
                                    "Garbage collector Gen2 count",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.Gen2Collections }));
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.MemoryTotalOperations,
                                    "Memory total operations count",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.TotalOperations }));
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.MeanHeapAllocations,
                                    "Bytes allocated per operation",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.GetBytesAllocatedPerOperation(benchmarkCase) }));
                                test.AddBenchmarkData(
                                    BenchmarkMeasureType.TotalHeapAllocations,
                                    "Total Bytes allocated",
                                    BenchmarkDiscreteStats.GetFrom(new double[] { benchmarkTest.GcStats.GetTotalAllocatedBytes(true) }));
                            }

                            test.Close(benchmarkTest.Success ? TestStatus.Pass : TestStatus.Fail, testEndTime - testStartTime);
                        }
                        catch (Exception testException)
                        {
                            testSuite.SetErrorInfo(testException);
                            consoleLogger.WriteLine(LogKind.Error, testException.ToString());
                        }
                    }

                    testSuite.Close(suiteEndTime - suiteStartTime);
                }
            }
        }

        if (exception is not null)
        {
            return new[] { "Datadog Exporter error: " + exception };
        }

        return new[] { $"Datadog Exporter ran successfully." };
    }

    internal async Task DisposeTestSessionAndModules()
    {
        foreach (var kv in _testModules)
        {
            BenchmarkMetadata.GetTimes(kv.Key, out var moduleStartTime, out var moduleEndTime);
            await kv.Value.CloseAsync(moduleEndTime - moduleStartTime).ConfigureAwait(false);
        }

        _testModules.Clear();

        var testSession = TestSession;
        testSession.Tags.CommandExitCode = Environment.ExitCode.ToString();
        await testSession.CloseAsync(Environment.ExitCode == 0 ? TestStatus.Pass : TestStatus.Fail, DateTime.UtcNow - testSession.StartTime).ConfigureAwait(false);
    }
}
