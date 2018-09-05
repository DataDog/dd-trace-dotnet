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

            // TODO: use "CorFlags MyAssembly.exe /32BIT+" to force 32/64 bit process?
            ProcessStartInfo startInfo;

            if (coreClr)
            {
                // .NET Core
                Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
                Environment.SetEnvironmentVariable("CORECLR_PROFILER", profilerClsid);
                Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", profilerDllPath);
                Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH_32", profilerDllPath);
                Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH_64", profilerDllPath);

                startInfo = new ProcessStartInfo("dotnet", appPath);
            }
            else
            {
                // .NET Framework
                Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "1");
                Environment.SetEnvironmentVariable("COR_PROFILER", profilerClsid);
                Environment.SetEnvironmentVariable("COR_PROFILER_PATH", profilerDllPath);
                Environment.SetEnvironmentVariable("COR_PROFILER_PATH_32", profilerDllPath);
                Environment.SetEnvironmentVariable("COR_PROFILER_PATH_64", profilerDllPath);

                startInfo = new ProcessStartInfo(appPath);
            }

            Environment.SetEnvironmentVariable("DATADOG_INTEGRATIONS", string.Join(";", integrationPaths));

            // clear this one
            Environment.SetEnvironmentVariable("DATADOG_PROFILER_PROCESSES", null);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            return Process.Start(startInfo);
        }
    }
}
