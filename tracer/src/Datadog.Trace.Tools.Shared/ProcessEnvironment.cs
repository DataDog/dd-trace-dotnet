// <copyright file="ProcessEnvironment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Datadog.Trace.Tools.Shared
{
    /// <summary>
    /// Helpers functions to read information about a target process
    /// </summary>
    public static class ProcessEnvironment
    {
        /// <summary>
        /// Reads the environment variables of a process
        /// </summary>
        /// <param name="process">Target process</param>
        /// <returns>List of environment variablzq</returns>
        /// <exception cref="NotSupportedException">Throws if running on an unsupported OS</exception>
        public static IReadOnlyDictionary<string, string> ReadVariables(Process process)
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return Windows.ProcessEnvironmentWindows.ReadVariables(process);
            }

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return Linux.ProcessEnvironmentLinux.ReadVariables(process);
            }

            throw new NotSupportedException("Reading environment variables is currently only supported on Windows and Linux.");
        }

        /// <summary>
        /// Reads the modules of a process
        /// </summary>
        /// <param name="process">Target process</param>
        /// <returns>List of modules</returns>
        public static string[] ReadModules(Process process)
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                // On Linux, Process.Modules does not list dynamically loaded assemblies: https://github.com/dotnet/runtime/issues/64042
                return Linux.ProcessEnvironmentLinux.ReadModules(process);
            }

            var modules = ReadModuleInformationWithRetry(process, static p => p.Modules)
                .OfType<ProcessModule>()
                .Select(static p => p.FileName)
                .Where(static p => p is not null)
                .ToArray()!;

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // On Windows, Process.Modules misses some dynamically loaded assemblies
                return modules.Union(Windows.OpenFiles.GetOpenFiles(process.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()!;
            }

            return modules!;
        }

        internal static TResult ReadModuleInformationWithRetry<TResult>(
            Process process,
            Func<Process, TResult> readModuleInformation)
        {
            const int maxAttempts = 5;
            const int retryDelayMilliseconds = 20;

            // In Windows CI, we saw "[FAILURE]: Could not fetch information about target process",
            // potentially caused by a race with DLL loading/unloading while reading module information.
            // Windows module enumeration can fail during these changes, so as a workaround we retry
            // these transient failures a few times before reporting an error.
            // See https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-enumprocessmodules#remarks
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return readModuleInformation(process);
                }
                catch (Win32Exception ex) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                            && attempt < maxAttempts
                                            && ex.NativeErrorCode is 0 or 6 or 299
                                            && !process.HasExited)
                {
                    // Give the target's DLL loader time to progress. This synchronous diagnostic
                    // read sleeps at most four times; a persistent failure propagates unchanged.
                    Thread.Sleep(retryDelayMilliseconds);
                }
            }
        }
    }
}
