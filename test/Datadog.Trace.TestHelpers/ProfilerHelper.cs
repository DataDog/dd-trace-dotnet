using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        private static string dotNetCoreExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";

        public static Process StartProcessWithProfiler(
            string appPath,
            IEnumerable<string> integrationPaths,
            string arguments = null,
            bool redirectStandardInput = false,
            int traceAgentPort = 9696)
        {
            if (appPath == null)
            {
                throw new ArgumentNullException(nameof(appPath));
            }

            if (integrationPaths == null)
            {
                throw new ArgumentNullException(nameof(integrationPaths));
            }

            // clear all relevant environment variables to start with a clean slate
            ClearProfilerEnvironmentVariables();

            ProcessStartInfo startInfo;

            if (EnvironmentMetadata.IsCoreClr())
            {
                // .NET Core
                startInfo = new ProcessStartInfo(dotNetCoreExecutable, $"{appPath} {arguments ?? string.Empty}");
                SetEnvironmentForDotNetCore(startInfo.EnvironmentVariables, processPath: dotNetCoreExecutable);
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(appPath, $"{arguments ?? string.Empty}");
                string executableFileName = Path.GetFileName(appPath);
                SetEnvironmentForDotNetFramework(startInfo.EnvironmentVariables, processPath: executableFileName);
            }

            SetSharedEnvironmentVariables(traceAgentPort, startInfo.EnvironmentVariables, integrationPaths);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            return Process.Start(startInfo);
        }

        public static void SetSharedEnvironmentVariables(int traceAgentPort, StringDictionary environmentVariables, IEnumerable<string> integrationPaths)
        {
            string integrations = string.Join(";", integrationPaths);
            environmentVariables["DD_INTEGRATIONS"] = integrations;
            environmentVariables["DD_TRACE_AGENT_HOSTNAME"] = "localhost";
            environmentVariables["DD_TRACE_AGENT_PORT"] = traceAgentPort.ToString();

            // for ASP.NET Core sample apps, set the server's port
            environmentVariables["ASPNETCORE_URLS"] = $"http://localhost:{traceAgentPort}/";

            foreach (var name in new string[] { "REDIS_HOST" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                {
                    environmentVariables[name] = value;
                }
            }
        }

        public static string GetProfilerDllPath()
        {
            return Path.Combine(
                EnvironmentMetadata.GetSolutionDirectory(),
                "src",
                "Datadog.Trace.ClrProfiler.Native",
                "bin",
                EnvironmentMetadata.GetBuildConfiguration(),
                EnvironmentMetadata.GetPlatform(),
                "Datadog.Trace.ClrProfiler.Native." + (EnvironmentMetadata.GetOS() == "win" ? "dll" : "so"));
        }

        public static void SetEnvironmentForDotNetCore(StringDictionary environmentVariables, string processPath)
        {
            string profilerDllPath = GetProfilerDllPath();
            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            environmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
            environmentVariables["CORECLR_PROFILER"] = Instrumentation.ProfilerClsid;
            environmentVariables["CORECLR_PROFILER_PATH"] = profilerDllPath;
            environmentVariables["DD_PROFILER_PROCESSES"] = processPath;
        }

        public static void SetEnvironmentForDotNetFramework(StringDictionary environmentVariables, string processPath)
        {
            string profilerDllPath = GetProfilerDllPath();
            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            environmentVariables["COR_ENABLE_PROFILING"] = "1";
            environmentVariables["COR_PROFILER"] = Instrumentation.ProfilerClsid;
            environmentVariables["COR_PROFILER_PATH"] = profilerDllPath;
            environmentVariables["DD_PROFILER_PROCESSES"] = processPath;
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
                                           "DD_INTEGRATIONS",
                                           "DATADOG_PROFILER_PROCESSES",
                                           "DATADOG_INTEGRATIONS",
                                       };

            foreach (string variable in environmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }
}
