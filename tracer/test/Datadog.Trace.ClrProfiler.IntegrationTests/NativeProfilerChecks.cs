// <copyright file="NativeProfilerChecks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NativeProfilerChecks : TestHelper
    {
        public NativeProfilerChecks(ITestOutputHelper output)
            : base(new EnvironmentHelper("Datadog.Trace.ClrProfiler.Native.Checks", typeof(TestHelper), output, samplesDirectory: Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false), output)
        {
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("RunOnWindows", "True")]
        public void RunChecksProject()
        {
            SetCallTargetSettings(enableCallTarget: true);
            SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agentPort))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");
            }
        }
    }
}
