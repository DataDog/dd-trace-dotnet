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
            TracerHome = GetTracerHomePath();

            // The Tracer is not currently utilizing the Native Loader in production. It is only being used in the Continuous Profiler beta.
            // To make sure we are covering both scenarios, we test WITH the native loader on PR builds, and WITHOUT it after integrating into the `master`.
            bool performComprehensiveTesting = string.Equals("true", Environment.GetEnvironmentVariable("perform_comprehensive_testing"), StringComparison.InvariantCultureIgnoreCase);
            ProfilerPath = performComprehensiveTesting ?  GetTracerNativeDLLPath() : GetNativeLoaderPath();

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

        public TestTransports TransportType { get; set; } = TestTransports.Tcp;

        public bool AutomaticInstrumentationEnabled { get; private set; } = true;

        public bool DebugModeEnabled { get; set; }

        public Dictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public string SampleName { get; }

        public string ProfilerPath { get; }

        public string TracerHome { get; }

        public string FullSampleName => $"{_appNamePrepend}{SampleName}";

        public static bool IsNet5()
        {
            return Environment.Version.Major >= 5;
        }

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core") || IsNet5();
        }

        public static string GetTracerHomePath()
        {
            var tracerHomeDirectoryEnvVar = "TracerHomeDirectory";
            var tracerHome = Environment.GetEnvironmentVariable(tracerHomeDirectoryEnvVar);
            if (string.IsNullOrEmpty(tracerHome))
            {
                // default
                return Path.Combine(
                    GetMonitoringHomePath(),
                    "Tracer");
            }

            if (!Directory.Exists(tracerHome))
            {
                throw new InvalidOperationException($"{tracerHomeDirectoryEnvVar} was set to '{tracerHome}', but directory does not exist");
            }

            // basic verification
            var tfmDirectory = EnvironmentTools.GetTracerTargetFrameworkDirectory();
            var dllLocation = Path.Combine(tracerHome, tfmDirectory);
            if (!Directory.Exists(dllLocation))
            {
                throw new InvalidOperationException($"{tracerHomeDirectoryEnvVar} was set to '{tracerHome}', but location does not contain expected folder '{tfmDirectory}'");
            }

            return tracerHome;
        }

        public static string GetMonitoringHomePath()
        {
            // default
            return Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "shared",
                "bin",
                "monitoring-home");
        }

        public static string GetNativeLoaderPath()
        {
            var monitoringHome = GetMonitoringHomePath();

            string fileName = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform()) switch
            {
                ("win", "X64")     => "Datadog.AutoInstrumentation.NativeLoader.x64.dll",
                ("win", "X86")     => "Datadog.AutoInstrumentation.NativeLoader.x86.dll",
                ("linux", "X64")   => "Datadog.AutoInstrumentation.NativeLoader.so",
                ("linux", "Arm64") => "Datadog.AutoInstrumentation.NativeLoader.so",
                ("osx", _)         => throw new PlatformNotSupportedException("The Native Loader is not yet supported on osx"),
                _ => throw new PlatformNotSupportedException()
            };

            var path = Path.Combine(monitoringHome, fileName);

            if (!File.Exists(path))
            {
                throw new Exception($"Unable to find Native Loader at {path}");
            }

            return path;
        }

        public static string GetTracerNativeDLLPath()
        {
            var tracerHome = GetTracerHomePath();

            var (extension, dir) = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform()) switch
            {
                ("win", "X64") => ("dll", "win-x64"),
                ("win", "X86") => ("dll", "win-x86"),
                ("linux", "X64") => ("so", null),
                ("linux", "Arm64") => ("so", null),
                ("osx", _) => ("dylib", null),
                _ => throw new PlatformNotSupportedException()
            };

            var fileName = $"Datadog.Trace.ClrProfiler.Native.{extension}";

            var path = dir is null
                           ? Path.Combine(tracerHome, fileName)
                           : Path.Combine(tracerHome, dir, fileName);

            if (!File.Exists(path))
            {
                throw new Exception($"Unable to find profiler at {path}");
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
            bool enableSecurity = false,
            bool enableBlocking = false,
            string externalRulesFile = null)
        {
            string profilerEnabled = AutomaticInstrumentationEnabled ? "1" : "0";
            environmentVariables["DD_DOTNET_TRACER_HOME"] = TracerHome;

            if (IsCoreClr())
            {
                environmentVariables["CORECLR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["CORECLR_PROFILER"] = EnvironmentTools.ProfilerClsId;
                environmentVariables["CORECLR_PROFILER_PATH"] = ProfilerPath;
            }
            else
            {
                environmentVariables["COR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["COR_PROFILER"] = EnvironmentTools.ProfilerClsId;
                environmentVariables["COR_PROFILER_PATH"] = ProfilerPath;
            }

            if (DebugModeEnabled)
            {
                environmentVariables["DD_TRACE_DEBUG"] = "1";
            }

            if (!string.IsNullOrEmpty(processToProfile))
            {
                environmentVariables["DD_PROFILER_PROCESSES"] = Path.GetFileName(processToProfile);
            }

            // for ASP.NET Core sample apps, set the server's port
            environmentVariables["ASPNETCORE_URLS"] = $"http://127.0.0.1:{aspNetCorePort}/";

            if (enableSecurity)
            {
                environmentVariables[ConfigurationKeys.AppSecEnabled] = enableSecurity.ToString();
            }

            if (!string.IsNullOrEmpty(externalRulesFile))
            {
                environmentVariables[ConfigurationKeys.AppSecRules] = externalRulesFile;
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
            environmentVariables[ConfigurationKeys.Telemetry.Enabled] = "false";

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
            if (TransportType == TestTransports.Uds)
            {
                string apmKey = "DD_APM_RECEIVER_SOCKET";
                string dsdKey = "DD_DOGSTATSD_SOCKET";

                environmentVariables.Add(apmKey, agent.TracesUdsPath);
                environmentVariables.Add(dsdKey, agent.StatsUdsPath);
            }
            else if (TransportType == TestTransports.WindowsNamedPipe)
            {
                string apmKey = "DD_TRACE_PIPE_NAME";
                string dsdKey = "DD_DOGSTATSD_PIPE_NAME";

                environmentVariables.Add(apmKey, agent.TracesUdsPath);
                environmentVariables.Add(dsdKey, agent.StatsUdsPath);
            }
            else if (TransportType == TestTransports.Tcp)
            {
                environmentVariables["DD_TRACE_AGENT_HOSTNAME"] = "127.0.0.1";
                environmentVariables["DD_TRACE_AGENT_PORT"] = agent.Port.ToString();

                if (agent.StatsdPort != default(int))
                {
                    environmentVariables["DD_DOGSTATSD_PORT"] = agent.StatsdPort.ToString();
                }
            }
        }

        public string GetSampleApplicationPath(string packageVersion = "", string framework = "")
        {
            var appFileName = GetSampleApplicationFileName();
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(packageVersion: packageVersion, framework: framework), appFileName);
            return sampleAppPath;
        }

        public string GetSampleApplicationFileName()
        {
            string extension = "exe";

            if (IsCoreClr() || _samplesDirectory.Contains("aspnet"))
            {
                extension = "dll";
            }

            return $"{FullSampleName}.{extension}";
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
            => $"C:\\Program Files{(Environment.Is64BitProcess ? string.Empty : " (x86)")}\\IIS Express\\iisexpress.exe";

        public string GetDotNetTest()
        {
            if (EnvironmentTools.IsWindows() && !IsCoreClr())
            {
                string filePattern = @"C:\Program Files (x86)\Microsoft Visual Studio\{0}\{1}\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
                List<Tuple<string, string>> lstTuple = new List<Tuple<string, string>>
                {
                    Tuple.Create("2019", "Enterprise"),
                    Tuple.Create("2019", "Professional"),
                    Tuple.Create("2019", "Community"),
                    Tuple.Create("2017", "Enterprise"),
                    Tuple.Create("2017", "Professional"),
                    Tuple.Create("2017", "Community"),
                };

                foreach (Tuple<string, string> tuple in lstTuple)
                {
                    var tryPath = string.Format(filePattern, tuple.Item1, tuple.Item2);
                    if (File.Exists(tryPath))
                    {
                        return tryPath;
                    }
                }
            }

            return GetDotnetExe();
        }

        public string GetDotnetExe()
            => (EnvironmentTools.IsWindows(), Environment.Is64BitProcess) switch
            {
                (true, true) => "dotnet.exe",
                (true, false) => Environment.GetEnvironmentVariable("DOTNET_EXE_32") ??
                                 Path.Combine(
                                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                     "dotnet",
                                     "dotnet.exe"),
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

        public string GetSampleApplicationOutputDirectory(string packageVersion = "", string framework = "", bool usePublishFolder = true)
        {
            var targetFramework = string.IsNullOrEmpty(framework) ? GetTargetFramework() : framework;
            var binDir = Path.Combine(
                GetSampleProjectDirectory(),
                "bin");

            string outputDir;

            if (_samplesDirectory.Contains("aspnet"))
            {
                outputDir = Path.Combine(
                    binDir,
                    EnvironmentTools.GetBuildConfiguration(),
                    "publish");
            }
            else if (EnvironmentTools.GetOS() == "win")
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetPlatform(),
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework);
            }
            else if (usePublishFolder)
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework,
                    "publish");
            }
            else
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework);
            }

            return outputDir;
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
            TransportType = TestTransports.WindowsNamedPipe;
        }

        public void EnableDefaultTransport()
        {
            TransportType = TestTransports.Tcp;
        }

        public void EnableUnixDomainSockets()
        {
#if NETCOREAPP
            TransportType = TestTransports.Uds;
#else
            // Unsupported
            throw new NotSupportedException("UDS is not supported in non-netcore applications");
#endif
        }

        public MockTracerAgent GetMockAgent(bool useStatsD = false, int? fixedPort = null)
        {
            MockTracerAgent agent = null;

#if NETCOREAPP
            // Decide between transports
            if (TransportType == TestTransports.Uds)
            {
                var tracesUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                var metricsUdsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                agent = new MockTracerAgent(new UnixDomainSocketConfig(tracesUdsPath, metricsUdsPath) { UseDogstatsD = useStatsD });
            }
            else if (TransportType == TestTransports.WindowsNamedPipe)
            {
                agent = new MockTracerAgent(new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}") { UseDogstatsD = useStatsD });
            }
            else
            {
                // Default
                var agentPort = fixedPort ?? TcpPortProvider.GetOpenPort();
                agent = new MockTracerAgent(agentPort, useStatsd: useStatsD);
            }
#else
            if (TransportType == TestTransports.WindowsNamedPipe)
            {
                agent = new MockTracerAgent(new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}"));
            }
            else
            {
                // Default
                var agentPort = fixedPort ?? TcpPortProvider.GetOpenPort();
                agent = new MockTracerAgent(agentPort, useStatsd: useStatsD);
            }
#endif

            _output.WriteLine($"Agent listener info: {agent.ListenerInfo}");

            return agent;
        }
    }
}
