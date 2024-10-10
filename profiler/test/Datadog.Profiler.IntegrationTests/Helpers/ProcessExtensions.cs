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
        private static readonly TimeSpan MemoryDumpDuration = TimeSpan.FromMinutes(15);

        public static void TakeMemoryDump(this Process process, string outputFolderPath, ITestOutputHelper output)
        {
            if (!EnvironmentHelper.IsRunningInCi())
            {
                output.WriteLine("^^^^^^^^^^^^^^^^^^^^^^ Currently not running in Github Actions CI. No memory dump will be taken.");
                return;
            }

            output.WriteLine($"^^^^^^^^^^^^^^^^^^^^^^ Taking memory dump of process Id {process.Id}...");

            if (EnvironmentHelper.IsRunningOnWindows())
            {
                TakeWindowsMemoryDump(process, outputFolderPath, output);
                return;
            }

            TakeLinuxMemoryDump(process, outputFolderPath, output);
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

            Microsoft.Extensions.Internal.ProcessExtensions.RunProcessAndWaitForExit(
                "gdb",
                $"-p {process.Id} -batch -ex \"thread apply all bt\" -ex \"detach\" -ex \"quit\"",
                TimeSpan.FromMinutes(1),
                out var stdout);

            output.WriteLine("================ Debug");
            output.WriteLine(stdout);
            File.WriteAllText($"{outputFolder}/parallel_stacks_{process.Id}", stdout);
        }

        private static void TakeWindowsMemoryDump(this Process process, string outputFolderPath, ITestOutputHelper output)
        {
            MemoryDumpHelper.InitializeAsync(output).GetAwaiter().GetResult();
            MemoryDumpHelper.CaptureMemoryDump(process, outputFolderPath, output);
        }

        private static void TakeLinuxMemoryDump(this Process process, string outputFolderPath, ITestOutputHelper output)
        {
            using var processDump = new Process();

            var dotnetRuntimeFolder = Path.GetDirectoryName(typeof(object).Assembly.Location);
            processDump.StartInfo.FileName = Path.Combine(dotnetRuntimeFolder!, "createdump");
            processDump.StartInfo.Arguments = $"{process.Id}";

            processDump.StartInfo.UseShellExecute = false;
            processDump.StartInfo.CreateNoWindow = true;
            processDump.StartInfo.RedirectStandardOutput = true;
            processDump.StartInfo.RedirectStandardError = true;
            processDump.StartInfo.RedirectStandardInput = false;
            processDump.StartInfo.WorkingDirectory = outputFolderPath;
            processDump.Start();

            using var helper = new ProcessHelper(processDump);

            var ranToCompletion = processDump.WaitForExit(MemoryDumpDuration) && helper.Drain(MemoryDumpDuration.Milliseconds / 2);
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
    }
}
