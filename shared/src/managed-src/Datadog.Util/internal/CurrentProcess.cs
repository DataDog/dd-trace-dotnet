using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Datadog.Util
{
    internal static class CurrentProcess
    {
        // For all methods in this class, take note that 'Process.GetCurrentProcess()' always returns
        // a new instacen of the 'Process' class and never null.
        // Net Fx reference source:
        // https://referencesource.microsoft.com/#System/services/monitoring/system/diagnosticts/Process.cs,1624
        // Net Core GitHub source on GitHub:
        // https://github.com/dotnet/runtime/blob/57bfe474518ab5b7cfe6bf7424a79ce3af9d6657/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs#L1084-L1087

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
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                return currentProcess.ProcessName;
            }
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
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                processName = currentProcess.ProcessName;
                machineName = currentProcess.MachineName;
                processId = currentProcess.Id;
            }
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
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                userProcessorTime = currentProcess.UserProcessorTime;
                privilegedProcessorTime = currentProcess.PrivilegedProcessorTime;
                totalProcessorTime = currentProcess.TotalProcessorTime;
            }
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
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                threadsCount = currentProcess.Threads.Count;
                processorAffinity = currentProcess.ProcessorAffinity;
            }
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
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                nonpagedSystemMemorySize = currentProcess.NonpagedSystemMemorySize64;
                pagedMemorySize = currentProcess.PagedMemorySize64;
                pagedSystemMemorySize = currentProcess.PagedSystemMemorySize64;
                privateMemorySize = currentProcess.PrivateMemorySize64;
                virtualMemorySize = currentProcess.VirtualMemorySize64;
            }
        }
    }
}
