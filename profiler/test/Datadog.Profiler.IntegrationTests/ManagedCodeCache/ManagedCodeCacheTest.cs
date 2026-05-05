// <copyright file="ManagedCodeCacheTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.ManagedCodeCache
{
    public class ManagedCodeCacheTest
    {
        private readonly ITestOutputHelper _output;

        public ManagedCodeCacheTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.TestProfiler")]
        public void ShouldValidateManagedCodeCache(string appName, string framework, string appAssembly)
        {
            var environment = new EnvironmentHelper(framework, enableTracer: false, enableProfiler: false, enableTestProfiler: true);
            var validationFile = Path.Combine(environment.LogDir, "validation.txt");

            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, environmentHelper: environment, commandLine: $"--output {validationFile}");

            var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            File.Exists(validationFile).Should().BeTrue();

            var reportContent = File.ReadAllText(validationFile);
            _output.WriteLine("[ManagedCodeCacheTest] === Validation Report ===");
            _output.WriteLine(reportContent);

            // Verify the report contains expected sections
            reportContent.Should().Contain("=== ManagedCodeCache Validation Report ===");
            reportContent.Should().Contain("## Summary");
            reportContent.Should().Contain("## Invalid IP Tests");
            reportContent.Should().Contain("Result: ✓ PASSED");
        }
    }
}
