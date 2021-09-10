// <copyright file="Keywords.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.RuntimeMetrics
{
    /// <summary>
    /// Keywords used by the CLR events.
    /// Enum and comments taken from Perfview source code: https://github.com/microsoft/perfview/blob/master/src/TraceEvent/Parsers/ClrTraceEventParser.cs
    /// </summary>
    [Flags]
    internal enum Keywords : long
    {
        None = 0,
        All = ~StartEnumeration,        // All does not include start-enumeration.  It just is not that useful.

        /// <summary>
        /// Logging when garbage collections and finalization happen.
        /// </summary>
        GC = 0x1,

        /// <summary>
        /// Events when GC handles are set or destroyed.
        /// </summary>
        GCHandle = 0x2,
        Binder = 0x4,

        /// <summary>
        /// Logging when modules actually get loaded and unloaded.
        /// </summary>
        Loader = 0x8,

        /// <summary>
        /// Logging when Just in time (JIT) compilation occurs.
        /// </summary>
        Jit = 0x10,

        /// <summary>
        /// Logging when precompiled native (NGEN) images are loaded.
        /// </summary>
        NGen = 0x20,

        /// <summary>
        /// Indicates that on attach or module load , a rundown of all existing methods should be done
        /// </summary>
        StartEnumeration = 0x40,

        /// <summary>
        /// Indicates that on detach or process shutdown, a rundown of all existing methods should be done
        /// </summary>
        StopEnumeration = 0x80,

        /// <summary>
        /// Events associated with validating security restrictions.
        /// </summary>
        Security = 0x400,

        /// <summary>
        /// Events for logging resource consumption on an app-domain level granularity
        /// </summary>
        AppDomainResourceManagement = 0x800,

        /// <summary>
        /// Logging of the internal workings of the Just In Time compiler.  This is fairly verbose.
        /// It details decisions about interesting optimization (like inlining and tail call)
        /// </summary>
        JitTracing = 0x1000,

        /// <summary>
        /// Log information about code thunks that transition between managed and unmanaged code.
        /// </summary>
        Interop = 0x2000,

        /// <summary>
        /// Log when lock contention occurs.  (Monitor.Enters actually blocks)
        /// </summary>
        Contention = 0x4000,

        /// <summary>
        /// Log exception processing.
        /// </summary>
        Exception = 0x8000,

        /// <summary>
        /// Log events associated with the threadpool, and other threading events.
        /// </summary>
        Threading = 0x10000,

        /// <summary>
        /// Dump the native to IL mapping of any method that is JIT compiled.  (V4.5 runtimes and above).
        /// </summary>
        JittedMethodILToNativeMap = 0x20000,

        /// <summary>
        /// If enabled will suppress the rundown of NGEN events on V4.0 runtime (has no effect on Pre-V4.0 runtimes).
        /// </summary>
        OverrideAndSuppressNGenEvents = 0x40000,

        /// <summary>
        /// Enables the 'BulkType' event
        /// </summary>
        Type = 0x80000,

        /// <summary>
        /// Enables the events associated with dumping the GC heap
        /// </summary>
        GCHeapDump = 0x100000,

        /// <summary>
        /// Enables allocation sampling with the 'fast'.  Sample to limit to 100 allocations per second per type.
        /// This is good for most detailed performance investigations.   Note that this DOES update the allocation
        /// path to be slower and only works if the process start with this on.
        /// </summary>
        GCSampledObjectAllocationHigh = 0x200000,

        /// <summary>
        /// Enables events associate with object movement or survival with each GC.
        /// </summary>
        GCHeapSurvivalAndMovement = 0x400000,

        /// <summary>
        /// Triggers a GC.  Can pass a 64 bit value that will be logged with the GC Start event so you know which GC you actually triggered.
        /// </summary>
        GCHeapCollect = 0x800000,

        /// <summary>
        /// Indicates that you want type names looked up and put into the events (not just meta-data tokens).
        /// </summary>
        GCHeapAndTypeNames = 0x1000000,

        /// <summary>
        /// Enables allocation sampling with the 'slow' rate, Sample to limit to 5 allocations per second per type.
        /// This is reasonable for monitoring.    Note that this DOES update the allocation path to be slower
        /// and only works if the process start with this on.
        /// </summary>
        GCSampledObjectAllocationLow = 0x2000000,

        /// <summary>
        /// Turns on capturing the stack and type of object allocation made by the .NET Runtime.   This is only
        /// supported after V4.5.3 (Late 2014)   This can be very verbose and you should seriously using  GCSampledObjectAllocationHigh
        /// instead (and GCSampledObjectAllocationLow for production scenarios).
        /// </summary>
        GCAllObjectAllocation = GCSampledObjectAllocationHigh | GCSampledObjectAllocationLow,

        /// <summary>
        /// This suppresses NGEN events on V4.0 (where you have NGEN PDBs), but not on V2.0 (which does not know about this
        /// bit and also does not have NGEN PDBS).
        /// </summary>
        SupressNGen = 0x40000,

        PerfTrack = 0x20000000,

        /// <summary>
        /// Also log the stack trace of events for which this is valuable.
        /// </summary>
        Stack = 0x40000000,

        /// <summary>
        /// This allows tracing work item transfer events (thread pool enqueue/dequeue/ioenqueue/iodequeue/a.o.)
        /// </summary>
        ThreadTransfer = 0x80000000L,

        /// <summary>
        /// .NET Debugger events
        /// </summary>
        Debugger = 0x100000000,

        /// <summary>
        /// Events intended for monitoring on an ongoing basis.
        /// </summary>
        Monitoring = 0x200000000,

        /// <summary>
        /// Events that will dump PDBs of dynamically generated assemblies to the ETW stream.
        /// </summary>
        Codesymbols = 0x400000000,

        /// <summary>
        /// Events that provide information about compilation.
        /// </summary>
        Compilation = 0x1000000000,

        /// <summary>
        /// Diagnostic events for diagnosing compilation and pre-compilation features.
        /// </summary>
        CompilationDiagnostic = 0x2000000000,

        /// <summary>
        /// Diagnostic events for capturing token information for events that express MethodID
        /// </summary>
        MethodDiagnostic = 0x4000000000,

        /// <summary>
        /// Diagnostic events for diagnosing issues involving the type loader.
        /// </summary>
        TypeDiagnostic = 0x8000000000,

        /// <summary>
        /// Recommend default flags (good compromise on verbosity).
        /// </summary>
        Default = GC | Type | GCHeapSurvivalAndMovement | Binder | Loader | Jit | NGen | SupressNGen
                     | StopEnumeration | Security | AppDomainResourceManagement | Exception | Threading | Contention | Stack | JittedMethodILToNativeMap
                     | ThreadTransfer | GCHeapAndTypeNames | Codesymbols | Compilation,

        /// <summary>
        /// What is needed to get symbols for JIT compiled code.
        /// </summary>
        JITSymbols = Jit | StopEnumeration | JittedMethodILToNativeMap | SupressNGen | Loader,

        /// <summary>
        /// This provides the flags commonly needed to take a heap .NET Heap snapshot with ETW.
        /// </summary>
        GCHeapSnapshot = GC | GCHeapCollect | GCHeapDump | GCHeapAndTypeNames | Type,
    }
}
