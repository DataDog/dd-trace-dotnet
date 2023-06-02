// <copyright file="ProcessMemoryAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Tools.Runner.Checks;
using Datadog.Trace.Tools.Runner.DumpAnalysis;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class ProcessMemoryAnalyzerTests : ConsoleTestHelper
    {
        public ProcessMemoryAnalyzerTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        [Trait("RunOnWindows", "True")]
        public async Task WalksManagedStackTracesCorrectly()
        {
            using var helper = await StartConsole(enableProfiler: false);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            ProcessMemoryAnalyzer.Analyze(processInfo.Id);

            console.Output.Should().Contain("System.Threading.Thread.Sleep");
        }
    }
}
