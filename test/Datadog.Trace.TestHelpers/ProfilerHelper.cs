using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

            string codeCoveragePath = null;

            if (useCodeCoverage)
            {
                codeCoveragePath = Environment.GetEnvironmentVariable("TEST_COVERAGE_PATH");
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            ProcessStartInfo startInfo;

            if (EnvironmentHelper.IsCoreClr())
            {
                // .NET Core
                if (codeCoveragePath != null)
                {
                    var profilerPath = Path.GetDirectoryName(environmentHelper.GetProfilerPath());

                    var reportFile = Path.Combine(codeCoveragePath, $"coverage.{Guid.NewGuid():N}.xml");

#if NETCOREAPP3_1_OR_GREATER
                    var assemblyFolders = Path.Combine(profilerPath, "netcoreapp3.1");
#else
                    var assemblyFolders = Path.Combine(profilerPath, "netstandard2.0");
#endif

                    startInfo = new ProcessStartInfo("coverlet", $"\"{applicationPath}\" --format cobertura --output \"{reportFile}\" --exclude \"[*]Datadog.Trace.Vendors.*\" --include-directory \"{assemblyFolders}\" --target dotnet --targetargs \"{applicationPath} {arguments ?? string.Empty}\"");
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
