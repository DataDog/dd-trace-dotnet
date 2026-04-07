// <copyright file="ExceptionGeneratorTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class ExceptionGeneratorTest
    {
        private readonly ITestOutputHelper _output;

        public ExceptionGeneratorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // NOTE: now that .NET Framework is supported by default, the profiler tries to connect
        //       to connect to the Agent using namedpipe. Since the Agent does not exist in CI,
        //       the ETW support is disabled in the tests for .NET Framework.

        [TestAppFact("Samples.ExceptionGenerator")]
        public void CheckSmoke(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, _output);
            if (framework == "net48")
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.RunAndCheck();
        }
    }
}
