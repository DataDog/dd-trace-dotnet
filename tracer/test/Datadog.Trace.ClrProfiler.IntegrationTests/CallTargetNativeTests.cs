// <copyright file="CallTargetNativeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CallTargetNativeTests : TestHelper
    {
        public CallTargetNativeTests()
            : base(new EnvironmentHelper("CallTargetNativeTest", typeof(TestHelper), samplesDirectory: Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false))
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> MethodArgumentsData()
        {
            for (int i = 0; i < 10; i++)
            {
                bool fastPath = i < 9;
                yield return new object[] { i, fastPath };
                yield return new object[] { i, fastPath };
            }
        }

        [TestCaseSource(nameof(MethodArgumentsData))]
        public void MethodArgumentsInstrumentation(int numberOfArguments, bool fastPath)
        {
            SetCallTargetSettings(enableCallTarget: true);
            SetEnvironmentVariable("DD_INTEGRATIONS", Path.Combine(EnvironmentHelper.GetSampleProjectDirectory(), "integrations.json"));
            int agentPort = TcpPortProvider.GetOpenPort();

            using (new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agentPort, arguments: numberOfArguments.ToString()))
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
                    Assert.AreEqual(43, beginMethodCount);
                    Assert.AreEqual(43, endMethodCount);
                    Assert.AreEqual(11, exceptionCount);
                }
                else
                {
                    Assert.AreEqual(42, beginMethodCount);
                    Assert.AreEqual(42, endMethodCount);
                    Assert.AreEqual(10, exceptionCount);
                }

                foreach (var typeName in typeNames)
                {
                    StringAssert.Contains(typeName, processResult.StandardOutput);
                }
            }
        }
    }
}
