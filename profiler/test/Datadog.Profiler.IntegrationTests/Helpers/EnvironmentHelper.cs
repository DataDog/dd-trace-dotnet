// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class EnvironmentHelper
    {
        private readonly string _framework;

        public EnvironmentHelper(string framework)
        {
            _framework = framework;
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

        internal void SetEnvironmentVariables(StringDictionary environmentVariables, int agentPort, int profilingExportIntervalInSeconds, string testLogDir, string testPprofDir, string serviceName, IReadOnlyDictionary<string, string> additionalEnvVars)
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

            if (!string.IsNullOrWhiteSpace(testLogDir))
            {
                environmentVariables["DD_PROFILING_LOG_DIR"] = testLogDir;
            }

            if (!string.IsNullOrWhiteSpace(testPprofDir))
            {
                environmentVariables["DD_PROFILING_OUTPUT_DIR"] = testPprofDir;
            }

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

            if (additionalEnvVars != null)
            {
                foreach (var kv in additionalEnvVars)
                {
                    environmentVariables[kv.Key] = kv.Value;
                }
            }
        }

        private static string GetProfilerGuid()
        {
            return UseNativeLoader() ? "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" : "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}";
        }

        private static bool UseNativeLoader()
        {
            var value = Environment.GetEnvironmentVariable("USE_NATIVE_LOADER");
            return string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1");
        }

        private static string GetDeployDir()
        {
            return Path.Combine(GetRootOutputDir(), "DDProf-Deploy");
        }

        private static string GetRootOutputDir()
        {
            const string BuildFolderName = "_build";

            string currentFolder = Environment.CurrentDirectory;
            int offset = currentFolder.IndexOf(BuildFolderName, StringComparison.Ordinal);
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
            // DD_TESTING_PROFILER_FOLDER is set by the CI
            var profilerFolder = Environment.GetEnvironmentVariable("DD_TESTING_PROFILER_FOLDER");
            if (profilerFolder != null && UseNativeLoader())
            {
                return Path.Combine(profilerFolder, "ContinuousProfiler");
            }

            return profilerFolder ?? GetDeployDir();
        }

        private static string GetMonitoringHomeDirectory()
        {
            // DD_TESTING_PROFILER_FOLDER is set by the CI and tests with the native loader are run only in the CI
            return Environment.GetEnvironmentVariable("DD_TESTING_PROFILER_FOLDER");
        }

        private static string GetProfilerPath()
        {
            var extension = GetOS() switch
            {
                "win" => "dll",
                "linux" => "so",
                _ => throw new PlatformNotSupportedException()
            };

            if (UseNativeLoader())
            {
                return Path.Combine(GetMonitoringHomeDirectory(), $"Datadog.AutoInstrumentation.NativeLoader.{GetPlatform()}.{extension}");
            }

            var profilerHomeFolder = GetProfilerHomeDirectory();
            var profilerBinary = $"Datadog.AutoInstrumentation.Profiler.Native.{GetPlatform()}.{extension}";
            return Path.Combine(profilerHomeFolder, profilerBinary);
        }

        private bool IsCoreClr()
        {
            return _framework.StartsWith("netcore", StringComparison.Ordinal) ||
                   _framework.StartsWith("net5", StringComparison.Ordinal);
        }
    }
}
