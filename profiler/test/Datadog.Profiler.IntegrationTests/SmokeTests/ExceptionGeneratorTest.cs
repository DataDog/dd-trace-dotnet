// <copyright file="ExceptionGeneratorTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

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

        [TestAppFact("Datadog.Demos.ExceptionGenerator", DisplayName = "ExceptionGenerator")]
        public void CheckSmoke(string appName, string framework, string appAssembly)
        {
            using var runner = new SmokeTestRunner(appName, framework, appAssembly, _output, enableNewPipeline: false);
            runner.RunAndCheck();
        }

        [TestAppFact("Datadog.Demos.ExceptionGenerator", DisplayName = "ExceptionGenerator-NewPipeline")]
        public void CheckSmokeNewPipeline(string appName, string framework, string appAssembly)
        {
            using var runner = new SmokeTestRunner(appName, framework, appAssembly, _output, enableNewPipeline: true);
            runner.RunAndCheck();
        }
    }
}
