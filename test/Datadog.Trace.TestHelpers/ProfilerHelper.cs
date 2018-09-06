using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        public static Process StartProcessWithProfiler(
            string appPath,
            bool coreClr,
            IEnumerable<string> integrationPaths,
            string profilerClsid,
            string profilerDllPath)
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
                Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
                Environment.SetEnvironmentVariable("CORECLR_PROFILER", profilerClsid);
                Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", profilerDllPath);

                startInfo = new ProcessStartInfo("dotnet.exe", appPath);
            }
            else
            {
                // .NET Framework
                Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "1");
                Environment.SetEnvironmentVariable("COR_PROFILER", profilerClsid);
                Environment.SetEnvironmentVariable("COR_PROFILER_PATH", profilerDllPath);

                startInfo = new ProcessStartInfo(appPath);
            }

            Environment.SetEnvironmentVariable("DATADOG_INTEGRATIONS", string.Join(";", integrationPaths));

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

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
