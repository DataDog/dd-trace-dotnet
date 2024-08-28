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

#if NETCOREAPP && !NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task InstrumentRunsOnEolFramework()
        {
            var logDir = SetLogDirectory();

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
            var logDir = SetLogDirectory();

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
            SetEnvironmentVariable("DD_INJECT_FORCE", "true");
            var logDir = SetLogDirectory();

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
            AssertHasExpectedTelemetry(logFileName, processResult, pointsJson);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
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
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.complete", 
                               "tags": ["injection_forced:true"]
                             }]
                             """;
            AssertHasExpectedTelemetry(logFileName, processResult, pointsJson);
        }

#endif
#if NETCOREAPP3_1_OR_GREATER
        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData("1")]
        [InlineData("0")]
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
            agent.Spans.Should().NotBeEmpty();
            agent.Telemetry.Should().NotBeEmpty();

            var pointsJson = """
                             [{
                               "name": "library_entrypoint.complete", 
                               "tags": ["injection_forced:false"]
                             }]
                             """;
            AssertHasExpectedTelemetry(logFileName, processResult, pointsJson);
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

        private void AssertNativeLoaderLogContainsString(string logDir, string requiredLog)
        {
            using var scope = new AssertionScope();
            var allFiles = Directory.GetFiles(logDir);
            AddFilesAsReportable(logDir, scope, allFiles);

            var nativeLoaderLogFilenames = allFiles
                                  .Where(filename => Path.GetFileName(filename).StartsWith("dotnet-native-loader-dotnet-"))
                                  .ToList();
            nativeLoaderLogFilenames.Should().NotBeEmpty();
            var nativeLoaderLogFiles = nativeLoaderLogFilenames.Select(File.ReadAllText).ToList();
            nativeLoaderLogFiles.Should().Contain(log => log.Contains(requiredLog));
        }

        private void AssertHasExpectedTelemetry(string echoLogFileName, ProcessResult processResult, string pointsJson)
        {
            using var s = new AssertionScope();
            File.Exists(echoLogFileName).Should().BeTrue();
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
                                            "pid": {{processResult.Process.Id}}
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
                process.Process.WaitForExit(30_000);
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
