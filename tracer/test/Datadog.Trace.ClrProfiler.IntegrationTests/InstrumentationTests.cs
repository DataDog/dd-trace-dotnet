// <copyright file="InstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    // These tests are based on SSI variables, so we have to explicitly reset them
    [EnvironmentRestorer("DD_INJECTION_ENABLED", "DD_INJECT_FORCE", "DD_TELEMETRY_FORWARDER_PATH")]
    public class InstrumentationTests : TestHelper, IClassFixture<InstrumentationTests.TelemetryReporterFixture>
    {
        private const string WatchFileEnvironmentVariable = "DD_INTERNAL_TEST_FILE_TO_WATCH";
        private readonly TelemetryReporterFixture _fixture;

        public InstrumentationTests(ITestOutputHelper output, TelemetryReporterFixture fixture)
            : base("Console", output)
        {
            _fixture = fixture;
            SetServiceVersion("1.0.0");
        }

        // There's nothing .NET 8 specific here, it's just that it's an identical test for all runtimes
        // so there's not really any point in testing it repeatedly
#if NET8_0
        public static string GetProgramCSThatMakesSpans()
        {
            return @"using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Foo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Make 1 HTTP requests to generate auto-instrumented spans
            using var httpClient = new HttpClient();
            for (int i = 0; i < 1; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(""http://localhost:55555/test"");
                    Console.WriteLine($""Request {i + 1} status: {response.StatusCode}"");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($""Request {i + 1} failed: {ex.Message}"");
                }
            }

            // just give it a sec for any spans to be sent (unsure if needed)
            await Task.Delay(1000);
        }
    }
}";
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentDotnetBuild()
        {
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet("new console -n instrumentation_test -o . --no-restore");
            FixTfm(workingDir);
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

        [SkippableTheory]
        [InlineData("vsdbg")] // list has vsdbg and vsdbg.exe
        [InlineData("dd-trace")] // list has dd-trace and dd-trace.exe
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentExcludedNames(string excludedProcess)
        {
            // FIXME: this should also take into account case insensitivity, but that is not yet supported
            // https://devblogs.microsoft.com/oldnewthing/20241007-00/?p=110345
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet($"new console -n {excludedProcess} -o . --no-restore");
            FixTfm(workingDir);
            AssertNotInstrumented(agent, logDir);

            var programCs = GetProgramCSThatMakesSpans();

            File.WriteAllText(Path.Combine(workingDir, "Program.cs"), programCs);

            // We use publish and then direct execution to avoid calling dotnet run
            // Currently, today, we instrument dotnet run, which results in some "spurious" spans (e.g. command_execution)
            // In the future, we may change that. But hte important part is that we don't instrument the target process itself
            var publishDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            logDir = await RunDotnet($"publish -o \"{publishDir}\"");
            AssertNotInstrumented(agent, logDir);

            // this _should NOT_ be instrumented
            var extension = EnvironmentTools.IsWindows() ? ".exe" : string.Empty;
            logDir = await RunCommand(workingDir, agent, Path.Join(publishDir, $"{excludedProcess}{extension}"));
            AssertNotInstrumented(agent, logDir);

            return;

            Task<string> RunDotnet(string arguments) => RunDotnetCommand(workingDir, agent, arguments);
        }

        [SkippableTheory]
        [InlineData("VSdbuG")] // list has vsdbg and vsdbg.exe, but not vsdbug
        [Trait("RunOnWindows", "True")]
        public async Task DoesInstrumentAllowedProcesses(string allowedProcess)
        {
            // Just a safe guard in case we break the test at some point in the future and
            // it keeps passing just because instrumentation isn't properly setup.
            var workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(workingDir);

            Output.WriteLine("Using workingDirectory: " + workingDir);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

            var logDir = await RunDotnet($"new console -n {allowedProcess} -o . --no-restore");
            FixTfm(workingDir);
            AssertNotInstrumented(agent, logDir);

            var programCs = GetProgramCSThatMakesSpans();

            File.WriteAllText(Path.Combine(workingDir, "Program.cs"), programCs);

            // We use publish and then direct execution to avoid calling dotnet run
            // Currently, today, we instrument dotnet run, which results in some "spurious" spans (e.g. command_execution)
            // In the future, we may change that. But hte important part is that we don't instrument the target process itself
            var publishDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            logDir = await RunDotnet($"publish -o \"{publishDir}\"");
            AssertNotInstrumented(agent, logDir);

            // this _SHOULD_ be instrumented
            var extension = EnvironmentTools.IsWindows() ? ".exe" : string.Empty;
            logDir = await RunCommand(workingDir, agent, Path.Join(publishDir, $"{allowedProcess}{extension}"));
            AssertInstrumented(agent, logDir);

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

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task WhenUsingRelativeTracerHome_InstrumentsApp()
        {
            SetLogDirectory();
            // the working dir when we run the app is the _test_ project, not the app itself, so we need to be relative to that
            // This is a perfect example of why we don't recommend using relative paths for these variables
            var workingDir = Environment.CurrentDirectory;
            var monitoringHome = EnvironmentHelper.MonitoringHome;
            var path = PathUtil.GetRelativePath(workingDir, monitoringHome);
            var effectivePath = Path.GetFullPath(Path.Combine(workingDir, path));
            Output.WriteLine($"Using DD_DOTNET_TRACER_HOME={path} with workingDir={workingDir} and monitoringHome={monitoringHome}, giving an effective path of {effectivePath}");
            SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", path);
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task WhenUsingPathWithDotsInInTracerHome_InstrumentsApp()
        {
            SetLogDirectory();
            var path = Path.Combine(EnvironmentHelper.MonitoringHome, "..", Path.GetFileName(EnvironmentHelper.MonitoringHome)!);
            Output.WriteLine("Using DD_DOTNET_TRACER_HOME " + path);
            SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", path);
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();
        }

        [SkippableTheory]
        [CombinatorialData]
        [Trait("RunOnWindows", "True")]
        public async Task WhenUsingDataPipeline_WritesLibdatadogLogs(bool debugEnabled)
        {
            var logDir = SetLogDirectory(debugEnabled ? "_true" : "_false");

            SetEnvironmentVariable("DD_TRACE_DATA_PIPELINE_ENABLED", "1");
            EnvironmentHelper.DebugModeEnabled = debugEnabled;
            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();
            AssertInstrumented(agent, logDir);

            // Verify the datapipeline log is there and has some data
            var allFiles = Directory.GetFiles(logDir);
            var filename = allFiles.Should().Contain(filename => Path.GetFileName(filename).StartsWith("dotnet-tracer-libdatadog-")).Subject;

            if (debugEnabled)
            {
                File.ReadAllText(filename)
                    .Should()
                    .NotBeNullOrWhiteSpace().And.Contain(
                         """
                         "level":"DEBUG"
                         """);
            }
            else
            {
                File.ReadAllText(filename)
                    .Should()
                    .BeEmpty(); // Check that file exists but it's empty.
            }
        }

#if NETCOREAPP && !NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentRunsOnEolFramework()
        {
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);
            AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentRunsOnEolFrameworkWithSSI()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
            // this is already tested in AssertNotInstrumented, but adding an explicit check here to make sure
            if (EnvironmentTools.IsWindows())
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs enabled");
                Directory.GetFiles(logDir).Should().NotContain(filename => Path.GetFileName(filename).StartsWith("dotnet-"));
            }
            else
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DoesNotInstrumentOnEolFrameworkWithSsiButWritesLogsIfEnabled()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            // This ensures we _do_ write the native loader log, even if we don't instrument
            SetEnvironmentVariable("DD_TRACE_LOG_BUFFERING_ENABLED", "0");
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
            AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
            if (EnvironmentTools.IsWindows())
            {
                Directory.GetFiles(logDir).Should().Contain(filename => Path.GetFileName(filename).StartsWith("dotnet-native-loader-"));
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentRunsOnEolFrameworkInSSIWithOverride()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            // set the "run me anyway, dammit" flag
            SetEnvironmentVariable("DD_INJECT_FORCE", "true");
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);
            if (EnvironmentTools.IsWindows())
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs enabled");
                AssertNativeLoaderLogContainsString(logDir, "Buffered logs flushed and buffering disabled");
            }
            else
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task OnEolFrameworkInSsi_WhenForwarderPathIsNotSet_DoesNotFailAndLogs()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            // DD_TELEMETRY_FORWARDER_PATH is not set

            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
            AssertNativeLoaderLogContainsString(logDir, "SingleStepGuardRails::SendTelemetry: Unable to send telemetry, DD_TELEMETRY_FORWARDER_PATH is not set");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task OnEolFrameworkInSsi_WhenForwarderPathDoesNotExist_DoesNotFailAndLogs()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", "does_not_exist");

            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
            AssertNativeLoaderLogContainsString(logDir, "SingleStepGuardRails::SendTelemetry: Unable to send telemetry, DD_TELEMETRY_FORWARDER_PATH path does not exist");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [Flaky("The creation of the app is flaky due to the .NET SDK: https://github.com/NuGet/Home/issues/14343")]
        public async Task OnEolFrameworkInSsi_WhenForwarderPathExists_CallsForwarderWithExpectedTelemetry()
        {
            var logDir = SetLogDirectory();
            var logFileName = Path.Combine(logDir, $"{Guid.NewGuid()}.txt");

            var echoApp = _fixture.GetAppPath(Output, EnvironmentHelper);
            Output.WriteLine("Setting forwarder to " + echoApp);
            Output.WriteLine("Logging telemetry to " + logFileName);

            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", echoApp);

            SetEnvironmentVariable(WatchFileEnvironmentVariable, logFileName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.abort", 
                               "tags": ["reason:eol_runtime"]
                             },{
                               "name": "library_entrypoint.abort.runtime"
                             }]
                             """;
            await AssertHasExpectedTelemetry(logFileName, processResult, pointsJson,  "abort", ".NET Core 3.0 or lower", "incompatible_runtime");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [Flaky("The creation of the app is flaky due to the .NET SDK: https://github.com/NuGet/Home/issues/14343")]
        public async Task OnEolFrameworkInSsi_WhenOverriden_CallsForwarderWithExpectedTelemetry()
        {
            var logDir = SetLogDirectory();
            var logFileName = Path.Combine(logDir, $"{Guid.NewGuid()}.txt");
            var echoApp = _fixture.GetAppPath(Output, EnvironmentHelper);
            Output.WriteLine("Setting forwarder to " + echoApp);
            Output.WriteLine("Logging telemetry to " + logFileName);

            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", echoApp);
            SetEnvironmentVariable("DD_INJECT_FORCE", "true");

            SetEnvironmentVariable(WatchFileEnvironmentVariable, logFileName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.complete", 
                               "tags": ["injection_forced:true"]
                             }]
                             """;
            await AssertHasExpectedTelemetry(logFileName, processResult, pointsJson, "success", "Force instrumentation enabled, incompatible runtime, .NET Core 3.0 or lower", "success_forced");
        }

