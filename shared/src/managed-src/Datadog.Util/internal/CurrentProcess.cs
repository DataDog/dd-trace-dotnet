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
        /// Convenience method for calling <see cref="System.Diagnostics.Process.Id" /> on the
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
        public static int GetId()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                return currentProcess.Id;
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

        /// <summary>
        /// Convenience method for getting the <c>Count</c> of <see cref="System.Diagnostics.Process.Modules" />       
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
        /// <returns>The number of the modules currently loaded into the current process.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetModulesCount()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                return currentProcess.Modules.Count;
            }
        }


        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.Modules" />
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para><para>
        /// Note that the <see cref="System.Diagnostics.ProcessModule" /> class, whose instances are contained in the
        /// collection returned by the <see cref="System.Diagnostics.Process.Modules" /> API is also guarded by the same
        /// link demand. So working with <c>ProcessModule</c> instances suffers from the same inconvenience.
        /// To address that, this method does NOT return <c>ProcessModule</c> instances directly.
        /// Instead, it copies the data obtained from the process to an array of <see cref="CurrentProcess.ModuleInfo"/> structures.
        /// Those can be accessed without worrying about the partial trust issue.<br />
        /// If you only require the number of process modules, not the individual module info, then
        /// it is more performant to call <see cref="GetModulesCount"/> instead of this method.
        /// </para>
        /// </summary>
        /// <returns>An array of <see cref="CurrentProcess.ModuleInfo"/> structures representing the modules currently loaded
        /// into this process.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ModuleInfo[] GetModules()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                ProcessModuleCollection processModules = currentProcess.Modules;
                ModuleInfo[] moduleInfos = new ModuleInfo[processModules.Count];

                for (int i = 0; i < moduleInfos.Length; i++)
                {
                    try
                    {
                        moduleInfos[i].SetTo(processModules, i);
                    }
                    catch
                    {
                        moduleInfos[i].Reset();
                    }
                }

                return moduleInfos;
            }
        }

        /// <summary>
        /// Convenience method for calling <see cref="System.Diagnostics.Process.MainModule" />
        /// on the instance obtained via the <see cref="System.Diagnostics.Process.GetCurrentProcess" />-method.
        /// <para>
        /// !! This method should be called from within a try-catch block !! <br />
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.Process" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when the caller method is being JIT compiled, NOT when methods on
        /// the <c>Process</c> class are being invoked.
        /// This wrapper, when called from within a try-catch block, allows catching the exception.
        /// </para><para>
        /// Note that the <see cref="System.Diagnostics.ProcessModule" /> class is also guarded by the same
        /// link demand. So working with <c>ProcessModule</c> instances suffers from the same inconvenience.
        /// To address that, this method does NOT return a <c>ProcessModule</c> instance directly.
        /// Instead, it copies the data obtained from the process to a <see cref="CurrentProcess.ModuleInfo"/> structure.
        /// That can be accessed without worrying about the partial trust issue.
        /// </para>
        /// </summary>
        /// <returns>An array of <see cref="CurrentProcess.ModuleInfo"/> structures representing the modules currently loaded
        /// into this process.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ModuleInfo GetMainModule()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule mainModule = currentProcess.MainModule)
                {
                    ModuleInfo mainModuleInfo = new ModuleInfo();

                    if (mainModule != null)
                    {
                        mainModuleInfo.SetTo(mainModule);
                    }

                    return mainModuleInfo;
                }
            }
        }

        /// <summary>
        /// Represents information from a <see cref="System.Diagnostics.ProcessModule" /> without raising
        /// a LinkDemand for FullTrust when used.
        /// <para>
        /// On the (classic) .NET Framework the <see cref="System.Diagnostics.ProcessModule" /> class is
        /// guarded by a LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// That exception is thrown when a method using the <c>ProcessModule</c> class is being JIT compiled,
        /// NOT when methods on that class class are being invoked.<br />
        /// This is the same situation as with the <see cref="System.Diagnostics.Process" /> class.
        /// The entire purpose of the <see cref="CurrentProcess" /> utility is to help dealing with this conveniently.
        /// The <see cref="CurrentProcess.GetModules"/> method returns information about the process modules.
        /// To allow working with that information without dealing with the partual trust issue, that method copies 
        /// information from the internally obtained <c>ProcessModule</c> items into instances of this type.
        /// </para>
        /// </summary>
        public struct ModuleInfo
        {
            public IntPtr BaseAddress { get; private set; }
            public IntPtr EntryPointAddress { get; private set; }
            public string FileName { get; private set; }
            public int ModuleMemorySize { get; private set; }
            public string ModuleName { get; private set; }

            /// <summary>
            /// This method should be only called by <see cref="CurrentProcess.GetModules" />.
            /// Note that like <see cref="System.Diagnostics.Process" />, <see cref="System.Diagnostics.ProcessModule" /> also
            /// triggers the LinkDemand for FullTrust, i.e. an exception in partial trust, so this method must be called in a try
            /// catch block (see the doc comment to the public <see cref="CurrentProcess" /> methods for details.
            /// </summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal void SetTo(ProcessModuleCollection processModules, int index)
            {
                using (ProcessModule module = processModules[index])
                {
                    this.BaseAddress = module.BaseAddress;
                    this.EntryPointAddress = module.EntryPointAddress;
                    this.FileName = module.FileName;
                    this.ModuleMemorySize = module.ModuleMemorySize;
                    this.ModuleName = module.ModuleName;
                }
            }

            /// <summary>This method should be only called by <see cref="CurrentProcess.GetMainModule" />.</summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal void SetTo(ProcessModule module)
            {
                this.BaseAddress = module.BaseAddress;
                this.EntryPointAddress = module.EntryPointAddress;
                this.FileName = module.FileName;
                this.ModuleMemorySize = module.ModuleMemorySize;
                this.ModuleName = module.ModuleName;
            }

            internal void Reset()
            {
                this.BaseAddress = IntPtr.Zero;
                this.EntryPointAddress = IntPtr.Zero;
                this.FileName = null;
                this.ModuleMemorySize = 0;
                this.ModuleName = null;
            }
        }
    }
}
