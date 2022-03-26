// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EnvironmentHelper
    {
        private readonly string _framework;
        private readonly string _appName;
        private readonly string _testId;

        public EnvironmentHelper(string appName, string framework, bool enableNewPipeline, bool enableTracer)
        {
            _framework = framework;
            _appName = appName;
            _testId = (enableNewPipeline ? "_NewPipepline" : string.Empty) + Guid.NewGuid().ToString("n").Substring(0, 8);

            if (enableNewPipeline)
            {
                EnableNewPipeline();
            }

            if (enableTracer)
            {
                EnableTracer();
            }

            InitializeLogAndPprofEnvironmentVariables();
        }

        public static bool IsInCI
        {
            get
            {
                return Environment.GetEnvironmentVariable(EnvironmentVariables.ProfilerInstallationFolder) != null;
            }
        }

        public static bool UseNativeLoader
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("USE_NATIVE_LOADER");
                return string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1");
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
            return Environment.Is64BitProcess ? "x64" : "x86";
        }

        internal static string GetConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        internal void EnableNewPipeline()
        {
            CustomEnvironmentVariables[EnvironmentVariables.LibDdPprofPipepline] = "1";
        }

        internal void EnableTracer()
        {
            AddTracerEnvironmentVariables();
        }

        internal void SetVariable(string key, string value)
        {
            CustomEnvironmentVariables[key] = value;
        }

        internal bool IsRunningWithNewPipeline()
        {
            return CustomEnvironmentVariables.TryGetValue(EnvironmentVariables.LibDdPprofPipepline, out var value) && string.Equals("1", value);
        }

        internal void SetVariables(StringDictionary environmentVariables, int agentPort, int profilingExportIntervalInSeconds, string serviceName)
        {
            var profilerPath = GetProfilerPath();
            if (!File.Exists(profilerPath))
            {
                throw new Exception($"Unable to find profiler dll at {profilerPath}.");
            }

            var profilerGuid = GetProfilerGuid();

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

            environmentVariables["DD_PROFILING_ENABLED"] = "1";
            environmentVariables["DD_TRACE_ENABLED"] = "0";

            environmentVariables["DD_TRACE_AGENT_PORT"] = agentPort.ToString();

            environmentVariables["DD_PROFILING_UPLOAD_PERIOD"] = profilingExportIntervalInSeconds.ToString();
            environmentVariables["DD_TRACE_DEBUG"] = "1";
            environmentVariables["DD_DOTNET_PROFILER_HOME"] = GetProfilerHomeDirectory();

            if (serviceName != null)
            {
                serviceName = serviceName.Trim();
                if (serviceName.Length > 0)
                {
                    environmentVariables["DD_SERVICE"] = serviceName;
                }
            }

            foreach (var key in CustomEnvironmentVariables.Keys)
            {
                environmentVariables[key] = CustomEnvironmentVariables[key];
            }
        }

        internal string GetTestOutputPath()
        {
            // DD_TESTING_OUPUT_DIR is set by the CI
            var baseTestOutputDir = Environment.GetEnvironmentVariable("DD_TESTING_OUPUT_DIR") ?? Path.GetTempPath();
            var testOutputPath = Path.Combine(baseTestOutputDir, $"TestApplication_{_appName}{_testId}_{Process.GetCurrentProcess().Id}", _framework);

            return testOutputPath;
        }

        private static string GetProfilerGuid()
        {
            return UseNativeLoader ? "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" : "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}";
        }

        private static string GetDeployDir()
        {
            return Path.Combine(GetRootOutputDir(), "DDProf-Deploy");
        }

        private static string GetRootOutputDir()
        {
            const string BuildFolderName = "_build";

            var currentFolder = Environment.CurrentDirectory;
            var offset = currentFolder.IndexOf(BuildFolderName, StringComparison.Ordinal);

            if (offset != -1)
            {
                return currentFolder.Substring(0, offset + BuildFolderName.Length);
            }

            throw new Exception("Cannot find Root output dir '" + BuildFolderName + "'");
        }

        private static string GetOS()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                   string.Empty;
        }

        private static string GetProfilerHomeDirectory()
        {
            if (!IsInCI)
            {
                return GetDeployDir();
            }

            var monitoringOrProfilerHome = GetMonitoringHomeDirectory();

            Assert.False(
                string.IsNullOrWhiteSpace(monitoringOrProfilerHome),
                $"To run integration tests in CI, we must set the {EnvironmentVariables.ProfilerInstallationFolder} environment variable.");

            if (UseNativeLoader)
            {
                return Path.Combine(monitoringOrProfilerHome, "ContinuousProfiler");
            }

            return monitoringOrProfilerHome;
        }

        private static string GetTracerHomeDirectory()
        {
            return Path.Combine(GetMonitoringHomeDirectory(), "tracer");
        }

        private static string GetMonitoringHomeDirectory()
        {
            // DD_TESTING_PROFILER_FOLDER is set by the CI and tests with the native loader are run only in the CI
            return Environment.GetEnvironmentVariable(EnvironmentVariables.ProfilerInstallationFolder);
        }

        private string GetProfilerPath()
        {
            var extension = GetOS() switch
            {
                "win" => "dll",
                "linux" => "so",
                _ => throw new PlatformNotSupportedException()
            };

            if (UseNativeLoader)
            {
                return Path.Combine(GetMonitoringHomeDirectory(), $"Datadog.AutoInstrumentation.NativeLoader.{GetPlatform()}.{extension}");
            }

            var profilerHomeFolder = GetProfilerHomeDirectory();
            var profilerBinary = $"Datadog.AutoInstrumentation.Profiler.Native.{GetPlatform()}.{extension}";
            return Path.Combine(profilerHomeFolder, profilerBinary);
        }

        private void AddTracerEnvironmentVariables()
        {
            CustomEnvironmentVariables["DD_TRACE_ENABLED"] = "1";
            CustomEnvironmentVariables["DD_DOTNET_TRACER_HOME"] = GetTracerHomeDirectory();
        }

        private void InitializeLogAndPprofEnvironmentVariables()
        {
            var baseOutputDir = GetTestOutputPath();
            CustomEnvironmentVariables[EnvironmentVariables.ProfilingLogDir] = Path.Combine(baseOutputDir, "logs");
            CustomEnvironmentVariables[EnvironmentVariables.ProfilingPprofDir] = Path.Combine(baseOutputDir, "pprofs");
        }

        private bool IsCoreClr()
        {
            return _framework.StartsWith("netcore", StringComparison.Ordinal) ||
                   _framework.StartsWith("net5", StringComparison.Ordinal);
        }
    }
}
