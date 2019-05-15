using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        private static readonly string DotNetCoreExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";

        public static Process StartProcessWithProfiler(
            EnvironmentHelper environmentHelper,
            IEnumerable<string> integrationPaths,
            string arguments = null,
            bool redirectStandardInput = false,
            int traceAgentPort = 9696)
        {
            if (environmentHelper == null)
            {
                throw new ArgumentNullException(nameof(environmentHelper));
            }

            if (integrationPaths == null)
            {
                throw new ArgumentNullException(nameof(integrationPaths));
            }

            var applicationPath = environmentHelper.GetSampleApplicationPath();

            // clear all relevant environment variables to start with a clean slate
            ClearProfilerEnvironmentVariables();

            ProcessStartInfo startInfo;

            if (EnvironmentHelper.IsCoreClr())
            {
                // .NET Core
                startInfo = new ProcessStartInfo(DotNetCoreExecutable, $"{applicationPath} {arguments ?? string.Empty}");
                environmentHelper.SetEnvironmentVariableDefaults(traceAgentPort, applicationPath, startInfo.EnvironmentVariables);
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(applicationPath, $"{arguments ?? string.Empty}");
                var executableFileName = Path.GetFileName(applicationPath);
                environmentHelper.SetEnvironmentVariableDefaults(traceAgentPort, executableFileName, startInfo.EnvironmentVariables);
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