#endif
#if NETCOREAPP3_1_OR_GREATER
        // We have different behaviour depending on whether the framework is in preview
        // This condition should always point to the "next" version of .NET
        // e.g. if .NET 10 is in preview, use NET10_0_OR_GREATER.
        // Once .NET 10 goes GA, update this to NET11_0_OR_GREATER
#if NET11_0_OR_GREATER
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [Flaky("The creation of the app is flaky due to the .NET SDK: https://github.com/NuGet/Home/issues/14343")]
        public async Task OnPreviewFrameworkInSsi_CallsForwarderWithExpectedTelemetry()
        {
            var logDir = SetLogDirectory();
            var logFileName = Path.Combine(logDir, $"{Guid.NewGuid()}.txt");
            var echoApp = _fixture.GetAppPath(Output, EnvironmentHelper);
            Output.WriteLine("Setting forwarder to " + echoApp);
            Output.WriteLine("Logging telemetry to " + logFileName);

            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", echoApp);
            // Need to force injection because we bail by default
            SetEnvironmentVariable("DD_INJECT_FORCE", "true");

            SetEnvironmentVariable(WatchFileEnvironmentVariable, logFileName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.complete", 
                               "tags": ["injection_forced:true"]
                             }]
                             """;
            await AssertHasExpectedTelemetry(logFileName, processResult, pointsJson, "success", "Force instrumentation enabled, incompatible runtime, .NET 10 or higher", "success_forced");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        [Flaky("The creation of the app is flaky due to the .NET SDK: https://github.com/NuGet/Home/issues/14343")]
        public async Task OnPreviewFrameworkInSsi_WhenForwarderPathExists_CallsForwarderWithExpectedTelemetry()
        {
            var logDir = SetLogDirectory();
            var logFileName = Path.Combine(logDir, $"{Guid.NewGuid()}.txt");

            var echoApp = _fixture.GetAppPath(Output, EnvironmentHelper);
            Output.WriteLine("Setting forwarder to " + echoApp);
            Output.WriteLine("Logging telemetry to " + logFileName);

            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", echoApp);

            SetEnvironmentVariable(WatchFileEnvironmentVariable, logFileName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.abort", 
                               "tags": ["reason:incompatible_runtime"]
                             },{
                               "name": "library_entrypoint.abort.runtime"
                             }]
                             """;
            await AssertHasExpectedTelemetry(logFileName, processResult, pointsJson, "abort", ".NET 10 or higher", "incompatible_runtime");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task OnPreviewFrameworkInSsi_Buffers()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertNotInstrumented(agent, logDir);
            // this is already tested in AssertNotInstrumented, but adding an explicit check here to make sure
            if (EnvironmentTools.IsWindows())
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs enabled");
                Directory.GetFiles(logDir).Should().NotContain(filename => Path.GetFileName(filename).StartsWith("dotnet-"));
            }
            else
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
            }
        }
