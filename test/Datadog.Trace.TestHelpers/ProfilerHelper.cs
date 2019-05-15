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
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

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

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            return Process.Start(startInfo);
        }
    }
}
