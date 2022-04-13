// <copyright file="BuggyBitsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public partial class BuggyBitsTest
    {
        private readonly ITestOutputHelper _output;

        public BuggyBitsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Datadog.Demos.BuggyBits", DisplayName = "BuggyBits")]
        public void CheckSmoke(string appName, string framework, string appAssembly)
        {
            using var runner = new SmokeTestRunner(appName, framework, appAssembly, _output, enableNewPipeline: false);
            runner.RunAndCheck();
        }

        [TestAppFact("Datadog.Demos.BuggyBits", DisplayName = "BuggyBits-NewPipeline")]
        public void CheckSmokeNewPipeline(string appName, string framework, string appAssembly)
        {
            using var runner = new SmokeTestRunner(appName, framework, appAssembly, _output, enableNewPipeline: true);
            runner.RunAndCheck();
        }
    }
}
