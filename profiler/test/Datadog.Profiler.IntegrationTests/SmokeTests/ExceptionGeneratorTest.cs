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

        [TestAppFact("Samples.ExceptionGenerator")]
        public void CheckSmoke(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, _output);
            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact("Samples.ExceptionGenerator")]
        public void CheckSmokeForOldWayToStackWalk(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, _output);
            runner.EnvironmentHelper.CustomEnvironmentVariables[EnvironmentVariables.UseBacktrace2] = "0";
            runner.RunAndCheck();
        }
    }
}
