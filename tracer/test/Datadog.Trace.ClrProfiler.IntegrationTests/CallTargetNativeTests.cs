// <copyright file="CallTargetNativeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CallTargetNativeTests : TestHelper
    {
        public CallTargetNativeTests(ITestOutputHelper output)
            : base(new EnvironmentHelper("CallTargetNativeTest", typeof(TestHelper), output, samplesDirectory: Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false), output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> MethodArgumentsData()
        {
            for (var i = 0; i < 10; i++)
            {
                var fastPath = i < 9;
                yield return new object[] { i, fastPath };
            }
        }

        [SkippableTheory]
        [MemberData(nameof(MethodArgumentsData))]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task MethodArgumentsInstrumentation(int numberOfArguments, bool fastPath)
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: numberOfArguments.ToString()))
            {
                string beginMethodString = $"ProfilerOK: BeginMethod\\({numberOfArguments}\\)";
                if (!fastPath)
                {
                    beginMethodString = $"ProfilerOK: BeginMethod\\(Array\\)";
                }

                int beginMethodCount = Regex.Matches(processResult.StandardOutput, beginMethodString).Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;
                int exceptionCount = Regex.Matches(processResult.StandardOutput, "Exception thrown.").Count;

                string[] typeNames =
                {
                    ".VoidMethod",
                    ".ReturnValueMethod",
                    ".ReturnReferenceMethod",
                    ".ReturnGenericMethod<string>",
                    ".ReturnGenericMethod<int>",
                    ".ReturnGenericMethod",
                };

                if (numberOfArguments == 0)
                {
                    // On number of arguments = 0 the throw exception on integrations async continuation runs.
                    // So we have 1 more case with an exception being reported from the integration.
                    Assert.Equal(172, beginMethodCount);
                    Assert.Equal(172, endMethodCount);
                    Assert.Equal(44, exceptionCount);
                }
                else
                {
                    Assert.Equal(168, beginMethodCount);
                    Assert.Equal(168, endMethodCount);
                    Assert.Equal(40, exceptionCount);
                }

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task MethodRefArguments()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "withref"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int begin2MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({2}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames =
                {
                    ".VoidMethod",
                    ".VoidRefMethod",
                };

                Assert.Equal(8, beginMethodCount);
                Assert.Equal(8, begin2MethodCount);
                Assert.Equal(16, endMethodCount);

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task MethodOutArguments()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "without"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int begin2MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({2}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames =
                {
                    ".VoidMethod",
                };

                Assert.Equal(4, beginMethodCount);
                Assert.Equal(4, begin2MethodCount);
                Assert.Equal(8, endMethodCount);

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task MethodAbstract()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "abstract"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({0}\\)").Count;
                int begin1MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames =
                {
                    ".VoidMethod",
                    ".OtherMethod",
                };

                Assert.Equal(1, beginMethodCount);
                Assert.Equal(4, begin1MethodCount);
                Assert.Equal(5, endMethodCount);

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task MethodInterface()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "interface"))
            {
                int begin1MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\(").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames =
                {
                    ".VoidMethod",
                    ".ReturnValueMethod",
                };

                Assert.Equal(3, begin1MethodCount);
                Assert.Equal(3, endMethodCount);

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task RemoveIntegrations()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "remove"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\(").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;
                int notInstrumentedMethodCount = Regex.Matches(processResult.StandardOutput, "OK: Not instrumented").Count;

                // Enabled, Disabled, Enabled -> 2 functions per cycle
                Assert.Equal(4, beginMethodCount);
                Assert.Equal(4, endMethodCount);
                Assert.Equal(2, notInstrumentedMethodCount);

                Assert.Contains(".VoidMethod", processResult.StandardOutput);

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task ExtraIntegrations()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "extras"))
            {
                int begin1MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({0}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames = { ".NonVoidWithBranchToLastReturn" };

                begin1MethodCount.Should().Be(1);
                endMethodCount.Should().Be(1);

                processResult.StandardOutput.Should().ContainAll(typeNames);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task CallTargetBubbleUpExceptionIntegrations()
        {
            SetInstrumentationVerification();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "calltargetbubbleupexceptions");
            processResult.ExitCode.Should().Be(0);
            var beginMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: BeginMethod\\(0\\)<CallTargetNativeTest.NoOp.CallTargetBubbleUpExceptionsIntegration").Count;
            var endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod(Async)?\\([0|1]\\)<CallTargetNativeTest.NoOp.CallTargetBubbleUpExceptionsIntegration, CallTargetNativeTest").Count;
            beginMethodCount.Should().Be(6);
            endMethodCount.Should().Be(6);
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task CategorizedCallTargetIntegrations()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "categories"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\(").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;
                int notInstrumentedMethodCount = Regex.Matches(processResult.StandardOutput, "OK: Not instrumented").Count;

                // Enabled, Disabled, Enabled -> 2 functions per cycle
                Assert.Equal(20, beginMethodCount);
                Assert.Equal(20, endMethodCount);
                Assert.Equal(10, notInstrumentedMethodCount);

                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableFact]
        [Trait("SupportsInstrumentationVerification", "True")]
        [Trait("RunOnWindows", "True")]
        public async Task MethodRefStructArguments()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: "withrefstruct"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int begin2MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({2}\\)").Count;
                int begin4MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({4}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames =
                {
                    "ReadOnlySpan<char>",
                    "Span<char>",
                    "ReadOnlyRefStruct",
                    "VoidMixedMethod",
                };

                Assert.Equal(6, beginMethodCount);
                Assert.Equal(9, begin2MethodCount);
                Assert.Equal(3, begin4MethodCount);
                Assert.Equal(18, endMethodCount);

                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }
    }
}
