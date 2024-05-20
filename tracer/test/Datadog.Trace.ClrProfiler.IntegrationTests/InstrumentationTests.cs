// <copyright file="InstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class InstrumentationTests : TestHelper
    {
        public InstrumentationTests(ITestOutputHelper output)
            : base("Console", output)
        {
            SetServiceVersion("1.0.0");
        }

// There's nothing .NET 8 specific here, it's just that it's an identical test for all runtimes
// so there's not really any point in testing it repeatedly
#if NET8_0
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentDotnetBuild()
        {
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet("new console -n instrumentation_test -o . --no-restore");
            AssertNotInstrumented(agent, logDir);

            logDir = await RunDotnet("restore");
            AssertNotInstrumented(agent, logDir);

            logDir = await RunDotnet("build");
            AssertNotInstrumented(agent, logDir);

            logDir = await RunDotnet("publish");
            AssertNotInstrumented(agent, logDir);

            return;

            Task<string> RunDotnet(string arguments) => RunDotnetCommand(workingDir, agent, arguments);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentsDotNetRun()
        {
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet("new console -n instrumentation_test -o . --no-restore");
            AssertNotInstrumented(agent, logDir);

            // this _should_ be instrumented so we expect managed data.
            // we also expect telemetry, but we end the app so quickly there's a risk of flake
            logDir = await RunDotnet("run");

            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);
            allFiles.Should().Contain(filename => Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-instrumentation_test-"));
            agent.Telemetry.Should().NotBeEmpty();

            return;

            Task<string> RunDotnet(string arguments) => RunDotnetCommand(workingDir, agent, arguments);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentsDotNetTest()
        {
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet("new xunit -n instrumentation_test -o . --no-restore");
            AssertNotInstrumented(agent, logDir);

            // this _should_ be instrumented so we expect managed data.
            // we also expect telemetry, but we end the app so quickly there's a risk of flake
            logDir = await RunDotnet("test");

            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);

            // dotnet test might either of the following, so process name could be testhost or dotnet:
            // - "C:\Users\andrew.lock\AppData\Local\Temp\upo3di0x\bin\Debug\net8.0\testhost.exe"  --runtimeconfig "C:\Users\andrew.lock\AppData\Local\Temp\upo3di0x\bin\Debug\net8.0\instrumentation_test.runtimeconfig.json" --depsfile "C:\Users\andrew.lock\AppData\Local\Temp\upo3di0x\bin\Debug\net8.0\instrumentation_test.deps.json" --port 56961 --endpoint 127.0.0.1:056961 --role client --parentprocessid 71908 --telemetryoptedin false
            // - /usr/share/dotnet/dotnet exec --runtimeconfig /tmp/yei4siaw/bin/Debug/net8.0/instrumentation_test.runtimeconfig.json --depsfile /tmp/yei4siaw/bin/Debug/net8.0/instrumentation_test.deps.json /tmp/yei4siaw/bin/Debug/net8.0/testhost.dll --port 44167 --endpoint 127.0.0.1:044167 --role client --parentprocessid 11522 --telemetryoptedin false
            allFiles.Should()
                    .Contain(
                         filename =>
                             Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-testhost-")
                          || Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-dotnet-"));
            agent.Telemetry.Should().NotBeEmpty();

            return;

            Task<string> RunDotnet(string arguments) => RunDotnetCommand(workingDir, agent, arguments);
        }
#endif

#if !NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentRunsOnEolFramework()
        {
            var logDir = Path.Combine(LogDirectory, nameof(InstrumentRunsOnEolFramework));
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();

            // not necessary, but belt-and-braces
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);
            allFiles.Should().Contain(filename => Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-dotnet-"));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentRunsOnEolFrameworkWithSSI()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");

            var logDir = Path.Combine(LogDirectory, nameof(DoesNotInstrumentRunsOnEolFrameworkWithSSI));
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentRunsOnEolFrameworkInSSIWithOverride()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            // set the "run me anyway, dammit" flag
            SetEnvironmentVariable("DD_TRACE_ALLOW_UNSUPPORTED_SSI_RUNTIMES", "true");

            var logDir = Path.Combine(LogDirectory, nameof(InstrumentRunsOnEolFrameworkInSSIWithOverride));
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();

            // not necessary, but belt-and-braces
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);
            allFiles.Should().Contain(filename => Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-dotnet-"));
        }

#endif

        private async Task<string> RunDotnetCommand(string workingDirectory, MockTracerAgent mockTracerAgent, string arguments)
        {
            // Create unique folder for easier post-mortem analysis
            var logDir = $"{workingDirectory}_logs_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}";
            Output.WriteLine("Running: dotnet " + arguments);
            Output.WriteLine("Using logDirectory: " + logDir);

            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            using var process = await ProfilerHelper.StartProcessWithProfiler(
                                    executable: EnvironmentHelper.GetDotnetExe(),
                                    EnvironmentHelper,
                                    mockTracerAgent,
                                    arguments,
                                    workingDirectory: workingDirectory); // points to the sample project

            using var helper = new ProcessHelper(process);

            WaitForProcessResult(helper);
            return logDir;
        }

        private void AssertNotInstrumented(MockTracerAgent mockTracerAgent, string logDir)
        {
            // should have bailed out, but we still write logs to the native loader log
            // _and_ the native tracer/profiler (because they're initialized), so important
            // point is we don't have managed logs, and no spans or telemetry
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);

            allFiles.Should().NotContain(filename => Path.GetFileName(filename).StartsWith("dotnet-tracer-managed-dotnet-"));
            mockTracerAgent.Spans.Should().BeEmpty();
            mockTracerAgent.Telemetry.Should().BeEmpty();
        }

        private void AddFilesAsReportable(string logDir, AssertionScope scope, string[] allFiles)
        {
            scope.AddReportable(
                $"Log files in {logDir}",
                () =>
                {
                    var sb = new StringBuilder();
                    foreach (var filename in allFiles)
                    {
                        sb.Append("File: ").AppendLine(filename);
                        sb.AppendLine("-----------------------");
                        sb.AppendLine(File.ReadAllText(filename));
                    }

                    return sb.ToString();
                });
        }
    }
}
