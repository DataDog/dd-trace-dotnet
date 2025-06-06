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
        private static readonly StackFrame GcFrame = new("|lm:[native] GC |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:");
        private static readonly StackFrame ClrFrame = new("|lm:[native] CLR |ns: |ct: |cg: |fn:.NET |fg: |sg:");

        private static readonly StackTrace GcStack = new(GcFrame, ClrFrame);

        private readonly ITestOutputHelper _output;

        public GarbageCollectorCpuTimeTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0" })]
        public void CheckCpuTimeForGcThreadsIsEnabledByDefaultWithServerGC(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioAllocations);
            // Enable GC Server
            runner.Environment.SetVariable("DOTNET_gcServer", "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should().Contain(sample => IsGcCpuSample(sample) && sample.StackTrace.Equals(GcStack));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0" })]
        public void CheckCpuTimeForGcThreadsValueIsReported(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioAllocations);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "1");

            // Enable walltime and check GC frame does not have a value for this column
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            // enable cputime profiler to ensure we get cpu time for GC threads
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            // Enable GC Server
            runner.Environment.SetVariable("DOTNET_gcServer", "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            SamplesHelper.GetSamples(runner.Environment.PprofDir).Should()
                // match the GC stacktrace and check that the waltime value is 0 and the cpu value is not 0
                .Contain(sample => IsGcCpuSample(sample) && sample.Values[0] == 0 && sample.Values[1] != 0);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0" })]
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

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0", "net9.0" })]
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
