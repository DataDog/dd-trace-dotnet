// <copyright file="TestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public abstract class TestHelper : IDisposable
    {
        protected TestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, samplePathOverrides), output)
        {
        }

        protected TestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, bool prependSamplesToAppName)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, samplePathOverrides, prependSamplesToAppName: false), output)
        {
        }

        protected TestHelper(string sampleAppName, ITestOutputHelper output)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output), output)
        {
        }

        protected TestHelper(EnvironmentHelper environmentHelper, ITestOutputHelper output)
        {
            EnvironmentHelper = environmentHelper;
            Output = output;

            Output.WriteLine($"Platform: {EnvironmentTools.GetPlatform()}");
            Output.WriteLine($"TargetPlatform: {EnvironmentTools.GetTestTargetPlatform()}");
            Output.WriteLine($"Configuration: {EnvironmentTools.GetBuildConfiguration()}");
            Output.WriteLine($"TargetFramework: {EnvironmentHelper.GetTargetFramework()}");
            Output.WriteLine($".NET Core: {EnvironmentHelper.IsCoreClr()}");
            Output.WriteLine($"Native Loader DLL: {EnvironmentHelper.GetNativeLoaderPath()}");

            // the directory would be created anyway, but in certain case a delay can lead to an exception from the LogEntryWatcher
            Directory.CreateDirectory(LogDirectory);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, LogDirectory);
        }

        public bool SecurityEnabled { get; private set; }

        protected virtual string LogDirectory => Path.Combine(DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance), $"{GetType().Name}Logs");

        protected EnvironmentHelper EnvironmentHelper { get; }

        protected string TestPrefix => $"{EnvironmentTools.GetBuildConfiguration()}.{EnvironmentHelper.GetTargetFramework()}";

        protected ITestOutputHelper Output { get; }

        public ITestOutputHelper GetOutput() => Output;

        public virtual void Dispose()
        {
        }

        public async Task<Process> StartDotnetTestSample(MockTracerAgent agent, string arguments, string packageVersion, int aspNetCorePort, string framework = "", bool forceVsTestParam = false, bool useDotnetExec = false)
        {
            // get path to sample app that the profiler will attach to
            string sampleAppPath = EnvironmentHelper.GetTestCommandForSampleApplicationPath(packageVersion, framework);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            Output.WriteLine($"Starting Application: {sampleAppPath} {arguments ?? string.Empty}");
            string testCli = forceVsTestParam || useDotnetExec ? EnvironmentHelper.GetDotnetExe() : EnvironmentHelper.GetDotNetTest();
            string exec = testCli;
            bool usesVsTest = testCli.StartsWith("dotnet") || testCli.Contains("dotnet.exe") || forceVsTestParam;
            string appPath = (useDotnetExec, usesVsTest) switch
            {
                (true, _) => $"exec {sampleAppPath}",
                (_, true) => $"vstest {sampleAppPath}",
                _ => sampleAppPath,
            };

            Output.WriteLine("Executable: " + exec);
            Output.WriteLine($"ApplicationPath: {appPath} {arguments ?? string.Empty}");
            var process = await ProfilerHelper.StartProcessWithProfiler(
                exec,
                EnvironmentHelper,
                Output,
                $"{appPath} {arguments ?? string.Empty}",
                aspNetCorePort: aspNetCorePort,
                ignoreProfilerProcessesVar: true);

            Output.WriteLine($"ProcessId: {process.Id}");

            return process;
        }

        public async Task<ProcessResult> RunDotnetTestSampleAndWaitForExit(MockTracerAgent agent, string arguments = null, string packageVersion = "", string framework = "", bool forceVsTestParam = false, int expectedExitCode = 0, bool useDotnetExec = false)
        {
            var process = await StartDotnetTestSample(agent, arguments, packageVersion, aspNetCorePort: 5000, framework: framework, forceVsTestParam: forceVsTestParam, useDotnetExec);

            using var helper = new ProcessHelper(process);
            return WaitForProcessResult(helper, expectedExitCode, dumpChildProcesses: true);
        }

        public async Task<Process> StartSample(ITestOutputHelper output, string arguments, string packageVersion, int aspNetCorePort, string framework = "", bool? enableSecurity = null, string externalRulesFile = null, bool usePublishWithRID = false, string dotnetRuntimeArgs = null)
        {
            // get path to sample app that the profiler will attach to
            var sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework, usePublishWithRID);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            var runtimeArgs = string.Empty;
            if (!string.IsNullOrEmpty(dotnetRuntimeArgs))
            {
                if (!EnvironmentHelper.IsCoreClr() || usePublishWithRID)
                {
                    throw new Exception($"Cannot use {nameof(dotnetRuntimeArgs)} with .NET Framework or when publishing with RID");
                }

                runtimeArgs = $"{dotnetRuntimeArgs} ";
            }

            Output.WriteLine($"Starting Application: {sampleAppPath}");
            var executable = EnvironmentHelper.IsCoreClr() && !usePublishWithRID ? EnvironmentHelper.GetSampleExecutionSource() : sampleAppPath;
            var args = EnvironmentHelper.IsCoreClr() && !usePublishWithRID ? $"{runtimeArgs}{sampleAppPath} {arguments ?? string.Empty}" : arguments;

            var process = await ProfilerHelper.StartProcessWithProfiler(
                executable,
                EnvironmentHelper,
                Output,
                args,
                aspNetCorePort: aspNetCorePort,
                processToProfile: executable,
                enableSecurity: enableSecurity,
                externalRulesFile: externalRulesFile,
                ignoreProfilerProcessesVar: usePublishWithRID);

            Output.WriteLine($"ProcessId: {process.Id}");

            return process;
        }

        public async Task<ProcessResult> RunSampleAndWaitForExit(string arguments = null, string packageVersion = "", string framework = "", int aspNetCorePort = 5000, bool usePublishWithRID = false, string dotnetRuntimeArgs = null)
        {
            var process = await StartSample(Output, arguments, packageVersion, aspNetCorePort: aspNetCorePort, framework: framework, usePublishWithRID: usePublishWithRID, dotnetRuntimeArgs: dotnetRuntimeArgs);
            using var helper = new ProcessHelper(process);

            return WaitForProcessResult(helper);
        }

        public ProcessResult WaitForProcessResult(ProcessHelper helper, int expectedExitCode = 0, bool dumpChildProcesses = false)
        {
            // this is _way_ too long, but we want to be v. safe
            // the goal is just to make sure we kill the test before
            // the whole CI run times out
            var process = helper.Process;
            var timeoutMs = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            var ranToCompletion = process.WaitForExit(timeoutMs) && helper.Drain(timeoutMs / 2);

            var standardOutput = helper.StandardOutput;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            var standardError = helper.ErrorOutput;

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

            if (!ranToCompletion && !process.HasExited)
            {
                var tookMemoryDump = MemoryDumpHelper.CaptureMemoryDump(process, includeChildProcesses: dumpChildProcesses);
                process.Kill();
                throw new Exception($"The sample did not exit in {timeoutMs}ms. Memory dump taken: {tookMemoryDump}. Killing process.");
            }

            var exitCode = process.ExitCode;

            Output.WriteLine($"ProcessId: " + process.Id);
            Output.WriteLine($"Exit Code: " + exitCode);

            ErrorHelpers.CheckForKnownSkipConditions(Output, exitCode, standardError, EnvironmentHelper);

            ExitCodeException.ThrowIfNonExpected(exitCode, expectedExitCode, standardError);

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }

        public async Task<(ProcessHelper Process, string ConfigFile)> StartIISExpress(
            int iisPort, IisAppType appType, string subAppPath, bool usePartialTrust, bool useLegacyCasModel)
        {
            var iisExpress = EnvironmentHelper.GetIisExpressPath();

            var appPool = appType switch
            {
                IisAppType.AspNetClassic => "Clr4ClassicAppPool",
                IisAppType.AspNetIntegrated => "Clr4IntegratedAppPool",
                IisAppType.AspNetCoreInProcess => "UnmanagedClassicAppPool",
                IisAppType.AspNetCoreOutOfProcess => "UnmanagedClassicAppPool",
                _ => throw new InvalidOperationException($"Unknown {nameof(IisAppType)} '{appType}'"),
            };

            var appPath = appType switch
            {
                IisAppType.AspNetClassic => EnvironmentHelper.GetSampleProjectDirectory(),
                IisAppType.AspNetIntegrated => EnvironmentHelper.GetSampleProjectDirectory(),
                IisAppType.AspNetCoreInProcess => EnvironmentHelper.GetSampleApplicationOutputDirectory(),
                IisAppType.AspNetCoreOutOfProcess => EnvironmentHelper.GetSampleApplicationOutputDirectory(),
                _ => throw new InvalidOperationException($"Unknown {nameof(IisAppType)} '{appType}'"),
            };

            var configTemplate = File.ReadAllText("applicationHost.config");

            var newConfig = Path.GetTempFileName();

            var virtualAppSection = subAppPath switch
            {
                null or "" or "/" => string.Empty,
                _ when !subAppPath.StartsWith("/") => throw new ArgumentException("Application path must start with '/'", nameof(subAppPath)),
                _ when subAppPath.EndsWith("/") => throw new ArgumentException("Application path must not end with '/'", nameof(subAppPath)),
                _ => $"<application path=\"{subAppPath}\" applicationPool=\"{appPool}\"><virtualDirectory path=\"/\" physicalPath=\"{appPath}\" /></application>",
            };

            configTemplate = configTemplate
                            .Replace("[PATH]", appPath)
                            .Replace("[PORT]", iisPort.ToString())
                            .Replace("[POOL]", appPool)
                            .Replace("[VIRTUAL_APPLICATION]", virtualAppSection);

            var isAspNetCore = appType == IisAppType.AspNetCoreInProcess || appType == IisAppType.AspNetCoreOutOfProcess;
            if (isAspNetCore)
            {
                var hostingModel = appType == IisAppType.AspNetCoreInProcess ? "inprocess" : "outofprocess";
                configTemplate = configTemplate
                                .Replace("[DOTNET]", EnvironmentHelper.GetDotnetExe())
                                .Replace("[RELATIVE_SAMPLE_PATH]", $".\\{EnvironmentHelper.GetSampleApplicationFileName()}")
                                .Replace("[HOSTING_MODEL]", hostingModel);
            }

            if (usePartialTrust || useLegacyCasModel)
            {
                const string defaultTrust = "<trust />";
                var trust = (usePartialTrust, useLegacyCasModel) switch
                {
                    (true, true) => """<trust level="High" legacyCasModel="true" />""",
                    (true, false) => """<trust level="High" />""",
                    (false, true) => """<trust level="Full" legacyCasModel="true" />""",
                    _ => defaultTrust,
                };

                configTemplate = configTemplate.Replace(defaultTrust, trust);
            }

            File.WriteAllText(newConfig, configTemplate);

            var args = new[]
                {
                    "/site:sample",
                    $"/config:{newConfig}",
                    "/systray:false",
                    "/trace:info"
                };

            Output.WriteLine($"[webserver] starting {iisExpress} {string.Join(" ", args)}");

            var process = await ProfilerHelper.StartProcessWithProfiler(
                iisExpress,
                EnvironmentHelper,
                Output,
                arguments: string.Join(" ", args),
                redirectStandardInput: true,
                processToProfile: appType == IisAppType.AspNetCoreOutOfProcess ? "dotnet.exe" : iisExpress);

            var semaphore = new SemaphoreSlim(0, 1);

            var processHelper = new ProcessHelper(
                process,
                line =>
                {
                    Output.WriteLine($"[webserver][stdout] {line}");

                    if (line.Contains("IIS Express is running"))
                    {
                        semaphore.Release();
                    }
                },
                line => Output.WriteLine($"[webserver][stderr] {line}"));

            await semaphore.WaitAsync(TimeSpan.FromSeconds(10));

            // Wait for iis express to finish starting up
            var retries = 5;
            while (true)
            {
                var usedPorts = IPGlobalProperties.GetIPGlobalProperties()
                                                  .GetActiveTcpListeners()
                                                  .Select(ipEndPoint => ipEndPoint.Port);

                if (usedPorts.Contains(iisPort))
                {
                    break;
                }

                retries--;

                if (retries == 0)
                {
                    throw new Exception("Gave up waiting for IIS Express.");
                }

                await Task.Delay(1500);
            }

            return (processHelper, newConfig);
        }

        public void EnableRasp(bool enable = true)
        {
            SetEnvironmentVariable(ConfigurationKeys.AppSec.RaspEnabled, enable.ToString().ToLower());
        }

        public void EnableEvidenceRedaction(bool? enable = null)
        {
            if (enable != null)
            {
                SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, enable.ToString().ToLower());
            }
        }

        public void DisableObfuscationQueryString()
        {
            SetEnvironmentVariable(ConfigurationKeys.ObfuscationQueryStringRegex, string.Empty);
        }

        public void SetEnvironmentVariable(string key, string value)
        {
            EnvironmentHelper.CustomEnvironmentVariables[key] = value;
        }

        public void ConfigureContainers(params ContainerFixture[] containers)
        {
            foreach (var container in containers)
            {
                foreach (var variable in container.GetEnvironmentVariables())
                {
                    SetEnvironmentVariable(variable.Key, variable.Value);
                }
            }
        }

        protected void ValidateSpans<T>(IEnumerable<MockSpan> spans, Func<MockSpan, T> mapper, IEnumerable<T> expected)
        {
            var spanLookup = new Dictionary<T, int>();
            foreach (var span in spans)
            {
                var key = mapper(span);
                if (spanLookup.ContainsKey(key))
                {
                    spanLookup[key]++;
                }
                else
                {
                    spanLookup[key] = 1;
                }
            }

            var missing = new List<T>();
            foreach (var e in expected)
            {
                var found = spanLookup.ContainsKey(e);
                if (found)
                {
                    if (--spanLookup[e] <= 0)
                    {
                        spanLookup.Remove(e);
                    }
                }
                else
                {
                    missing.Add(e);
                }
            }

            foreach (var e in missing)
            {
                Assert.Fail($"no span found for `{e}`, remaining spans: `{string.Join(", ", spanLookup.Select(kvp => $"{kvp.Key}").ToArray())}`");
            }
        }

        /// <summary>
        /// NOTE: Only use this for local debugging, don't set permanently in tests
        /// We have a dedicated run that tests with debug mode enabled, so want to make
        /// sure that "normal" runs don't set this flag.
        /// </summary>
        protected void EnableDebugMode()
        {
            EnvironmentHelper.DebugModeEnabled = true;
        }

        protected void SetServiceName(string serviceName)
        {
            SetEnvironmentVariable(ConfigurationKeys.ServiceName, serviceName);
        }

        protected void SetServiceVersion(string serviceVersion)
        {
            SetEnvironmentVariable(ConfigurationKeys.ServiceVersion, serviceVersion);
        }

        /// <summary>
        /// Add a workaround for the fact that .NET Core 3.1 and .NET 5 don't
        /// support the version of alpine we're currently using, and so we need to
        /// force the correct runtime id for .NET to use
        /// </summary>
        protected void UseNativeLibraryAlpineWorkaround()
        {
#if NETCOREAPP3_1 || NET5_0
            if (EnvironmentHelper.IsAlpine())
            {
                var rid = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => "linux-musl-arm64",
                    Architecture.X64 => "linux-musl-x64",
                    _ => throw new Exception("Unknown architecture " + RuntimeInformation.ProcessArchitecture)
                };
                SetEnvironmentVariable("DOTNET_RUNTIME_ID", rid);
            }
