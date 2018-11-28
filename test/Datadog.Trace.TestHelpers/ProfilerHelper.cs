using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        private static readonly string DotNetCoreExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";

        public static Process StartProcessWithProfiler(
            string appPath,
            bool coreClr,
            IEnumerable<string> integrationPaths,
            string profilerClsid,
            string profilerDllPath,
            string arguments = null,
            int traceAgentPort = 9696,
            bool createNoWindow = true)
        {
#pragma warning disable SA1501 // Statement must not be on a single line
            if (appPath == null) { throw new ArgumentNullException(nameof(appPath)); }
            if (integrationPaths == null) { throw new ArgumentNullException(nameof(integrationPaths)); }
            if (profilerClsid == null) { throw new ArgumentNullException(nameof(profilerClsid)); }
            if (profilerDllPath == null) { throw new ArgumentNullException(nameof(profilerDllPath)); }
            if (!File.Exists(profilerDllPath)) { throw new Exception($"Native profiler library not found in \"{profilerDllPath}\""); }
#pragma warning restore SA1501 // Statement must not be on a single line

            ProcessStartInfo startInfo;

            if (coreClr)
            {
                // .NET Core
                startInfo = new ProcessStartInfo(DotNetCoreExecutable, $"{appPath} {arguments ?? string.Empty}");
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(appPath, $"{arguments ?? string.Empty}");
            }

            // clear all relevant environment variables to start with a clean slate
            ClearProfilerEnvironmentVariables();

            // get environment variables that need to be set on the new process to enable profiling
            var environmentVariables = GetProfilerEnvironmentVariables(profilerClsid, profilerDllPath, integrationPaths, traceAgentPort);

            foreach (var keyValuePair in environmentVariables)
            {
                startInfo.EnvironmentVariables[keyValuePair.Key] = keyValuePair.Value;
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = createNoWindow;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;

            return Process.Start(startInfo);
        }

        public static IDictionary<string, string> GetProfilerEnvironmentVariables(
            string profilerClsid,
            string profilerDllPath,
            IEnumerable<string> integrationFiles,
            int traceAgentPort = 9696)
        {
            return new Dictionary<string, string>
            {
                // .NET Core
                ["CORECLR_ENABLE_PROFILING"] = "1",
                ["CORECLR_PROFILER"] = profilerClsid,
                ["CORECLR_PROFILER_PATH"] = profilerDllPath,

                // .NET Framework
                ["COR_ENABLE_PROFILING"] = "1",
                ["COR_PROFILER"] = profilerClsid,
                ["COR_PROFILER_PATH"] = profilerDllPath,

                // Datadog
                ["DD_INTEGRATIONS"] = string.Join(";", integrationFiles),
                ["DD_AGENT_HOST"] = "localhost",
                ["DD_TRACE_AGENT_PORT"] = traceAgentPort.ToString(),
            };
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
                "DD_AGENT_NAME",
                "DD_TRACE_AGENT_PORT",
            };

            foreach (string variable in environmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }
}
