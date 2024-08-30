// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class EnvironmentHelper
    {
        private static readonly string RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

        private readonly ITestOutputHelper _output;
        private readonly int _major;
        private readonly int _minor;
        private readonly string _patch = null;

        private readonly string _appNamePrepend;
        private readonly string _runtime;
        private readonly bool _isCoreClr;
        private readonly string _samplesDirectory;
        private readonly TargetFrameworkAttribute _targetFramework;
        private TestTransports _transportType = TestTransports.Tcp;

        public EnvironmentHelper(
            string sampleName,
            Type anchorType,
            ITestOutputHelper output,
            string samplesDirectory = null,
            bool prependSamplesToAppName = true)
        {
            SampleName = sampleName;
            _samplesDirectory = samplesDirectory ?? Path.Combine("test", "test-applications", "integrations");
            _targetFramework = Assembly.GetAssembly(anchorType).GetCustomAttribute<TargetFrameworkAttribute>();
            _output = output;
            MonitoringHome = GetMonitoringHomePath();
            LogDirectory = DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance);

            var parts = _targetFramework.FrameworkName.Split(',');
            _runtime = parts[0];
            _isCoreClr = _runtime.Equals(EnvironmentTools.CoreFramework);

            var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');
            _major = int.Parse(versionParts[0]);
            _minor = int.Parse(versionParts[1]);

            if (versionParts.Length == 3)
            {
                _patch = versionParts[2];
            }

            _appNamePrepend = prependSamplesToAppName
                          ? "Samples."
                          : string.Empty;
        }

        public bool AutomaticInstrumentationEnabled { get; private set; } = true;

        public bool DebugModeEnabled { get; set; }

        public Dictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public string SampleName { get; }

        public string LogDirectory { get; }

        public string MonitoringHome { get; }

        public string FullSampleName => $"{_appNamePrepend}{SampleName}";

        public static bool IsNet5()
        {
            return Environment.Version.Major >= 5;
        }

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core") || IsNet5();
        }

        public static bool IsAlpine() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine"));

        public static string GetMonitoringHomePath()
        {
            var monitoringHomeDirectoryEnvVar = "MonitoringHomeDirectory";
            var monitoringHome = Environment.GetEnvironmentVariable(monitoringHomeDirectoryEnvVar);
            if (string.IsNullOrEmpty(monitoringHome))
            {
                // default
                monitoringHome = Path.Combine(
                    EnvironmentTools.GetSolutionDirectory(),
                    "shared",
                    "bin",
                    "monitoring-home");
            }

            if (!Directory.Exists(monitoringHome))
            {
                throw new InvalidOperationException($"{monitoringHomeDirectoryEnvVar} was set to '{monitoringHome}', but directory does not exist");
            }

            // basic verification
            var tfmDirectory = EnvironmentTools.GetTracerTargetFrameworkDirectory();
            var dllLocation = Path.Combine(monitoringHome, tfmDirectory);
            if (!Directory.Exists(dllLocation))
            {
                throw new InvalidOperationException($"{monitoringHomeDirectoryEnvVar} was set to '{monitoringHome}', but location does not contain expected folder '{tfmDirectory}'");
            }

            return monitoringHome;
        }

        public static string GetNativeLoaderPath()
        {
            var monitoringHome = GetMonitoringHomePath();

            var (extension, dir) = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentTools.GetTestTargetPlatform(), IsAlpine()) switch
            {
                ("win", _, "X64", _) => ("dll", "win-x64"),
                ("win", _, "X86", _) => ("dll", "win-x86"),
                ("linux", "Arm64", _, false) => ("so", "linux-arm64"),
                ("linux", "Arm64", _, true) => ("so", "linux-musl-arm64"),
                ("linux", "X64", _, false) => ("so", "linux-x64"),
                ("linux", "X64", _, true) => ("so", "linux-musl-x64"),
                ("osx", _, _, _) => ("dylib", "osx"),
                var unsupportedTarget => throw new PlatformNotSupportedException(unsupportedTarget.ToString())
            };

            var fileName = $"Datadog.Trace.ClrProfiler.Native.{extension}";
            var path = Path.Combine(monitoringHome, dir, fileName);

            if (!File.Exists(path))
            {
                throw new Exception($"Unable to find Native Loader at {path}");
            }

            return path;
        }

        public static void ClearProfilerEnvironmentVariables()
        {
            var environmentVariables = new[]
            {
                // .NET Core
                "CORECLR_ENABLE_PROFILING",
                "CORECLR_PROFILER",
                "CORECLR_PROFILER_PATH",
                "CORECLR_PROFILER_PATH_32",
                "CORECLR_PROFILER_PATH_64",

                // .NET Framework
                "COR_ENABLE_PROFILING",
                "COR_PROFILER",
                "COR_PROFILER_PATH",

                // Datadog
                "DD_PROFILER_PROCESSES",
                "DD_DOTNET_TRACER_HOME",
                "DD_DISABLED_INTEGRATIONS",
                "DD_SERVICE",
                "DD_VERSION",
                "DD_TAGS",
                "DD_APPSEC_ENABLED",
                "DD_WRITE_INSTRUMENTATION_TO_DISK"
            };

            foreach (string variable in environmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        public void SetEnvironmentVariables(
            MockTracerAgent agent,
            int aspNetCorePort,
            IDictionary<string, string> environmentVariables,
            string processToProfile = null,
            bool? enableSecurity = null,
            string externalRulesFile = null,
            bool ignoreProfilerProcessesVar = false)
        {
            string profilerEnabled = AutomaticInstrumentationEnabled ? "1" : "0";
            environmentVariables["DD_DOTNET_TRACER_HOME"] = MonitoringHome;

            // see https://github.com/DataDog/dd-trace-dotnet/pull/3579
            environmentVariables["DD_INTERNAL_WORKAROUND_77973_ENABLED"] = "1";

            // Set a canary variable that should always be ignored
            // and check that it doesn't appear in the logs
            environmentVariables["SUPER_SECRET_CANARY"] = "MySuperSecretCanary";

            // Everything should be using the native loader now
            var nativeLoaderPath = GetNativeLoaderPath();

            if (IsCoreClr())
            {
                environmentVariables["CORECLR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["CORECLR_PROFILER"] = EnvironmentTools.ProfilerClsId;
                environmentVariables["CORECLR_PROFILER_PATH"] = nativeLoaderPath;

                var apiWrapperPath = GetApiWrapperPath();

                if (!string.IsNullOrEmpty(apiWrapperPath) && !environmentVariables.ContainsKey("LD_PRELOAD"))
                {
                    if (File.Exists(apiWrapperPath))
                    {
                        environmentVariables["LD_PRELOAD"] = apiWrapperPath;
                    }
                    else if (IsRunningInAzureDevOps())
                    {
                        // For convenience, allow tests to run without LD_PRELOAD outside of CI
                        throw new Exception($"Unable to find API Wrapper at {apiWrapperPath}");
                    }
                }
            }
            else
            {
                environmentVariables["COR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["COR_PROFILER"] = EnvironmentTools.ProfilerClsId;
                environmentVariables["COR_PROFILER_PATH"] = nativeLoaderPath;
            }

            if (DebugModeEnabled)
            {
                environmentVariables["DD_TRACE_DEBUG"] = "1";
            }

            if (!string.IsNullOrEmpty(processToProfile) && !ignoreProfilerProcessesVar)
            {
                environmentVariables["DD_PROFILER_PROCESSES"] = Path.GetFileName(processToProfile);
            }

            // for ASP.NET Core sample apps, set the server's port
            environmentVariables["ASPNETCORE_URLS"] = $"http://127.0.0.1:{aspNetCorePort}/";

            if (enableSecurity != null)
            {
                environmentVariables[ConfigurationKeys.AppSec.Enabled] = enableSecurity.Value.ToString();
            }

            if (!string.IsNullOrEmpty(externalRulesFile))
            {
                environmentVariables[ConfigurationKeys.AppSec.Rules] = externalRulesFile;
            }

            // set the querystring regex to something stupidly large, as it can introduce random flake into snapshots
            if (!environmentVariables.ContainsKey("DD_TRACE_OBFUSCATION_QUERY_STRING_REGEXP_TIMEOUT"))
            {
                environmentVariables["DD_TRACE_OBFUSCATION_QUERY_STRING_REGEXP_TIMEOUT"] = 10_000_000.ToString();
            }

            if (!environmentVariables.ContainsKey("DD_IAST_REGEXP_TIMEOUT"))
            {
                environmentVariables["DD_IAST_REGEXP_TIMEOUT"] = 10_000_000.ToString();
            }

            if (!environmentVariables.ContainsKey("DD_APPSEC_WAF_TIMEOUT"))
            {
                environmentVariables["DD_APPSEC_WAF_TIMEOUT"] = 10_000_000.ToString();
            }

            foreach (var name in new[] { "SERVICESTACK_REDIS_HOST", "STACKEXCHANGE_REDIS_HOST" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                {
                    environmentVariables[name] = value;
                }
            }

            // set consistent env name (can be overwritten by custom environment variable)
            environmentVariables["DD_ENV"] = "integration_tests";
            environmentVariables[ConfigurationKeys.Telemetry.Enabled] = agent.TelemetryEnabled.ToString();
            if (agent.TelemetryEnabled)
            {
                environmentVariables[ConfigurationKeys.Telemetry.AgentlessEnabled] = "0";
            }

            // Don't attach the profiler to these processes
            environmentVariables["DD_PROFILER_EXCLUDE_PROCESSES"] =
                "devenv.exe;Microsoft.ServiceHub.Controller.exe;ServiceHub.Host.CLR.exe;ServiceHub.TestWindowStoreHost.exe;" +
                "ServiceHub.DataWarehouseHost.exe;sqlservr.exe;VBCSCompiler.exe;iisexpresstray.exe;msvsmon.exe;PerfWatson2.exe;" +
                "ServiceHub.IdentityHost.exe;ServiceHub.VSDetouredHost.exe;ServiceHub.SettingsHost.exe;ServiceHub.Host.CLR.x86.exe;" +
                "ServiceHub.RoslynCodeAnalysisService32.exe;MSBuild.exe;ServiceHub.ThreadedWaitDialog.exe";

            ConfigureTransportVariables(environmentVariables, agent);

            foreach (var key in CustomEnvironmentVariables.Keys)
            {
                environmentVariables[key] = CustomEnvironmentVariables[key];
            }
        }

        public void ConfigureTransportVariables(IDictionary<string, string> environmentVariables, MockTracerAgent agent)
        {
            var envVars = agent switch
            {
#if NETCOREAPP3_1_OR_GREATER
                MockTracerAgent.UdsAgent uds => new Dictionary<string, string>
                {
                    { "DD_APM_RECEIVER_SOCKET", uds.TracesUdsPath },
                    { "DD_DOGSTATSD_SOCKET", uds.StatsUdsPath },
                },
#endif
                MockTracerAgent.NamedPipeAgent np => new Dictionary<string, string>
                {
                    { "DD_TRACE_PIPE_NAME", np.TracesWindowsPipeName },
                    { "DD_DOGSTATSD_PIPE_NAME", np.StatsWindowsPipeName },
                },
                MockTracerAgent.TcpUdpAgent { StatsdPort: not 0 } tcp => new Dictionary<string, string>
                {
                    { "DD_TRACE_AGENT_HOSTNAME", "127.0.0.1" },
                    { "DD_TRACE_AGENT_PORT", tcp.Port.ToString() },
                    { "DD_DOGSTATSD_PORT", tcp.StatsdPort.ToString() },
                },
                MockTracerAgent.TcpUdpAgent tcp => new Dictionary<string, string>
                {
                    { "DD_TRACE_AGENT_HOSTNAME", "127.0.0.1" },
                    { "DD_TRACE_AGENT_PORT", tcp.Port.ToString() },
                },
                _ => throw new InvalidOperationException($"Unknown MockTracerAgent type {agent?.GetType()}")
            };

            foreach (var envVar in envVars)
            {
                environmentVariables[envVar.Key] = envVar.Value;
            }
        }

        public string GetSampleApplicationPath(string packageVersion = "", string framework = "", bool usePublishWithRID = false)
        {
            var appFileName = GetSampleApplicationFileName(usePublishWithRID);
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(packageVersion: packageVersion, framework: framework, usePublishWithRID: usePublishWithRID), appFileName);
            return sampleAppPath;
        }

        public string GetSampleApplicationFileName(bool usePublishWithRID = false)
        {
            if (usePublishWithRID)
            {
                return EnvironmentTools.IsWindows() ? $"{FullSampleName}.exe" : FullSampleName;
            }

            return IsCoreClr() || _samplesDirectory.Contains("aspnet") ? $"{FullSampleName}.dll" : $"{FullSampleName}.exe";
        }

        public string GetTestCommandForSampleApplicationPath(string packageVersion = "", string framework = "")
        {
            var appFileName = $"{FullSampleName}.dll";
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(packageVersion: packageVersion, framework: framework), appFileName);
            return sampleAppPath;
        }

        public string GetSampleExecutionSource()
        {
            string executor;

            if (_samplesDirectory.Contains("aspnet"))
            {
                executor = GetIisExpressPath();
            }
            else if (IsCoreClr())
            {
                executor = GetDotnetExe();
            }
            else
            {
                var appFileName = $"{FullSampleName}.exe";
                executor = Path.Combine(GetSampleApplicationOutputDirectory(), appFileName);

                if (!File.Exists(executor))
                {
                    throw new Exception($"Unable to find executing assembly at {executor}");
                }
            }

            return executor;
        }

        public string GetIisExpressPath()
            => $"C:\\Program Files{(EnvironmentTools.IsTestTarget64BitProcess() ? string.Empty : " (x86)")}\\IIS Express\\iisexpress.exe";

        public string GetDotNetTest()
        {
            if (EnvironmentTools.IsWindows() && !IsCoreClr())
            {
                string filePattern32 = @"C:\Program Files (x86)\Microsoft Visual Studio\{0}\{1}\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
                string filePattern64 = @"C:\Program Files\Microsoft Visual Studio\{0}\{1}\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
                List<Tuple<string, string>> lstTuple = new List<Tuple<string, string>>
                {
                    Tuple.Create("2022", "Enterprise"),
                    Tuple.Create("2022", "Professional"),
                    Tuple.Create("2022", "Community"),
                    Tuple.Create("2019", "Enterprise"),
                    Tuple.Create("2019", "Professional"),
                    Tuple.Create("2019", "Community"),
                    Tuple.Create("2017", "Enterprise"),
                    Tuple.Create("2017", "Professional"),
                    Tuple.Create("2017", "Community"),
                };

                foreach (Tuple<string, string> tuple in lstTuple)
                {
                    var tryPath = string.Format(filePattern32, tuple.Item1, tuple.Item2);
                    if (File.Exists(tryPath))
                    {
                        return tryPath;
                    }

                    tryPath = string.Format(filePattern64, tuple.Item1, tuple.Item2);
                    if (File.Exists(tryPath))
                    {
                        return tryPath;
                    }
                }
            }

            return GetDotnetExe();
        }

        public string GetDotnetExe()
            => (EnvironmentTools.IsWindows(), EnvironmentTools.IsTestTarget64BitProcess()) switch
            {
                (true, false) => Environment.GetEnvironmentVariable("DOTNET_EXE_32") ??
                                 Path.Combine(
                                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                     "dotnet",
                                     "dotnet.exe"),
                (true, _) => "dotnet.exe",
                _ => "dotnet",
            };

        public string GetSampleProjectDirectory()
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var projectDir = Path.Combine(
                solutionDirectory,
                "tracer",
                _samplesDirectory,
                $"{FullSampleName}");
            return projectDir;
        }

        public string GetSampleApplicationOutputDirectory(string packageVersion = "", string framework = "", bool usePublishFolder = true, bool usePublishWithRID = false)
        {
            var targetFramework = string.IsNullOrEmpty(framework) ? GetTargetFramework() : framework;
            var binDir = Path.Combine(GetSampleProjectDirectory(), "bin");
            var artifactsBinDir = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "artifacts", "bin");
            var artifactsPublishDir = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "artifacts", "publish");

            if (_samplesDirectory.Contains("aspnet"))
            {
                return Path.Combine(
                    binDir,
                    EnvironmentTools.GetBuildConfiguration(),
                    "publish");
            }
            else if (EnvironmentTools.GetOS() == "win" && !usePublishWithRID)
            {
                return Path.Combine(artifactsBinDir, FullSampleName, GetPivot());
            }
            else if (usePublishWithRID)
            {
                return Path.Combine(artifactsPublishDir, FullSampleName, GetPivot());
            }
            else if (usePublishFolder)
            {
                return Path.Combine(artifactsPublishDir, FullSampleName, GetPivot());
            }
            else
            {
                return Path.Combine(artifactsBinDir, FullSampleName, GetPivot());
            }

            string GetPivot()
            {
                var rid = (usePublishWithRID, IsAlpine()) switch
                {
                    (false, _) => string.Empty,
                    (true, false) => $"_{EnvironmentTools.GetOS()}-{(EnvironmentTools.GetPlatform() == "Arm64" ? "arm64" : "x64")}",
                    (true, true) => $"_{EnvironmentTools.GetOS()}-musl-{(EnvironmentTools.GetPlatform() == "Arm64" ? "arm64" : "x64")}",
                };
                var config = EnvironmentTools.GetBuildConfiguration().ToLowerInvariant();
                var packageVersionPivot = string.IsNullOrEmpty(packageVersion) ? string.Empty : $"_{packageVersion}";
                return $"{config}_{targetFramework}{packageVersionPivot}{rid}";
            }
        }

        public string GetTargetFramework()
        {
            if (_isCoreClr)
            {
                if (_major >= 5)
                {
                    return $"net{_major}.{_minor}";
                }

                return $"netcoreapp{_major}.{_minor}";
            }

            return $"net{_major}{_minor}{_patch ?? string.Empty}";
        }

        public void SetAutomaticInstrumentation(bool enabled)
        {
            AutomaticInstrumentationEnabled = enabled;
        }

        public void EnableWindowsNamedPipes(string tracePipeName = null, string statsPipeName = null)
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new NotSupportedException("Windows named pipes is only supported on Windows");
            }

            _transportType = TestTransports.WindowsNamedPipe;
        }

        public void EnableDefaultTransport()
        {
            _transportType = TestTransports.Tcp;
        }

        public void EnableUnixDomainSockets()
        {
#if NETCOREAPP3_1_OR_GREATER
            _transportType = TestTransports.Uds;
#else
            // Unsupported
            throw new NotSupportedException("UDS is not supported in non-netcore applications or < .NET Core 3.1 ");
#endif
        }

        public void EnableTransport(TestTransports transport)
        {
            switch (transport)
            {
                case TestTransports.Tcp:
                    EnableDefaultTransport();
                    break;
                case TestTransports.Uds:
                    EnableUnixDomainSockets();
                    break;
                case TestTransports.WindowsNamedPipe:
                    EnableWindowsNamedPipes();
                    break;
                default:
                    throw new InvalidOperationException("Unknown transport " + transport.ToString());
            }
        }

        public MockTracerAgent GetMockAgent(bool useStatsD = false, int? fixedPort = null, bool useTelemetry = false)
        {
            MockTracerAgent agent = null;

            // Decide between transports
            if (_transportType == TestTransports.Uds)
            {
#if NETCOREAPP3_1_OR_GREATER
                var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                var metricsUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                agent = MockTracerAgent.Create(_output, new UnixDomainSocketConfig(tracesUdsPath, metricsUdsPath) { UseDogstatsD = useStatsD, UseTelemetry = useTelemetry });
#else
                throw new NotSupportedException("UDS is not supported in non-netcore applications or < .NET Core 3.1 ");
#endif
            }
            else if (_transportType == TestTransports.WindowsNamedPipe)
            {
                agent = MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}") { UseDogstatsD = useStatsD, UseTelemetry = useTelemetry });
            }
            else
            {
                // Default
                var agentPort = fixedPort ?? TcpPortProvider.GetOpenPort();
                agent = MockTracerAgent.Create(_output, agentPort, useStatsd: useStatsD, useTelemetry: useTelemetry);
            }

            _output.WriteLine($"Agent listener info: {agent.ListenerInfo}");

            return agent;
        }

        public bool IsRunningInAzureDevOps()
        {
            return Environment.GetEnvironmentVariable("SYSTEM_COLLECTIONID") != null;
        }

        public bool IsScheduledBuild()
        {
            return IsEnvironmentVariableSet("isScheduledBuild");
        }

        public string GetApiWrapperPath()
        {
            var archFolder = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), IsAlpine()) switch
            {
                ("linux", "Arm64", true) => "linux-musl-arm64",
                ("linux", "Arm64", false) => "linux-arm64",
                ("linux", "X64", true) => "linux-musl-x64",
                ("linux", "X64", false) => "linux-x64",
                _ => string.Empty,
            };

            if (string.IsNullOrEmpty(archFolder))
            {
                return string.Empty;
            }

            return Path.Combine(GetMonitoringHomePath(), archFolder, "Datadog.Linux.ApiWrapper.x64.so");
        }

        private bool IsEnvironmentVariableSet(string ev)
        {
            if (string.IsNullOrEmpty(ev))
            {
                return false;
            }

            var evValue = Environment.GetEnvironmentVariable(ev);
            if (evValue == null)
            {
                CustomEnvironmentVariables.TryGetValue(ev, out evValue);
            }

            return evValue == "1" || string.Equals(evValue, "true", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
