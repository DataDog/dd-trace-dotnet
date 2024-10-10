// <copyright file="UnsafeToUnwindTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class UnsafeToUnwindTest
    {
        private readonly ITestOutputHelper _output;

        public UnsafeToUnwindTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void AvoidUnwindingUnsafeExecution(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 28", enableTracer: true);
            // TODO add the env var here
            runner.Environment.SetVariable("<ENVIRONMENT VARIABLE>", "");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            var toSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:UnSafeToUnwind |fg: |sg:()"));

            var exceptionToSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionUnsafeUnwind |fg: |sg:()"));

            foreach (var sample in samples)
            {
                Assert.False(sample.StackTrace.EndWith(toSkipStack));
                Assert.False(sample.StackTrace.EndWith(exceptionToSkipStack));
            }
        }
    }
}
