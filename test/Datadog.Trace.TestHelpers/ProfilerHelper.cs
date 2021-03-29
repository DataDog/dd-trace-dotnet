using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        public static Process StartProcessWithProfiler(
            string executable,
            string applicationPath,
            EnvironmentHelper environmentHelper,
            IEnumerable<string> integrationPaths,
            string arguments = null,
            bool redirectStandardInput = false,
            int traceAgentPort = 9696,
            int aspNetCorePort = 5000,
            int? statsdPort = null,
            bool useCodeCoverage = true)
        {
            if (environmentHelper == null)
            {
                throw new ArgumentNullException(nameof(environmentHelper));
            }

            if (integrationPaths == null)
            {
                throw new ArgumentNullException(nameof(integrationPaths));
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            ProcessStartInfo startInfo;

            if (useCodeCoverage && System.Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                useCodeCoverage = false;
            }

            if (EnvironmentHelper.IsCoreClr())
            {
                // .NET Core
                if (useCodeCoverage)
                {
                    var applicationFolder = Path.GetDirectoryName(applicationPath);
                    var profilerFolder = Path.Combine(applicationFolder, @"profiler-lib\netcoreapp3.1");
                    var reportFile = Path.Combine(applicationFolder, $"coverage.{Guid.NewGuid():N}.xml");

                    startInfo = new ProcessStartInfo("coverlet", $"\"{applicationPath}\" --format cobertura --output \"{reportFile}\" --exclude \"[*]Datadog.Trace.Vendors.*\" --include-directory \"{profilerFolder}\" --target dotnet --targetargs \"{applicationPath} {arguments ?? string.Empty}\"");
                }
                else
                {
                    startInfo = new ProcessStartInfo(executable, $"{applicationPath} {arguments ?? string.Empty}");
                }
            }
            else
            {
                // .NET Framework
                startInfo = new ProcessStartInfo(applicationPath, $"{arguments ?? string.Empty}");
            }

            environmentHelper.SetEnvironmentVariables(traceAgentPort, aspNetCorePort, statsdPort, executable, startInfo.EnvironmentVariables);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            return Process.Start(startInfo);
        }
    }
}
