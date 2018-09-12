using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        private const string DotNetCoreExecutable = "dotnet.exe";

        public static Process StartProcessWithProfiler(
            string appPath,
            bool coreClr,
            IEnumerable<string> integrationPaths,
            string profilerClsid,
            string profilerDllPath,
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

            if (profilerClsid == null)
            {
                throw new ArgumentNullException(nameof(profilerClsid));
            }

            // clear all relevant environment variables to start with a clean slate
            ClearProfilerEnvironmentVariables();

            ProcessStartInfo startInfo;

            if (coreClr)
            {
                // .NET Core
                startInfo = new ProcessStartInfo(DotNetCoreExecutable, $"{appPath} ${arguments ?? string.Empty}");

                startInfo.EnvironmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
                startInfo.EnvironmentVariables["CORECLR_PROFILER"] = profilerClsid;
                startInfo.EnvironmentVariables["CORECLR_PROFILER_PATH"] = profilerDllPath;

                startInfo.EnvironmentVariables["DD_PROFILER_PROCESSES"] = DotNetCoreExecutable;
                startInfo.EnvironmentVariables["DATADOG_PROFILER_PROCESSES"] = DotNetCoreExecutable;
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(appPath, $"{arguments ?? string.Empty}");

                startInfo.EnvironmentVariables["COR_ENABLE_PROFILING"] = "1";
                startInfo.EnvironmentVariables["COR_PROFILER"] = profilerClsid;
                startInfo.EnvironmentVariables["COR_PROFILER_PATH"] = profilerDllPath;

                string executableFileName = Path.GetFileName(appPath);
                startInfo.EnvironmentVariables["DD_PROFILER_PROCESSES"] = executableFileName;
                startInfo.EnvironmentVariables["DATADOG_PROFILER_PROCESSES"] = executableFileName;
            }

            string integrations = string.Join(";", integrationPaths);
            startInfo.EnvironmentVariables["DD_INTEGRATIONS"] = integrations;
            startInfo.EnvironmentVariables["DATADOG_INTEGRATIONS"] = integrations;
            startInfo.EnvironmentVariables["DD_TRACE_AGENT_HOSTNAME"] = "localhost";
            startInfo.EnvironmentVariables["DD_TRACE_AGENT_PORT"] = traceAgentPort.ToString();

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            return Process.Start(startInfo);
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
