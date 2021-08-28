// <copyright file="ProcessHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    internal static class ProcessHelpers
    {
        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/> and <see cref="Process.ProcessName"/>
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <returns>Returns the name of the current process</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentProcessName()
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                return currentProcess.ProcessName;
            }
        }

        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/> and its property accesses
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <param name="processName">The name of the current process</param>
        /// <param name="machineName">The machine name of the current process</param>
        /// <param name="processId">The ID of the current process</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetCurrentProcessInformation(out string processName, out string machineName, out int processId)
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                processName = currentProcess.ProcessName;
                machineName = currentProcess.MachineName;
                processId = currentProcess.Id;
            }
        }

        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/> and its property accesses
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <param name="userProcessorTime">CPU time in user mode</param>
        /// <param name="systemCpuTime">CPU time in kernel mode</param>
        /// <param name="threadCount">Number of threads</param>
        /// <param name="privateMemorySize">Committed memory size</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetCurrentProcessRuntimeMetrics(out TimeSpan userProcessorTime, out TimeSpan systemCpuTime, out int threadCount, out long privateMemorySize)
        {
            using var process = Process.GetCurrentProcess();

            userProcessorTime = process.UserProcessorTime;
            systemCpuTime = process.PrivilegedProcessorTime;
            threadCount = process.Threads.Count;
            privateMemorySize = process.PrivateMemorySize64;
        }
    }
}
