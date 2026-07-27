// <copyright file="GarbageCollectorCpuTimeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.GarbageCollections
{
    // Tests are done only for .NET 5+ because GC threads got names starting .NET 5
    // And this feature only work in case of GC server setting (not workstation)
    public class GarbageCollectorCpuTimeTest
    {
        private const string ScenarioGenerics = "--scenario 12 --param 2";
        private const string ScenarioAllocations = "--scenario 26 --param 2500";
        // This test asserts that GC threads report a non-zero CPU value. The GC sample is only
        // emitted when the GC threads' CPU-time delta since the previous export is >= 1 ms
        // (see NativeThreadsCpuProviderBase::GetSamples). A larger allocation count forces more
        // (and longer) collections so the server GC threads reliably accrue measurable CPU time.
        private const string ScenarioAllocationsHeavy = "--scenario 26 --param 20000";
        private static readonly StackFrame GcFrame = new("|lm:[native] GC |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:");
        private static readonly StackFrame ClrFrame = new("|lm:[native] CLR |ns: |ct: |cg: |fn:.NET |fg: |sg:");

        private static readonly StackTrace GcStack = new(GcFrame, ClrFrame);

        private readonly ITestOutputHelper _output;

        public GarbageCollectorCpuTimeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" })]
        public void CheckCpuTimeForGcThreadsIsEnabledByDefaultWithServerGC(string appName, string framework, string appAssembly)
        {
            // Use the heavier allocation workload so the GC threads reliably accrue >= 1 ms of CPU
            // (the GC sample is only emitted above that threshold). The default server-GC config is
            // kept on purpose here, so no DOTNET_GCHeapCount/DATAS overrides are set.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioAllocationsHeavy);
            // Enable GC Server
            runner.Environment.SetVariable("DOTNET_gcServer", "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().Contain(sample => IsGcCpuSample(sample) && sample.StackTrace.Equals(GcStack));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" })]
        public void CheckCpuTimeForGcThreadsValueIsReported(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioAllocationsHeavy);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "1");

            // Enable walltime and check GC frame does not have a value for this column
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            // enable cputime profiler to ensure we get cpu time for GC threads
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            // Enable GC Server
            runner.Environment.SetVariable("DOTNET_gcServer", "1");
            // Force several GC heaps (=> several ".NET Server GC" threads) and disable DATAS so
            // the runtime does not start with a single, lightly-used heap on .NET 9/10. This makes
            // the GC threads accrue >= 1 ms of CPU within the run, avoiding a flaky "no GC sample"
            // result driven by Windows' coarse (ms) thread-CPU accounting.
            runner.Environment.SetVariable("DOTNET_GCHeapCount", "4");
            runner.Environment.SetVariable("DOTNET_GCDynamicAdaptationMode", "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should()
                // match the GC stacktrace and check that the waltime value is 0 and the cpu value is not 0
                .Contain(sample => IsGcCpuSample(sample) && sample.Values[0] == 0 && sample.Values[1] != 0);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" })]
        public void CheckNoGcSampleIfCpuProfilingIsDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "1");

            // make sure walltime is activated to have samples
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            // cpu profiler is disabled to ensure:
            // - The app does not crash
            // - The GC sample is not present
            runner.Environment.SetVariable("DOTNET_gcServer", "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotContain(sample => sample.StackTrace.Equals(GcStack));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" })]
        public void CheckNoGcSampleIfWorkstationGC(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            // disable gc server
            runner.Environment.SetVariable("DOTNET_gcServer", "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotContain(sample => IsGcCpuSample(sample));
        }

        [TestAppFact("Samples.Computer01", new[] { "netcoreapp3.1", "net48" })]
        public void CheckFeatureIsDisabledIfUnsupportedDotnetVersions(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "1");

            // make sure walltime is activated to have samples
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            // enable gc server
            runner.Environment.SetVariable("COMPlus_gcServer", "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().NotContain(sample => IsGcCpuSample(sample));
        }

        private static bool IsGcCpuSample((StackTrace StackTrace, PprofHelper.Label[] Labels, long[] Values) sample)
        {
            return sample.StackTrace.Equals(GcStack) && sample.Labels.Any(label => label.Name == "gc_cpu_sample" && label.Value == "true");
        }
    }
}
