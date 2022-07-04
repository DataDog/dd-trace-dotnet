// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EnvironmentHelper
    {
        private static string _solutionDirectory = null;
        private readonly string _framework;
        private readonly string _appName;
        private readonly string _testId;

        public EnvironmentHelper(string appName, string framework, bool enableTracer)
        {
            _framework = framework;
            _appName = appName;
            _testId = Guid.NewGuid().ToString("n").Substring(0, 8);

            if (enableTracer)
            {
                EnableTracer();
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

        internal void EnableTracer()
        {
            AddTracerEnvironmentVariables();
        }

        internal void SetVariable(string key, string value)
        {
            CustomEnvironmentVariables[key] = value;
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
            var profilerHome = GetProfilerHomeDirectory();
            return Path.Combine(profilerHome, $"Datadog.AutoInstrumentation.Profiler.Native.{GetPlatform()}.{GetLibraryExtension()}");
        }

        internal string GetTracerNativeLibraryPath()
        {
            var tracerHome = GetTracerHomeDirectory();

            var tracerRelativePath = GetOS() switch
            {
                "win" => Path.Combine($"win-{GetPlatform()}", "Datadog.Trace.ClrProfiler.Native.dll"),
                "linux" => Path.Combine("tracer", "Datadog.Tracer.Native.so"),
                _ => throw new PlatformNotSupportedException()
            };

            return Path.Combine(tracerHome, tracerRelativePath);
        }

        internal string GenerateLoaderConfigFile()
        {
            var profilerPath = GetProfilerNativeLibraryPath();
            var tracerPath = GetTracerNativeLibraryPath();

            var loaderConfigFilePath = Path.GetTempFileName();
            using var sw = new StreamWriter(loaderConfigFilePath);

            sw.WriteLine($"PROFILER;{{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}};{GetOS()}-{GetPlatform()};{profilerPath}");
            sw.WriteLine($"TRACER;{{50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}};{GetOS()}-{GetPlatform()};{tracerPath}");
            return loaderConfigFilePath;
        }

        internal void PopulateEnvironmentVariables(StringDictionary environmentVariables, int agentPort, int profilingExportIntervalInSeconds, string serviceName)
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

            environmentVariables["DD_PROFILING_ENABLED"] = "1";
            environmentVariables["DD_TRACE_ENABLED"] = "0";

            environmentVariables["DD_TRACE_AGENT_PORT"] = agentPort.ToString();

            environmentVariables["DD_PROFILING_UPLOAD_PERIOD"] = profilingExportIntervalInSeconds.ToString();
            environmentVariables["DD_TRACE_DEBUG"] = "1";
            environmentVariables["DD_DOTNET_PROFILER_HOME"] = GetProfilerHomeDirectory();

            if (!IsRunningOnWindows())
            {
                environmentVariables["LD_PRELOAD"] = Path.Combine(GetProfilerHomeDirectory(), "Datadog.Linux.ApiWrapper.x64.so");
            }

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
            return Path.Combine(GetSolutionDirectory(), "profiler", "_build");
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

        private static string GetProfilerHomeDirectory()
        {
            // This environment variable is set in the CI (Github / AzDo)
            var monitoringHome = Environment.GetEnvironmentVariable("MonitoringHomeDirectory");
            if (!string.IsNullOrWhiteSpace(monitoringHome))
            {
                return Path.Combine(monitoringHome, "continuousprofiler");
            }

            return GetDeployDir();
        }

        private static string GetMonitoringHome()
        {
            var s = Environment.GetEnvironmentVariable("MonitoringHome");
            if (string.IsNullOrWhiteSpace(s))
            {
                return Path.Combine(GetSolutionDirectory(), "shared", "bin", "monitoring-home");
            }

            return s;
        }

        private static string GetTracerHomeDirectory()
        {
            if (IsRunningOnWindows())
            {
                return Path.Combine(GetMonitoringHome(), "tracer");
            }

            return GetMonitoringHome();
        }

        private string GetNativeLoaderPath()
        {
            var fileName = GetOS() switch
            {
                "win" => $"Datadog.AutoInstrumentation.NativeLoader.{GetPlatform()}.dll",
                "linux" => "Datadog.Trace.ClrProfiler.Native.so",
                _ => throw new PlatformNotSupportedException()
            };

            return Path.Combine(GetMonitoringHome(), fileName);
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
            // Set tracer log directory too
            CustomEnvironmentVariables["DD_TRACE_LOG_DIRECTORY"] = Path.Combine(baseOutputDir, "logs");
            CustomEnvironmentVariables[EnvironmentVariables.ProfilingPprofDir] = Path.Combine(baseOutputDir, "pprofs");
        }

        private bool IsCoreClr()
        {
            return _framework.StartsWith("netcore", StringComparison.Ordinal) ||
                   !_framework.StartsWith("net4", StringComparison.Ordinal);
        }
    }
}
