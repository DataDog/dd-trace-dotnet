// <copyright file="MemoryMappedCounters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class MemoryMappedCounters : IRuntimeMetricsListener
    {
        private const string GarbageCollectionMetrics = $"{MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {MetricsNames.ContentionCount}, {MetricsNames.Gen0CollectionsCount}, {MetricsNames.Gen1CollectionsCount}, {MetricsNames.Gen2CollectionsCount}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MemoryMappedCounters>();

        private readonly IDogStatsd _statsd;
        private readonly int _processId;

        private int? _previousGen0Count;
        private int? _previousGen1Count;
        private int? _previousGen2Count;

        private double? _lastContentionCount;
        private MemoryMappedFile _file;
        private MemoryMappedViewAccessor _view;

        public MemoryMappedCounters(IDogStatsd statsd)
        {
            _statsd = statsd;

            ProcessHelpers.GetCurrentProcessInformation(out _, out _, out _processId);

            MemoryMappedFile file = null;
            MemoryMappedViewAccessor view = null;

            try
            {
                file = MemoryMappedFile.OpenExisting(@"Cor_CLR_WRITER\Cor_SxSPublic_IPCBlock");
                view = file.CreateViewAccessor();

                if (GC.CollectionCount(0) == 0)
                {
                    // This is unlikely to happen, but we must make sure that the GC has run at least once,
                    // otherwise the GC structure that we use for the sanity check will be empty.
                    GC.Collect(0, GCCollectionMode.Forced, blocking: true); // Sorry ._.
                }

                // Sanity check
                view.Read<IpcControlBlock>(0, out var controlBlock);

                if (!IsInitialized(in controlBlock))
                {
                    throw new InvalidOperationException("The IPC control block is not initialized");
                }

                if (controlBlock.Perf.GC.ProcessID != _processId)
                {
                    throw new InvalidOperationException($"The PID in the IPC control block does not match (expected {_processId}, found {controlBlock.Perf.GC.ProcessID}");
                }
            }
            catch
            {
                view?.Dispose();
                file?.Dispose();
                throw;
            }

            _file = file;
            _view = view;
        }

        [Flags]
        private enum IpcHeaderFlags : ushort
        {
            IPC_FLAG_USES_FLAGS = 0x1,
            IPC_FLAG_INITIALIZED = 0x2,
            IPC_FLAG_X86 = 0x4
        }

        private static bool IsInitialized(in IpcControlBlock controlBlock)
        {
            if (controlBlock.Header.Flags == 0)
            {
                return false;
            }

            if ((controlBlock.Header.Flags & IpcHeaderFlags.IPC_FLAG_USES_FLAGS) == IpcHeaderFlags.IPC_FLAG_USES_FLAGS)
            {
                // The header uses flags, check that the initialized flag is set
                if ((controlBlock.Header.Flags & IpcHeaderFlags.IPC_FLAG_INITIALIZED) != IpcHeaderFlags.IPC_FLAG_INITIALIZED)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.Dispose();
                _view = null;
            }

            if (_file != null)
            {
                _file.Dispose();
                _file = null;
            }
        }

        public void Refresh()
        {
            _view.Read<IpcControlBlock>(0, out var controlBlock);

            var perf = controlBlock.Perf;

            if (perf.GC.ProcessID != _processId)
            {
                throw new InvalidOperationException($"The PID in the IPC control block does not match (expected {_processId}, found {controlBlock.Perf.GC.ProcessID}");
            }

            _statsd.Gauge(MetricsNames.Gen0HeapSize, perf.GC.GenHeapSize0);
            _statsd.Gauge(MetricsNames.Gen1HeapSize, perf.GC.GenHeapSize1);
            _statsd.Gauge(MetricsNames.Gen2HeapSize, perf.GC.GenHeapSize2);
            _statsd.Gauge(MetricsNames.LohSize, perf.GC.LargeObjSize);

            var contentionCount = perf.LocksAndThreads.Contention;

            if (_lastContentionCount == null)
            {
                _lastContentionCount = contentionCount;
            }
            else
            {
                _statsd.Counter(MetricsNames.ContentionCount, contentionCount - _lastContentionCount.Value);
                _lastContentionCount = contentionCount;
            }

            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            if (_previousGen0Count != null)
            {
                _statsd.Increment(MetricsNames.Gen0CollectionsCount, gen0 - _previousGen0Count.Value);
            }

            if (_previousGen1Count != null)
            {
                _statsd.Increment(MetricsNames.Gen1CollectionsCount, gen1 - _previousGen1Count.Value);
            }

            if (_previousGen2Count != null)
            {
                _statsd.Increment(MetricsNames.Gen2CollectionsCount, gen2 - _previousGen2Count.Value);
            }

            _previousGen0Count = gen0;
            _previousGen1Count = gen1;
            _previousGen2Count = gen2;

            Log.Debug("Sent the following metrics to the DD agent: {Metrics}", GarbageCollectionMetrics);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct TRICOUNT
        {
            public readonly int Current;
            public readonly int Total;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct IPCHeader
        {
            /// <summary>
            /// Value of 0 is special; means that this block has never been touched before by a writer
            /// </summary>
            public readonly int Counter;

            /// <summary>
            /// Value of 0 is special; means that chunk is currently free (runtime ids are always greater than 0)
            /// </summary>
            public readonly int RuntimeId;

            public readonly int Reserved1;
            public readonly int Reserved2;

            /// <summary>
            /// Version of the IPC Block
            /// </summary>
            public readonly ushort Version;

            /// <summary>
            /// Flags field
            /// </summary>
            public readonly IpcHeaderFlags Flags;

            /// <summary>
            /// Size of the entire shared memory block
            /// </summary>
            public readonly int BlockSize;

            /// <summary>
            /// Stamp for year built
            /// </summary>
            public readonly ushort BuildYear;

            /// <summary>
            /// Stamp for Month/Day built
            /// </summary>
            public readonly ushort BuildNumber;

            /// <summary>
            /// Number of entries in the table
            /// </summary>
            public readonly int NumEntries;

            /// <summary>
            /// Entry describing each client's block
            /// </summary>
            public readonly IPCEntry Table;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct IpcControlBlock
        {
            public readonly IPCHeader Header;

            public readonly PerfCounterIpcControlBlock Perf;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct IPCEntry
        {
            /// <summary>
            /// Offset of the IPC Block from the end of the Full IPC Header
            /// </summary>
            public readonly int Offset;

            /// <summary>
            /// Size (in bytes) of the block
            /// </summary>
            public readonly int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfJit
        {
            /// <summary>
            /// Number of methods jitted
            /// </summary>
            public readonly int MethodsJitted;

            /// <summary>
            /// IL jitted stats
            /// </summary>
            public readonly TRICOUNT ILJitted;

            /// <summary>
            /// # of standard Jit failures
            /// </summary>
            public readonly int JitFailures;

            /// <summary>
            /// Time in JIT since last sample
            /// </summary>
            public readonly int TimeInJit;

            /// <summary>
            /// Time in JIT base counter
            /// </summary>
            public readonly int TimeInJitBase;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfCounterIpcControlBlock
        {
            // Versioning info

            /// <summary>
            /// Size of this entire block
            /// </summary>
            public readonly short Size;

            /// <summary>
            /// Attributes for this block
            /// </summary>
            public readonly short Attributes;

            // Counter Sections
            public readonly PerfGC GC;
            public readonly PerfContexts Context;
            public readonly PerfInterop Interop;
            public readonly PerfLoading Loading;
            public readonly PerfExceptions Exceptions;
            public readonly PerfLocksAndThreads LocksAndThreads;
            public readonly PerfJit Jit;
            public readonly PerfSecurity Security;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfSecurity
        {
            public readonly int TotalRuntimeChecks;

            /// <summary>
            ///  % time authenticating
            /// </summary>
            public readonly long TimeAuthorize;

            /// <summary>
            /// Link time checks
            /// </summary>
            public readonly int LinkChecks;

            /// <summary>
            /// % time in Runtime checks
            /// </summary>
            public readonly int TimeRTchecks;

            /// <summary>
            /// % time in Runtime checks base counter
            /// </summary>
            public readonly int TimeRTchecksBase;

            /// <summary>
            /// Depth of stack for security checks
            /// </summary>
            public readonly int StackWalkDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfLocksAndThreads
        {
            /// <summary>
            /// # of times in AwareLock::EnterEpilogue()
            /// </summary>
            public readonly int Contention;

            public readonly TRICOUNT QueueLength;

            /// <summary>
            /// Number (created - destroyed) of logical threads
            /// </summary>
            public readonly int CurrentThreadsLogical;

            /// <summary>
            /// Number (created - destroyed) of OS threads
            /// </summary>
            public readonly int CurrentThreadsPhysical;

            /// <summary>
            /// # of Threads execute in runtime's control
            /// </summary>
            public readonly TRICOUNT RecognizedThreads;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfExceptions
        {
            /// <summary>
            /// Number of Exceptions thrown
            /// </summary>
            public readonly int Thrown;

            /// <summary>
            /// Number of Filters executed
            /// </summary>
            public readonly int FiltersExecuted;

            /// <summary>
            /// Number of Finallys executed
            /// </summary>
            public readonly int FinallysExecuted;

            /// <summary>
            /// Delta from throw to catch site on stack
            /// </summary>
            public readonly int ThrowToCatchStackDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfLoading
        {
            public readonly TRICOUNT ClassesLoaded;

            /// <summary>
            /// Current # of AppDomains
            /// </summary>
            public readonly TRICOUNT AppDomains;

            /// <summary>
            /// Current # of Assemblies
            /// </summary>
            public readonly TRICOUNT Assemblies;

            /// <summary>
            /// % time loading
            /// </summary>
            public readonly long TimeLoading;

            /// <summary>
            /// Avg search length for assemblies
            /// </summary>
            public readonly int AsmSearchLength;

            /// <summary>
            /// Classes Failed to load
            /// </summary>
            public readonly int LoadFailures;

            /// <summary>
            /// Total size of heap used by the loader
            /// </summary>
            public readonly nint LoaderHeapSize;

            /// <summary>
            /// Rate at which app domains are unloaded
            /// </summary>
            public readonly int AppDomainsUnloaded;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfInterop
        {
            /// <summary>
            /// Number of CCWs
            /// </summary>
            public readonly int CCW;

            /// <summary>
            /// Number of stubs
            /// </summary>
            public readonly int Stubs;

            /// <summary>
            /// # of time marshalling args and return values
            /// </summary>
            public readonly int Marshalling;

            /// <summary>
            /// Number of tlbs we import
            /// </summary>
            public readonly int TLBImports;

            /// <summary>
            /// Number of tlbs we export
            /// </summary>
            public readonly int TLBExports;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfContexts
        {
            /// <summary>
            /// # of remote calls
            /// </summary>
            public readonly int RemoteCalls;

            /// <summary>
            /// Number of current channels
            /// </summary>
            public readonly int Channels;

            /// <summary>
            /// Number of context proxies
            /// </summary>
            public readonly int Proxies;

            /// <summary>
            /// # of Context-bound classes
            /// </summary>
            public readonly int Classes;

            /// <summary>
            /// # of context bound objects allocated
            /// </summary>
            public readonly int ObjAlloc;

            /// <summary>
            /// The current number of contexts
            /// </summary>
            public readonly int Contexts;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PerfGC
        {
            /// <summary>
            /// Count of collects gen 0
            /// </summary>
            public readonly nint GenCollections0;

            /// <summary>
            /// Count of collects gen 1
            /// </summary>
            public readonly nint GenCollections1;

            /// <summary>
            /// Count of collects gen 2
            /// </summary>
            public readonly nint GenCollections2;

            /// <summary>
            /// Count of promoted memory from gen 0
            /// </summary>
            public readonly nint PromotedMem0;

            /// <summary>
            /// Count of promoted memory from gen 1
            /// </summary>
            public readonly nint PromotedMem1;

            /// <summary>
            /// Count of memory promoted due to finalization
            /// </summary>
            public readonly nint PromotedFinalizationMem;

            /// <summary>
            /// Process ID
            /// </summary>
            public readonly nint ProcessID;

            /// <summary>
            /// Size of heaps gen 0
            /// </summary>
            public readonly nint GenHeapSize0;

            /// <summary>
            /// Size of heaps gen 1
            /// </summary>
            public readonly nint GenHeapSize1;

            /// <summary>
            /// Size of heaps gen 2
            /// </summary>
            public readonly nint GenHeapSize2;

            /// <summary>
            /// Total number of committed bytes
            /// </summary>
            public readonly nint TotalCommittedBytes;

            /// <summary>
            /// Bytes reserved via VirtualAlloc
            /// </summary>
            public readonly nint TotalReservedBytes;

            /// <summary>
            /// Size of Large Object Heap
            /// </summary>
            public readonly nint LargeObjSize;

            /// <summary>
            /// Count of instances surviving from finalizing
            /// </summary>
            public readonly nint SurviveFinalize;

            /// <summary>
            /// Count of GC handles
            /// </summary>
            public readonly nint Handles;

            /// <summary>
            /// Bytes allocated
            /// </summary>
            public readonly nint Alloc;

            /// <summary>
            /// Bytes allocated for Large Objects
            /// </summary>
            public readonly nint LargeAlloc;

            /// <summary>
            /// Number of explicit GCs
            /// </summary>
            public readonly nint InducedGCs;

            /// <summary>
            /// Time in GC
            /// </summary>
            public readonly int TimeInGC;

            /// <summary>
            /// Must follow time in GC counter
            /// </summary>
            public readonly int TimeInGCBase;

            /// <summary>
            /// # of Pinned Objects
            /// </summary>
            public readonly nint PinnedObj;

            /// <summary>
            /// # of sink blocks
            /// </summary>
            public readonly nint SinkBlocks;
        }
    }
}

#endif