#else
        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("1")]
        [InlineData("0")]
        [Flaky("The creation of the app is flaky due to the .NET SDK: https://github.com/NuGet/Home/issues/14343")]
        public async Task OnSupportedFrameworkInSsi_CallsForwarderWithExpectedTelemetry(string isOverriden)
        {
            var logDir = SetLogDirectory();
            var logFileName = Path.Combine(logDir, $"{Guid.NewGuid()}.txt");
            var echoApp = _fixture.GetAppPath(Output, EnvironmentHelper);
            Output.WriteLine("Setting forwarder to " + echoApp);
            Output.WriteLine("Logging telemetry to " + logFileName);

            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            SetEnvironmentVariable("DD_TELEMETRY_FORWARDER_PATH", echoApp);
            // this value doesn't matter, should have same result, and _shouldn't_ change the metrics
            SetEnvironmentVariable("DD_INJECT_FORCE", isOverriden);

            SetEnvironmentVariable(WatchFileEnvironmentVariable, logFileName);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.complete", 
                               "tags": ["injection_forced:false"]
                             }]
                             """;
            await AssertHasExpectedTelemetry(logFileName, processResult, pointsJson, "success", "Successfully configured automatic instrumentation", "success");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task OnSupportedFrameworkInSsi_BuffersLogsInitiallyAndThenFlushes()
        {
            // indicate we're running in auto-instrumentation, this just needs to be non-null
            SetEnvironmentVariable("DD_INJECTION_ENABLED", "tracer");
            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");
            AssertInstrumented(agent, logDir);
            if (EnvironmentTools.IsWindows())
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs enabled");
                AssertNativeLoaderLogContainsString(logDir, "Buffered logs flushed and buffering disabled");
            }
            else
            {
                AssertNativeLoaderLogContainsString(logDir, "Buffering of logs disabled");
            }
        }
#endif
#endif

        // The dynamic context switch/bail out is only available in .NET 8+
#if NET8_0_OR_GREATER
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task WhenDynamicCodeIsEnabled_InstrumentsApp()
        {
            SetLogDirectory();
            var dotnetRuntimeArgs = CreateRuntimeConfigWithDynamicCodeEnabled(true);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1", dotnetRuntimeArgs: dotnetRuntimeArgs);
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task WhenDynamicCodeIsDisabled_DoesNotInstrument()
        {
            // We move the logs to a throw-away temp location so that they're not checked by the logs-checker in CI.
            // That's because in this scenario the Profiler will never receive the stable config results, so will
            // never initialize, because we bail-out in the managed loader (i.e. we never initialize the tracer).
            // This isn't ideal - we'd rather bail out in the native loader - but it's not possible to check the context
            // switch on the native side AFAIK (though we should check again to see if we can).
            // Regardless, the CP is loaded, and generates an error in its logs, but as this isn't a supported scenario
            // anyway, that's fine.

            var logDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

            var dotnetRuntimeArgs = CreateRuntimeConfigWithDynamicCodeEnabled(false);

            using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1", dotnetRuntimeArgs: dotnetRuntimeArgs);
            agent.Spans.Should().BeEmpty();
            agent.Telemetry.Should().BeEmpty();
        }

        private string CreateRuntimeConfigWithDynamicCodeEnabled(bool enabled)
        {
            // Set to false when PublishAot is set _even if the app is not published with AOT_
            var name = "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";
            var value = enabled ? "true" : "false";

            // copy the app runtime config to a separate folder before modifying it
            var fileName = "Samples.Console.runtimeconfig.json";
            var sourceFile = Path.Combine(Path.GetDirectoryName(EnvironmentHelper.GetSampleApplicationPath())!, fileName);
            var destDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            var destFile = Path.Combine(destDir, fileName);
            Directory.CreateDirectory(destDir);

            Output.WriteLine("Reading contents of " + sourceFile);
            var contents = File.ReadAllText(sourceFile);

            // hacky replacement to add an extra property, but meh, we can expand/fix it later if
            // we need to support more values or support the value already existing
            var replacement = $$"""
                                    "configProperties": {
                                      "{{name}}": {{value}},
                                """;
            var fixedContents = contents.Replace("""    "configProperties": {""", replacement);

            Output.WriteLine("Writing new contents to" + destFile);
            File.WriteAllText(destFile, fixedContents);

            // return the path to the variable in the format needed to be passed to the dotnet exe
            // when running the program. Don't ask me why you need to use dotnet exec...
            // it's weird, but here we are
            var dotnetRuntimeArgs = $"exec --runtimeconfig \"{destFile}\"";
            return dotnetRuntimeArgs;
        }
#endif

        /// <summary>
        /// Should only be called _directly_ by running test, so that the testName is populated correctly
        /// </summary>
        private string SetLogDirectory(string suffix = "", [CallerMemberName] string testName = null)
        {
            var logDir = Path.Combine(LogDirectory, $"{testName}{suffix}");
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);
            return logDir;
        }

        private Task<string> RunDotnetCommand(string workingDirectory, MockTracerAgent mockTracerAgent, string arguments)
        {
            // Disable .NET CLI telemetry to prevent extra HTTP spans
            SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            return RunCommand(workingDirectory, mockTracerAgent, "dotnet", arguments);
        }

        private async Task<string> RunCommand(string workingDirectory, MockTracerAgent mockTracerAgent, string exe, string arguments = null)
        {
            // Create unique folder for easier post-mortem analysis
            var logDir = $"{workingDirectory}_logs_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}";
            Output.WriteLine($"Running: {exe} {arguments}");
            Output.WriteLine("Using logDirectory: " + logDir);

            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);
            // Disable .NET CLI telemetry to prevent extra HTTP spans
            SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

            using var process = await ProfilerHelper.StartProcessWithProfiler(
                                    executable: exe,
                                    EnvironmentHelper,
                                    mockTracerAgent,
                                    arguments,
                                    workingDirectory: workingDirectory); // points to the sample project

            using var helper = new ProcessHelper(process);

            WaitForProcessResult(helper);
            return logDir;
        }

        private void AssertInstrumented(MockTracerAgent agent, string logDir)
        {
            // should have bailed out, but we still write logs to the native loader log
            // _and_ the native tracer/profiler (because they're initialized), so important
            // point is we don't have managed logs, and no spans or telemetry
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);

            allFiles.Should().Contain(filename => Path.GetFileName(filename).StartsWith($"dotnet-tracer-managed-"));
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();
        }

        private void AssertNotInstrumented(MockTracerAgent mockTracerAgent, string logDir)
        {
            // should have bailed out, but we still write logs to the native loader log
            // _and_ the native tracer/profiler (because they're initialized), so important
            // point is we don't have managed logs, and no spans or telemetry
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);

            var loggingDisabled = IsAllLoggingDisabledForBailout();
            if (loggingDisabled)
            {
                // In this bail-out scenario, we should not see _any_ logs from the tracer (there will be a guid.txt file, but no logs)
                allFiles.Should().NotContain(filename => Path.GetFileName(filename).StartsWith("dotnet-"));
            }
            else
            {
                // We should _only_ have native loader logs, no other logs (managed, native tracer, etc.)
                allFiles.Should().OnlyContain(filename => Path.GetFileName(filename).StartsWith("dotnet-native-loader-") || Path.GetExtension(filename) != ".log");
                mockTracerAgent.Spans.Should().BeEmpty();
                mockTracerAgent.Telemetry.Should().BeEmpty();
            }

            mockTracerAgent.Spans.Should().BeEmpty();
            mockTracerAgent.Telemetry.Should().BeEmpty();
        }

        private void AssertNativeLoaderLogContainsString(string logDir, string requiredLog)
        {
            var allFiles = Directory.GetFiles(logDir);

            var nativeLoaderLogFilenames = allFiles
                                  .Where(filename => Path.GetFileName(filename).StartsWith("dotnet-native-loader-"))
                                  .ToList();

            if (nativeLoaderLogFilenames.Count == 0)
            {
                // We can't guarantee that the file should be here, so just assume we have checked for presence previously
                return;
            }

            var nativeLoaderLogFiles = nativeLoaderLogFilenames.Select(File.ReadAllText).ToList();
            nativeLoaderLogFiles.Should().Contain(log => log.Contains(requiredLog));
        }

        private async Task AssertHasExpectedTelemetry(string echoLogFileName, ProcessResult processResult, string pointsJson, string injectResult, string injectResultReason, string injectResultClass)
        {
            using var s = new AssertionScope();

            // Wait for the telemetry echo file to be written (with timeout)
            // The telemetry forwarder may write the file asynchronously after process exit
            var fileWaitTimeout = TimeSpan.FromSeconds(10);
            var fileWaitStart = DateTime.UtcNow;
            while (!File.Exists(echoLogFileName) && (DateTime.UtcNow - fileWaitStart) < fileWaitTimeout)
            {
                await Task.Delay(100);
            }

            File.Exists(echoLogFileName).Should().BeTrue($"Telemetry echo file should exist at {echoLogFileName} within {fileWaitTimeout.TotalSeconds} seconds");

            var echoLogContent = File.ReadAllText(echoLogFileName);
            s.AddReportable(echoLogFileName, echoLogContent);

#if NETFRAMEWORK
            var runtimeVersion = "4.7.2"; // best we get on the native side
            var runtimeName = ".NET Framework";
#else
            var runtimeName = ".NET Core";
#if NET5_0_OR_GREATER
            var runtimeVersion = $"{Environment.Version.Major}.{Environment.Version.Minor}.{Environment.Version.Build}";
#elif NETCOREAPP3_1
            var runtimeVersion = $"3.1.0";
#elif NETCOREAPP3_0
            var runtimeVersion = "3.0.0";
#elif NETCOREAPP2_1_OR_GREATER
            var runtimeVersion = "2.1.0";
#else
            var runtimeVersion = "2.0.0";
#endif
#endif
            var expectedTelemetry = $$"""
                                      {
                                          "metadata": {
                                            "runtime_name": "{{runtimeName}}",
                                            "runtime_version": "{{runtimeVersion}}",
                                            "language_name": "dotnet",
                                            "language_version": "{{runtimeVersion}}",
                                            "tracer_version": "{{TracerConstants.ThreePartVersion}}",
                                            "pid": {{processResult.Process.Id}},
                                            "inject_result": "{{injectResult}}",
                                            "inject_result_reason": "{{injectResultReason}}",
                                            "inject_result_class": "{{injectResultClass}}"
                                          },
                                          "points": {{pointsJson}}
                                      }
                                      """;

            var argTypePrefix = "library_entrypoint ";
            echoLogContent.Should().StartWith(argTypePrefix);
            var telemetryArgument = echoLogContent.Substring(argTypePrefix.Length);
            telemetryArgument.Should().BeJsonEquivalentTo(expectedTelemetry);
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

        private bool IsAllLoggingDisabledForBailout()
        {
            var isSsi = EnvironmentHelper.CustomEnvironmentVariables.TryGetValue("DD_INJECTION_ENABLED", out var injectionEnabled)
                     && !string.IsNullOrEmpty(injectionEnabled);

            var bufferingDisabled = EnvironmentHelper.CustomEnvironmentVariables.TryGetValue("DD_TRACE_LOG_BUFFERING_ENABLED", out var bufferingEnabled)
                                 && (bufferingEnabled.ToLower() == "false" || bufferingEnabled == "0");

            var loggingDisabled = isSsi && EnvironmentTools.IsWindows() && !bufferingDisabled;
            return loggingDisabled;
        }

        private void FixTfm(string workingDir)
        {
            // This is a hack because for _some_ reason, we can't set the -f flag in the Linux CI.
            // Force the project to target .NET 8 instead of whatever the SDK defaults to
            foreach (var projectFile in Directory.GetFiles(workingDir, "*.csproj"))
            {
                var projectContent = File.ReadAllText(projectFile);

                // Replace any target framework with the updated version
                // and add a langversion update to ensure we still compile
                var replacement = $"""
                                   <TargetFramework>{EnvironmentHelper.GetTargetFramework()}</TargetFramework>
                                   <LangVersion>latest</LangVersion>
                                   """;
                var updatedContent = Regex.Replace(
                    projectContent,
                    @"<TargetFramework>.+</TargetFramework>",
                    replacement);
                File.WriteAllText(projectFile, updatedContent);
            }
        }

        public class TelemetryReporterFixture : IDisposable
        {
            private readonly string _workingDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            private string _appPath;

            public string GetAppPath(ITestOutputHelper output, EnvironmentHelper environment)
            {
                if (!string.IsNullOrEmpty(_appPath))
                {
                    return _appPath;
                }

                var publishDir = Path.Combine(_workingDir, "publish");
                Directory.CreateDirectory(publishDir);
                output.WriteLine("Using forwarder directory " + _workingDir);

                // Create project directory, yeah this should _probably_ just be a sample, but meh
                var program = $"""
                               using System;
                               using System.IO;
                               using System.Text;
                               using System.Reflection;

                               var sb = new StringBuilder();
                               sb.Append(string.Join(" ", args));
                               sb.Append(" ");

                               string line;
                               while ((line = Console.In.ReadLine()) != null)
                                   sb.AppendLine(line);

                               var data = sb.ToString();

                               Console.WriteLine(data);

                               var logFileName = Environment.GetEnvironmentVariable("{WatchFileEnvironmentVariable}");
                               File.WriteAllText(logFileName, data);
                               """;
                File.WriteAllText(Path.Combine(_workingDir, "Program.cs"), program);

                var project = """
                              <Project Sdk="Microsoft.NET.Sdk">
                                  <PropertyGroup>
                                      <OutputType>Exe</OutputType>
                                      <TargetFramework>net8.0</TargetFramework>
                                  </PropertyGroup>
                              </Project>
                              """;
                File.WriteAllText(Path.Combine(_workingDir, "telemetry_echo.csproj"), project);

                // publish the echo app as self contained (for "simplicity")
                var rid = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentHelper.IsAlpine()) switch
                {
                    ("win", _, _) => "win-x64",
                    ("linux", "Arm64", false) => "linux-arm64",
                    ("linux", "Arm64", true) => "linux-musl-arm64",
                    ("linux", "X64", false) => "linux-x64",
                    ("linux", "X64", true) => "linux-musl-x64",
                    ("osx", "X64", _) => "osx-x64",
                    ("osx", "Arm64", _) => "osx-arm64",
                    var unsupportedTarget => throw new PlatformNotSupportedException(unsupportedTarget.ToString())
                };

                var startInfo = new ProcessStartInfo(environment.GetDotnetExe(), $"publish -c Release -r {rid} --self-contained -o {publishDir}")
                {
                    WorkingDirectory = _workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = new ProcessHelper(Process.Start(startInfo), x => output.WriteLine(x), x => output.WriteLine(x));
                const int timeoutMs = 30_000;
                if (!process.Process.WaitForExit(timeoutMs))
                {
                    var tookMemoryDump = MemoryDumpHelper.CaptureMemoryDump(process.Process, includeChildProcesses: true);
                    process.Process.Kill();
                    throw new Exception($"The sample did not exit in {timeoutMs}ms. Memory dump taken: {tookMemoryDump}. Killing process.");
                }

                process.Drain(15_000);

                var extension = EnvironmentTools.IsWindows() ? ".exe" : string.Empty;
                _appPath = Path.Combine(publishDir, $"telemetry_echo{extension}");
                output.WriteLine("Created forwarder at: " + _appPath);
                File.Exists(_appPath).Should().BeTrue();

                // need to chmod +x it on linux
                if (!EnvironmentTools.IsWindows())
                {
                    output.WriteLine("Running chmod +x " + _appPath);

                    var chmodStart = new ProcessStartInfo("chmod", $"+x {_appPath}")
                    {
                        WorkingDirectory = Path.GetDirectoryName(_appPath)!,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    using var chmodProcess = new ProcessHelper(Process.Start(chmodStart), x => output.WriteLine(x), x => output.WriteLine(x));
                    process.Process.WaitForExit(30_000);
                    process.Drain(15_000);
                }

                return _appPath;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_workingDir, recursive: true);
                }
                catch (Exception)
                {
                    // swallow
                }
            }
        }
    }
}
