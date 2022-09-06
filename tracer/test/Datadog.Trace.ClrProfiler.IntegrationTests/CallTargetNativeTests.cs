// <copyright file="CallTargetNativeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
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
            for (int i = 0; i < 10; i++)
            {
                bool fastPath = i < 9;
                yield return new object[] { i, fastPath };
            }
        }

        [SkippableTheory]
        [MemberData(nameof(MethodArgumentsData))]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void MethodArgumentsInstrumentation(int numberOfArguments, bool fastPath)
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: numberOfArguments.ToString()))
            {
                string beginMethodString = $"ProfilerOK: BeginMethod\\({numberOfArguments}\\)";
                if (!fastPath)
                {
                    beginMethodString = $"ProfilerOK: BeginMethod\\(Array\\)";
                }

                int beginMethodCount = Regex.Matches(processResult.StandardOutput, beginMethodString).Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;
                int exceptionCount = Regex.Matches(processResult.StandardOutput, "Exception thrown.").Count;

                string[] typeNames = new string[]
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
        public void MethodRefArguments()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: "withref"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int begin2MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({2}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames = new string[]
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
        public void MethodOutArguments()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: "without"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int begin2MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({2}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames = new string[]
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
        public void MethodAbstract()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: "abstract"))
            {
                int beginMethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({0}\\)").Count;
                int begin1MethodCount = Regex.Matches(processResult.StandardOutput, $"ProfilerOK: BeginMethod\\({1}\\)").Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

                string[] typeNames = new string[]
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
        [Trait("SupportsInstrumentationVerification", "True")]
        public void RemoveIntegrations()
        {
            SetInstrumentationVerification();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: "remove"))
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
    }
}
