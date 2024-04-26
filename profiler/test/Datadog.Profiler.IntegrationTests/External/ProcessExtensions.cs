// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

/*
 * This file was copied from the .net core repository.
 *
 * Note: since .Net Core 3.1, Kill method on Process class has a boolean value to kill the entire process tree or not.
 */
using System;
using System.Diagnostics;
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
                RunProcessAndWaitForExit(
                    "kill",
                    $"-9 {pid}",
                    timeout,
                    out var stdout);
            }
        }

        public static bool RunProcessAndWaitForExit(string fileName, string arguments, TimeSpan timeout, out string stdout)
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
                var errout = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(errout))
                {
                    stdout += Environment.NewLine + "=== Error output ===" + Environment.NewLine;
                    stdout += errout;
                }

                return true;
            }
            else
            {
                stdout = $"process created with command-line \"{fileName} arguments\" has been killed";
                process.Kill();
                return false;
            }
        }
    }
}
