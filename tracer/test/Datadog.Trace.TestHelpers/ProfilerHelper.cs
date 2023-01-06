// <copyright file="ProfilerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        private static readonly string[] ExesToExcludeFromCorflags = TestRunners.ValidNames
                                                                     .Concat(new[] { "dotnet", "iisexpress" })
                                                                     .ToArray();

        public static Process StartProcessWithProfiler(
            string executable,
            EnvironmentHelper environmentHelper,
            MockTracerAgent agent,
            string arguments = null,
            bool redirectStandardInput = false,
            int aspNetCorePort = 5000,
            string processToProfile = null,
            bool? enableSecurity = null,
            string externalRulesFile = null,
            string workingDirectory = null)
        {
            if (environmentHelper == null)
            {
                throw new ArgumentNullException(nameof(environmentHelper));
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            // this is nasty, but it's the only way I could find to force
            // a .NET Framework exe to run in 32 bit if required
            if (EnvironmentTools.IsWindows()
             && !EnvironmentHelper.IsCoreClr()
             && !EnvironmentTools.IsTestTarget64BitProcess()
             && Path.GetExtension(executable) == ".exe"
             && !ExesToExcludeFromCorflags.Contains(Path.GetFileNameWithoutExtension(executable)))
            {
                string corFlagsExe = null;
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var dotnetWindowsSdkToolsFolder = Path.Combine(programFiles, "Microsoft SDKs", "Windows", "v10.0A", "bin");

                if (Directory.Exists(dotnetWindowsSdkToolsFolder))
                {
                    // get sub directories, e.g.
                    // @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools",
                    // @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools",
                    foreach (var folder in Directory.EnumerateDirectories(dotnetWindowsSdkToolsFolder))
                    {
                        var exe = Path.Combine(folder, "x64", "CorFlags.exe");
                        if (File.Exists(exe))
                        {
                            corFlagsExe = exe;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(corFlagsExe))
                {
                    agent.Output?.WriteLine($"CorFlags.exe found at {corFlagsExe}");
                    var setBit = EnvironmentTools.IsTestTarget64BitProcess()
                                   ? "/32BITREQ-"
                                   : "/32BITREQ+";

                    agent.Output?.WriteLine($"Updating {Path.GetFileName(executable)} using {setBit}");
                    var opts = new ProcessStartInfo(corFlagsExe, $"\"{executable}\" {setBit}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    var executedSuccessfully = Process.Start(opts).WaitForExit(20_000);

                    if (!executedSuccessfully)
                    {
                        throw new Exception($"Error setting CorFlags.exe {Path.GetFileName(executable)} {setBit}");
                    }
                }
                else
                {
                    throw new Exception("Could not find CorFlags.exe so unable to set as 32bit");
                }
            }

            var startInfo = new ProcessStartInfo(executable, $"{arguments ?? string.Empty}");

            environmentHelper.SetEnvironmentVariables(
                agent,
                aspNetCorePort,
                startInfo.Environment,
                processToProfile,
                enableSecurity,
                externalRulesFile);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            return Process.Start(startInfo);
        }
    }
}
