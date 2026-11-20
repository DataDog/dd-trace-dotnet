// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Profiler.IntegrationTests.Xunit;
using Datadog.Profiler.SmokeTests;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EnvironmentHelper
    {
        private static string _solutionDirectory = null;
        private readonly string _framework;
        private readonly string _testOutputPath;
        private readonly bool _enableProfiler;

        public EnvironmentHelper(string framework, bool enableTracer, bool enableProfiler)
        {
            _framework = framework;
            _testOutputPath = BuildTestOutputPath(framework);
            _enableProfiler = enableProfiler;

            if (enableTracer)
            {
                EnableTracer();
            }
            else
            {
                // With Stable Configuration, it is needed to wait for the tracer to enable/disable/auto the profiler
                // So, use the kill switch to rely on env vars instead
                CustomEnvironmentVariables["DD_PROFILING_MANAGED_ACTIVATION_ENABLED"] = "0";
            }

            InitializeLogAndPprofEnvironmentVariables();
        }

        public static bool IsAlpine
        {
            get
            {
                var s = Environment.GetEnvironmentVariable("IsAlpine");
                return "true".Equals(s, StringComparison.OrdinalIgnoreCase);
            }
        }

        public Dictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public string LogDir
        {
            get
            {
                return CustomEnvironmentVariables[EnvironmentVariables.ProfilingLogDir];
            }
        }

        public string PprofDir
        {
            get
            {
                return CustomEnvironmentVariables[EnvironmentVariables.ProfilingPprofDir];
            }
        }

        public static string GetBinOutputPath()
        {
            return Path.Combine(GetRootOutputDir(), "bin");
        }

        public static bool IsRunningOnWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public static string GetPlatform()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "ARM64",
                Architecture.X86 => "x86",
                _ => "x64",
            };
        }

        public static bool IsRunningInCi() =>
            // This environment variable is set in the CI (Github / AzDo)
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MonitoringHomeDirectory"));

        internal static string GetConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        internal static void DisableDefaultProfilers(TestApplicationRunner runner)
        {
            DisableDefaultProfilers(runner.Environment);
        }

        internal static void DisableDefaultProfilers(SmokeTestRunner runner)
        {
            DisableDefaultProfilers(runner.EnvironmentHelper);
        }

        /// <summary>
        /// Must be called by any test that enables single step instrumentation (<see cref="EnvironmentVariables.SsiDeployed"/>).
        /// The single step guard rails abort instrumentation on preview runtimes, so when a <paramref name="framework"/>
        /// is in preview we have to force injection (<see cref="EnvironmentVariables.SsiInjectionForced"/>) for it,
        /// otherwise the profiler is never loaded and nothing is exported. No supported framework currently needs this.
        /// </summary>
        internal static void ForceInjectionIfRequired(TestApplicationRunner runner, string framework)
        {
            if (IsRunningOnWindows())
            {
                // in SSI on Windows log buffering is enabled, which means we don't write
                // any logs unless we inject, which makes debugging issues harder
                runner.Environment.SetVariable(EnvironmentVariables.SsiLogBufferingEnabled, "0");
            }
        }

        internal void EnableTracer()
        {
            AddTracerEnvironmentVariables();
        }

        internal void SetVariable(string key, string value)
        {
            CustomEnvironmentVariables[key] = value;
        }

        /// <summary>
        /// Enables the listed features. Every other profiler data source is disabled.
        /// Options that change how profiles are built (code hotspots, endpoint, ETW, managed activation,
        /// managed code cache...) are set only when listed: their default depends on the platform
        /// (for example the managed code cache is on by default on ARM64), so disabling them would change the test.
        /// </summary>
        internal void EnableOnly(params ProfilerFeature[] features)
        {
            var enabled = new HashSet<ProfilerFeature>();
            if (features is not null)
            {
                foreach (var feature in features)
                {
                    enabled.Add(feature);
                }
            }

            foreach (ProfilerFeature feature in Enum.GetValues(typeof(ProfilerFeature)))
            {
                if (enabled.Contains(feature))
                {
                    SetVariable(VariableName(feature), "1");
                }
                else if (IsDataSource(feature))
                {
                    SetVariable(VariableName(feature), "0");
                }
            }
        }

        internal static bool IsDataSource(ProfilerFeature feature)
        {
            switch (feature)
            {
                case ProfilerFeature.Cpu:
                case ProfilerFeature.WallTime:
                case ProfilerFeature.Exceptions:
                case ProfilerFeature.Allocations:
                case ProfilerFeature.LockContention:
                case ProfilerFeature.GarbageCollection:
                case ProfilerFeature.LiveHeap:
                case ProfilerFeature.Timeline:
                case ProfilerFeature.Network:
                case ProfilerFeature.GcThreadsCpuTime:
                case ProfilerFeature.HeapSnapshot:
                case ProfilerFeature.WaitHandleContention:
                    return true;
                default:
                    return false;
            }
        }

        internal string GetLibraryExtension()
        {
            var profilerFileNameExt = GetOS() switch
            {
                "win" => "dll",
                "linux" => "so",
                _ => throw new PlatformNotSupportedException()
            };

            return profilerFileNameExt;
        }

        internal string GetProfilerNativeLibraryPath()
        {
            // When running locally, we want to run with a local build of the compiler.
            // So we need to look in the deploy directory
            var profilerHome = IsRunningInCi() ? GetMonitoringHome() : GetDeployDir();
            return Path.Combine(profilerHome, GetArchitectureSubfolder(), $"Datadog.Profiler.Native.{GetLibraryExtension()}");
        }

        internal string GetTracerNativeLibraryPath()
        {
            var monitoringHome = GetMonitoringHome();
            return Path.Combine(monitoringHome, GetArchitectureSubfolder(), $"Datadog.Tracer.Native.{GetNativeDllExtension()}");
        }

        internal string GenerateLoaderConfigFile()
        {
            var profilerPath = GetProfilerNativeLibraryPath();
            var tracerPath = GetTracerNativeLibraryPath();
            var architecture = GetArchitectureSubfolder(IsAlpine);

            return WriteLoaderConfigFile(
                $"PROFILER;{{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}};{architecture};{profilerPath}",
                $"TRACER;{{50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}};{architecture};{tracerPath}");
        }

        internal void PopulateEnvironmentVariables(StringDictionary environmentVariables, MockDatadogAgent agent, int profilingExportIntervalInSeconds, string serviceName)
        {
            var profilerPath = GetNativeLoaderPath();

            environmentVariables["DD_NATIVELOADER_CONFIGFILE"] = GenerateLoaderConfigFile();

            if (!File.Exists(profilerPath))
            {
                throw new Exception($"Unable to find profiler dll at {profilerPath}.");
            }

            var profilerGuid = GetNativeLoaderGuid();

            if (IsCoreClr())
            {
                environmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
                environmentVariables["CORECLR_PROFILER"] = profilerGuid;
                environmentVariables["CORECLR_PROFILER_PATH"] = profilerPath;
            }
            else
            {
                environmentVariables["COR_ENABLE_PROFILING"] = "1";
                environmentVariables["COR_PROFILER"] = profilerGuid;
                environmentVariables["COR_PROFILER_PATH"] = profilerPath;
            }

            if (_enableProfiler)
            {
                environmentVariables["DD_PROFILING_ENABLED"] = "1";
            }

            // Linux ARM64: native profiler requires DD_INTERNAL_PROFILING_ENABLED_ARM64; align managed enablement.
            environmentVariables["DD_INTERNAL_PROFILING_ENABLED_ARM64"] = "1";

            // Test machines are slow and loaded enough that the default 2s is not always enough to
            // populate the libraries cache, and the profiler silently falls back to slower unwinding.
            environmentVariables[EnvironmentVariables.LibrariesInfoCacheStartTimeout] = "10000";

            // On x86 (WOW64) CI machines, the whole process can stall for a few seconds at startup, which makes
            // the default 2s watcher start timeout expire and the profiler abort its initialization.
            // Tests can still override it (e.g. "0" to force a start failure) via CustomEnvironmentVariables.
            if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                environmentVariables[EnvironmentVariables.WatcherStartTimeout] = "10000";
            }

            environmentVariables["DD_TRACE_ENABLED"] = "0";

            environmentVariables["DD_PROFILING_UPLOAD_PERIOD"] = profilingExportIntervalInSeconds.ToString();
            environmentVariables["DD_TRACE_DEBUG"] = "1";

            if (!IsRunningOnWindows())
            {
                environmentVariables["LD_PRELOAD"] = GetLinuxApiWrapperPath();
            }

            if (serviceName != null)
            {
                serviceName = serviceName.Trim();
                if (serviceName.Length > 0)
                {
                    environmentVariables["DD_SERVICE"] = serviceName;
                }
            }

            if (agent != null)
            {
                ConfigureTransportVariables(environmentVariables, agent);
            }

            foreach (var key in CustomEnvironmentVariables.Keys)
            {
                environmentVariables[key] = CustomEnvironmentVariables[key];
            }
        }

        internal string GetTestOutputPath()
        {
            return _testOutputPath;
        }

        private static string VariableName(ProfilerFeature feature)
        {
            switch (feature)
            {
                case ProfilerFeature.Cpu:
                    return EnvironmentVariables.CpuProfilerEnabled;
                case ProfilerFeature.WallTime:
                    return EnvironmentVariables.WallTimeProfilerEnabled;
                case ProfilerFeature.Exceptions:
                    return EnvironmentVariables.ExceptionProfilerEnabled;
                case ProfilerFeature.Allocations:
                    return EnvironmentVariables.AllocationProfilerEnabled;
                case ProfilerFeature.LockContention:
                    return EnvironmentVariables.ContentionProfilerEnabled;
                case ProfilerFeature.GarbageCollection:
                    return EnvironmentVariables.GarbageCollectionProfilerEnabled;
                case ProfilerFeature.LiveHeap:
                    return EnvironmentVariables.LiveHeapProfilerEnabled;
                case ProfilerFeature.Timeline:
                    return EnvironmentVariables.ThreadLifetimeEnabled;
                case ProfilerFeature.Network:
                    return EnvironmentVariables.HttpProfilingEnabled;
                case ProfilerFeature.CodeHotspots:
                    return EnvironmentVariables.CodeHotSpotsEnable;
                case ProfilerFeature.Endpoint:
                    return EnvironmentVariables.EndpointProfilerEnabled;
                case ProfilerFeature.TimestampsAsLabel:
                    return EnvironmentVariables.TimestampsAsLabelEnabled;
                case ProfilerFeature.DebugInfo:
                    return EnvironmentVariables.DebugInfoEnabled;
                case ProfilerFeature.GcThreadsCpuTime:
                    return EnvironmentVariables.GcThreadsCpuTimeEnabled;
                case ProfilerFeature.Etw:
                    return EnvironmentVariables.EtwEnabled;
                case ProfilerFeature.HeapSnapshot:
                    return EnvironmentVariables.HeapSnapshotEnabled;
                case ProfilerFeature.HeapSnapshotSkipTraversal:
                    return EnvironmentVariables.HeapSnapshotSkipTraversal;
                case ProfilerFeature.WaitHandleContention:
                    return EnvironmentVariables.WaitHandleContentionProfilingEnabled;
                case ProfilerFeature.ForceHttpSampling:
                    return EnvironmentVariables.ForceHttpSamplingEnabled;
                case ProfilerFeature.ManagedActivation:
                    return EnvironmentVariables.ManagedActivationEnabled;
                case ProfilerFeature.ManagedCodeCache:
                    return EnvironmentVariables.UseManagedCodeCache;
                case ProfilerFeature.MemoryFootprint:
                    return EnvironmentVariables.MemoryFootprintEnabled;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown profiler feature.");
            }
        }

        private static void DisableDefaultProfilers(EnvironmentHelper env)
        {
            env.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            env.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            env.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            env.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "0");
            env.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "0");
            env.SetVariable(EnvironmentVariables.GcThreadsCpuTimeEnabled, "0");
            env.SetVariable(EnvironmentVariables.ThreadLifetimeEnabled, "0");
        }

        private static string BuildTestOutputPath(string framework)
        {
            // DD_TESTING_OUPUT_DIR is set by the CI
            var baseTestOutputDir = Environment.GetEnvironmentVariable("DD_TESTING_OUPUT_DIR") ?? Path.Combine(Path.GetTempPath(), "ProfilerTest");

            var testName = TestContext.Current.TestName ?? "UnknownTestClass.UnknownTestMethod";
            var testOutputPath = Path.Combine(testName.Split('.'));

            testOutputPath = Path.Combine(baseTestOutputDir, testOutputPath, framework);

            // needed only for local test to ensure that we do not have artifacts from previous runs
            if (Directory.Exists(testOutputPath))
            {
                Directory.Delete(testOutputPath, recursive: true);
            }

            return testOutputPath;
        }

        private static void ConfigureTransportVariables(StringDictionary environmentVariables, MockDatadogAgent agent)
        {
            var envVars = agent switch
            {
                MockDatadogAgent.NamedPipeAgent np => new Dictionary<string, string>
                {
                    { "DD_TRACE_PIPE_NAME", np.ProfilesPipeName },
                },
                MockDatadogAgent.HttpAgent http => new Dictionary<string, string>
                {
                    { "DD_TRACE_AGENT_HOSTNAME", "127.0.0.1" },
                    { "DD_TRACE_AGENT_PORT", http.Port.ToString() },
                },
                _ => throw new InvalidOperationException($"Unknown MockDatadogAgent type {agent?.GetType()}")
            };

            foreach (var envVar in envVars)
            {
                environmentVariables[envVar.Key] = envVar.Value;
            }
        }

        private static string GetNativeLoaderGuid()
        {
            return "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        }

        private static string GetDeployDir()
        {
            return Path.Combine(GetRootOutputDir(), "DDProf-Deploy");
        }

        private static string GetRootOutputDir()
        {
            return Path.Combine(GetSolutionDirectory(), "artifacts", "profiler-build");
        }

        /// <summary>
        /// Find the solution directory from anywhere in the hierarchy.
        /// </summary>
        /// <returns>The solution directory.</returns>
        private static string GetSolutionDirectory()
        {
            if (_solutionDirectory == null)
            {
                var startDirectory = Environment.CurrentDirectory;
                var currentDirectory = Directory.GetParent(startDirectory);
                const string searchItem = @"Datadog.Profiler.sln";

                while (true)
                {
                    var slnFile = currentDirectory.GetFiles(searchItem).SingleOrDefault();

                    if (slnFile != null)
                    {
                        break;
                    }

                    currentDirectory = currentDirectory.Parent;

                    if (currentDirectory == null || !currentDirectory.Exists)
                    {
                        throw new Exception($"Unable to find solution directory from: {startDirectory}");
                    }
                }

                _solutionDirectory = currentDirectory.FullName;
            }

            return _solutionDirectory;
        }

        private static string GetOS()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                   string.Empty;
        }

        private static string GetMonitoringHome()
        {
            var s = Environment.GetEnvironmentVariable("MonitoringHome");
            if (string.IsNullOrWhiteSpace(s))
            {
                return Path.Combine(GetSolutionDirectory(), "artifacts", "monitoring-home");
            }

            return s;
        }

        private static string GetArchitectureSubfolder()
            => GetArchitectureSubfolder(IsAlpine);

        private static string GetArchitectureSubfolder(bool isAlpine)
            => (GetOS(), GetPlatform(), isAlpine) switch
            {
                ("win", "x64", _) => "win-x64",
                ("win", "x86", _) => "win-x86",
                ("linux", "x64", false) => "linux-x64",
                ("linux", "x64", true) => "linux-musl-x64",
                ("linux", "ARM64", false) => "linux-arm64",
                ("linux", "ARM64", true) => "linux-musl-arm64",
                ("osx", _, _) => "osx-x64",
                _ => throw new PlatformNotSupportedException()
            };

        private string GetNativeLoaderPath()
        {
            if (!IsRunningInCi())
            {
                // native loader output folder
                var binFolder = Path.Combine(GetSolutionDirectory(), "artifacts", "native-bin", "Datadog.Trace.ClrProfiler.Native");

                return GetOS() switch
                {
                    "linux" => Path.Combine(binFolder, "Datadog.Trace.ClrProfiler.Native.so"),
                    "win" => Path.Combine(binFolder, GetConfiguration(), GetPlatform(), "Datadog.Trace.ClrProfiler.Native.dll"),
                    _ => throw new PlatformNotSupportedException(),
                };
            }

            return Path.Combine(GetMonitoringHome(), GetArchitectureSubfolder(), $"Datadog.Trace.ClrProfiler.Native.{GetNativeDllExtension()}");
        }

        private string GetLinuxApiWrapperPath()
        {
            var filename = "Datadog.Linux.ApiWrapper.x64.so";
            var deployDir = IsRunningInCi() ? GetMonitoringHome() : GetDeployDir();
            return Path.Combine(deployDir, GetArchitectureSubfolder(), filename);
        }

        private string GetNativeDllExtension()
            => GetOS() switch
            {
                "win" => "dll",
                "linux" => "so",
                "osx" => "dylib",
                _ => throw new PlatformNotSupportedException()
            };

        private void AddTracerEnvironmentVariables()
        {
            // Temporarily disable tiered compilation
            CustomEnvironmentVariables["COMPlus_TieredCompilation"] = "0";

            CustomEnvironmentVariables["DD_TRACE_ENABLED"] = "1";
            CustomEnvironmentVariables["DD_DOTNET_TRACER_HOME"] = GetMonitoringHome();
        }

        private void InitializeLogAndPprofEnvironmentVariables()
        {
            var baseOutputDir = GetTestOutputPath();
            CustomEnvironmentVariables[EnvironmentVariables.ProfilingLogDir] = Path.Combine(baseOutputDir, "logs");
            CustomEnvironmentVariables[EnvironmentVariables.ProfilingPprofDir] = Path.Combine(baseOutputDir, "pprofs");
        }

        private string WriteLoaderConfigFile(string profilerLine, string tracerLine)
        {
            // Leave the file in the per-test output directory so CI uploads it with the logs.
            // A test that starts several processes needs a distinct file for each one.
            // CreateNew reserves the name before the write, so concurrent launches cannot share a path.
            Directory.CreateDirectory(_testOutputPath);

            for (var index = 1; ; index++)
            {
                var fileName = index == 1 ? "loader.conf" : $"loader-{index}.conf";
                var path = Path.Combine(_testOutputPath, fileName);

                FileStream stream;
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(path))
                {
                    continue;
                }

                using (stream)
                {
                    using var writer = new StreamWriter(stream, leaveOpen: true);
                    writer.WriteLine(profilerLine);
                    writer.WriteLine(tracerLine);
                }

                return path;
            }
        }

        private bool IsCoreClr()
        {
            return _framework.StartsWith("netcore", StringComparison.Ordinal) ||
                   !_framework.StartsWith("net4", StringComparison.Ordinal);
        }
    }
}
