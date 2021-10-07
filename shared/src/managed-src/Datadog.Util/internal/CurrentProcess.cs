using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Util
{
    internal static class CurrentProcess
    {
        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.ProcessName" /> on the
        /// instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para>
        /// </summary>
        /// <returns>Returns the name of the current process.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetName()
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                using (currentProcess)
                {
                    return currentProcess.ProcessName;
                }
            }

            return null;
        }

        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.ProcessName" />,
        /// <see cref="System.Diagnostics.Process.MachineName" />, and <see cref="System.Diagnostics.Process.Id" />
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para>
        /// </summary>
        /// <param name="processName">The name of the current process,</param>
        /// <param name="machineName">The machine name of the current process.</param>
        /// <param name="processId">The ID of the current process.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetIdentityInfo(out string processName, out string machineName, out int processId)
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                using (currentProcess)
                {
                    processName = currentProcess.ProcessName;
                    machineName = currentProcess.MachineName;
                    processId = currentProcess.Id;
                    return;
                }
            }

            processName = machineName = null;
            processId = 0;
        }

        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.UserProcessorTime" />,
        /// <see cref="System.Diagnostics.Process.PrivilegedProcessorTime" />,
        /// and <see cref="System.Diagnostics.Process.TotalProcessorTime" />
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para>
        /// </summary>
        /// <param name="userProcessorTime">CPU time in user mode.</param>
        /// <param name="privilegedProcessorTime">CPU time in kernel mode.</param>
        /// <param name="totalProcessorTime">Total CPU time.</param>        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetCpuTimeUsage(out TimeSpan userProcessorTime, out TimeSpan privilegedProcessorTime, out TimeSpan totalProcessorTime)
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                using (currentProcess)
                {
                    userProcessorTime = currentProcess.UserProcessorTime;
                    privilegedProcessorTime = currentProcess.PrivilegedProcessorTime;
                    totalProcessorTime = currentProcess.TotalProcessorTime;
                    return;
                }
            }

            userProcessorTime = privilegedProcessorTime = totalProcessorTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Convenience method for getting the <c>Count</c> of <see cref="System.Diagnostics.Process.Threads" />,        
        /// and the <see cref="System.Diagnostics.Process.ProcessorAffinity" /> mask
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para>
        /// </summary>
        /// <param name="threadsCount">The current count of the threads in the process.</param>
        /// <param name="processorAffinity">The processor affinity mask.</param>        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetThreadingInfo(out int threadsCount, out IntPtr processorAffinity)
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                using (currentProcess)
                {
                    threadsCount = currentProcess.Threads.Count;
                    processorAffinity = currentProcess.ProcessorAffinity;
                    return;
                }
            }

            threadsCount = 0;
            processorAffinity = IntPtr.Zero;
        }

        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.NonpagedSystemMemorySize64" />,
        /// <see cref="System.Diagnostics.Process.PagedMemorySize64" />,
        /// <see cref="System.Diagnostics.Process.PagedSystemMemorySize64" />,
        /// <see cref="System.Diagnostics.Process.PrivateMemorySize64" />,
        /// and <see cref="System.Diagnostics.Process.VirtualMemorySize64" />
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para>
        /// </summary>
        /// <param name="userProcessorTime">CPU time in user mode.</param>
        /// <param name="privilegedProcessorTime">CPU time in kernel mode.</param>
        /// <param name="totalProcessorTime">Total CPU time.</param>        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetMemoryUsage(out long nonpagedSystemMemorySize,
                                          out long pagedMemorySize,
                                          out long pagedSystemMemorySize,
                                          out long privateMemorySize,
                                          out long virtualMemorySize)
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (currentProcess != null)
            {
                using (currentProcess)
                {
                    nonpagedSystemMemorySize = currentProcess.NonpagedSystemMemorySize64;
                    pagedMemorySize = currentProcess.PagedMemorySize64;
                    pagedSystemMemorySize = currentProcess.PagedSystemMemorySize64;
                    privateMemorySize = currentProcess.PrivateMemorySize64;
                    virtualMemorySize = currentProcess.VirtualMemorySize64;
                    return;
                }
            }

            nonpagedSystemMemorySize = pagedMemorySize = pagedSystemMemorySize = privateMemorySize = virtualMemorySize = 0;
        }
    }
}