#endif

        }

        protected void SetSecurity(bool security)
        {
            SecurityEnabled = security;
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSec.Enabled, security ? "true" : "false");
        }

        protected void SetInstrumentationVerification()
        {
            bool verificationEnabled = ShouldUseInstrumentationVerification();

            if (verificationEnabled)
            {
                SetEnvironmentVariable(ConfigurationKeys.LogDirectory, EnvironmentHelper.LogDirectory);
            }
        }

        protected void VerifyInstrumentation(Process process)
        {
            if (!ShouldUseInstrumentationVerification())
            {
                return;
            }

            var logDirectory = EnvironmentHelper.LogDirectory;
            InstrumentationVerification.VerifyInstrumentation(process, logDirectory);
        }

        protected bool ShouldUseInstrumentationVerification()
        {
            if (!EnvironmentTools.IsWindows())
            {
                // Instrumentation Verification is currently only supported only on Windows
                return false;
            }

            // verify instrumentation adds a lot of time to tests so we only run it on azure and if it a scheduled build.
            // Return 'true' to verify instrumentation on local machine.
            // return true;
            return EnvironmentHelper.IsRunningInAzureDevOps() && EnvironmentHelper.IsScheduledBuild();
        }

        /// <summary>
        /// Creates a new <see cref="HttpRequestMessage"/> to use in the test.
        /// Derived tests can override this to customize the request (e.g. add headers).
        /// </summary>
        protected virtual HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string requestUri, DateTimeOffset testStart)
        {
            return new HttpRequestMessage(method, requestUri);
        }

        private bool IsServerSpan(MockSpan span) =>
            span.Tags.GetValueOrDefault(Tags.SpanKind) == SpanKinds.Server;
    }
}
