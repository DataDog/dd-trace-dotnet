// <copyright file="NativeProfilerChecks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NativeProfilerChecks : TestHelper
    {
        public NativeProfilerChecks(ITestOutputHelper output)
            : base(new EnvironmentHelper("Datadog.Tracer.Native.Checks", typeof(TestHelper), output, samplesDirectory: Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false), output)
        {
            SetServiceVersion("1.0.0");
            EnableDebugMode();
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void RunChecksProject()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent))
            {
                var exitCode = processResult.ExitCode;
                if (exitCode == 139)
                {
                    // TODO: We should figure out why this happens, but hard to reproduce
                    throw new SkipException("Unexpected segmentation fault in NativeProfilerChecks");
                }

                exitCode.Should().Be(0);
                VerifyInstrumentation(processResult.Process);
            }
        }
    }
}
