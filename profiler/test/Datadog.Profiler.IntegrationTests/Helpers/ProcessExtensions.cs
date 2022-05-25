// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class ProcessExtensions
    {
        private static readonly int MemoryDumpDurationMs = 900_000;

        public static void TakeMemoryDump(this Process process, string outputFolderPath, ITestOutputHelper output)
        {
            // this environment variable is set by Github Actions CI to `true` https://docs.github.com/en/actions/learn-github-actions/environment-variables
            string ciEnvVar = Environment.GetEnvironmentVariable("CI");

            if (!bool.TryParse(ciEnvVar, out var result) || !result)
            {
                output.WriteLine("^^^^^^^^^^^^^^^^^^^^^^ Currently not running in Github Actions CI. No memory dump will be taken.");
                return;
            }

            output.WriteLine($"^^^^^^^^^^^^^^^^^^^^^^ Taking memory dump of process Id {process.Id}...");

            using var processDump = new Process();

            if (EnvironmentHelper.IsRunningOnWindows())
            {
                // In Github Actions CI, we downloaded, extracted procdump. We also add its folder to the PATH
                processDump.StartInfo.FileName = Environment.Is64BitProcess ? "procdump64.exe" : "procdump.exe";
                processDump.StartInfo.Arguments = $@"-ma {process.Id} -accepteula";
            }
            else
            {
                processDump.StartInfo.FileName = "dotnet-dump";
                processDump.StartInfo.Arguments = $"collect --process-id {process.Id}";
            }

            processDump.StartInfo.UseShellExecute = false;
            processDump.StartInfo.CreateNoWindow = true;
            processDump.StartInfo.RedirectStandardOutput = true;
            processDump.StartInfo.RedirectStandardError = true;
            processDump.StartInfo.RedirectStandardInput = false;
            processDump.StartInfo.WorkingDirectory = outputFolderPath;
            processDump.Start();

            using var helper = new ProcessHelper(processDump);

            bool ranToCompletion = processDump.WaitForExit(MemoryDumpDurationMs) && helper.Drain(MemoryDumpDurationMs / 2);
            if (!ranToCompletion)
            {
                output.WriteLine("   Failed to take a memory dump.");
                if (!processDump.HasExited)
                {
                    output.WriteLine($"   Dumping tool (Id {processDump.Id}) has not exited. Terminating it.");

                    try
                    {
                        processDump.KillTree();
                    }
                    catch
                    {
                        // do nothing
                    }
                }
            }
            else
            {
                output.WriteLine($"   Memory dump successfully taken for process {process.Id}");
            }

            var standardOutput = helper.StandardOutput;
            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                output.WriteLine($"   Memory dump tool output: {Environment.NewLine}{standardOutput}");
            }

            var errorOutput = helper.ErrorOutput;
            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                output.WriteLine($"   Memory dump tool error: {Environment.NewLine}{errorOutput}");
            }
        }

        public static void KillTree(this Process process)
        {
            Microsoft.Extensions.Internal.ProcessExtensions.KillTree(process);
        }

        public static void GetAllThreadsStack(this Process process, string outputFolder, ITestOutputHelper output)
        {
            if (EnvironmentHelper.IsRunningOnWindows())
            {
                output.WriteLine("^^^^^^^^^^^^^^^^^^^^^^^^^ For now cannot get callstack of all threads on Windows");
                return;
            }

            var pid = process.Id;
            if (!EnvironmentHelper.IsAlpine)
            {
                // on Non-Alpine, the `dotnet` binary is launch by `catchsegv`
                // we need to get the child process (`dotnet`) spawned by `catchsegv`
                output.WriteLine($"== Get childrens of process {process.Id}");
                Microsoft.Extensions.Internal.ProcessExtensions.RunProcessAndWaitForExit(
                   "pgrep",
                   $"-P {process.Id}",
                   TimeSpan.FromSeconds(2),
                   out var childrens);

                output.WriteLine($"== children list: {childrens}");

                if (int.TryParse(childrens, out var parsedPid))
                {
                    pid = parsedPid;
                }
                else
                {
                    output.WriteLine($"^^^^^^^^^^^^^^^^^^^^^^^^^ process {process.Id} may not have childrens or we were unable to parse the list of its childrens: {childrens}");
                }
            }

            Microsoft.Extensions.Internal.ProcessExtensions.RunProcessAndWaitForExit(
                "gdb",
                $"-p {process.Id} -batch -ex \"thread apply all bt\" -ex \"detach\" -ex \"quit\"",
                TimeSpan.FromMinutes(1),
                out var stdout1);

            output.WriteLine("================ Debug");
            output.WriteLine(stdout1);
            File.WriteAllText($"{outputFolder}/parallel_stacks_{process.Id}", stdout1);

            Microsoft.Extensions.Internal.ProcessExtensions.RunProcessAndWaitForExit(
                "gdb",
                $"-p {pid} -batch -ex \"thread apply all bt\" -ex \"detach\" -ex \"quit\"",
                TimeSpan.FromMinutes(1),
                out var stdout);

            File.WriteAllText($"{outputFolder}/parallel_stacks_{pid}", stdout);
            output.WriteLine("================ Debug end");
        }
    }
}
