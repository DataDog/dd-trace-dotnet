// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

/*
 * This file was copied from the .net core repository.
 * When running on Linux, we use catchsegv to display callstacks if our
 * application ran into a segmentation fault.
 * This tool runs our test application as child process.
 *
 * If the application is stuck, we kill the process (the one with catchsegv), but the child process is not killed.
 * We use this class to have the capability to kill the entire process tree whatever the framework we target.
 *
 * Note: since .Net Core 3.1, Kill method on Process class has a boolean value to kill the entire process tree or not.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Extensions.Internal
{
    internal static class ProcessExtensions
    {
        private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

        public static void KillTree(this Process process) => process.KillTree(_defaultTimeout);

        public static void KillTree(this Process process, TimeSpan timeout)
        {
            var pid = process.Id;
            if (_isWindows)
            {
                RunProcessAndWaitForExit(
                    "taskkill",
                    $"/T /F /PID {pid}",
                    timeout,
                    out var _);
            }
            else
            {
                var children = new HashSet<int>();
                GetAllChildIdsUnix(pid, children, timeout);
                foreach (var childId in children)
                {
                    KillProcessUnix(childId, timeout);
                }

                KillProcessUnix(pid, timeout);
            }
        }

        private static void GetAllChildIdsUnix(int parentId, ISet<int> children, TimeSpan timeout)
        {
            RunProcessAndWaitForExit(
                "pgrep",
                $"-P {parentId}",
                timeout,
                out var stdout);

            if (!string.IsNullOrEmpty(stdout))
            {
                using (var reader = new StringReader(stdout))
                {
                    while (true)
                    {
                        var text = reader.ReadLine();
                        if (text == null)
                        {
                            return;
                        }

                        if (int.TryParse(text, out var id))
                        {
                            children.Add(id);
                            // Recursively get the children
                            GetAllChildIdsUnix(id, children, timeout);
                        }
                    }
                }
            }
        }

        private static void KillProcessUnix(int processId, TimeSpan timeout)
        {
            RunProcessAndWaitForExit(
                "kill",
                $"-9 {processId}",
                timeout,
                out var stdout);
        }

        private static void RunProcessAndWaitForExit(string fileName, string arguments, TimeSpan timeout, out string stdout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var process = Process.Start(startInfo);

            stdout = null;
            if (process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                stdout = process.StandardOutput.ReadToEnd();
            }
            else
            {
                process.Kill();
            }
        }
    }
}