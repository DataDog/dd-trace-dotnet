// <copyright file="ContextTracker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ContinuousProfiler
{
    internal class ContextTracker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextTracker));

        private readonly ProfilerStatus _status;
        private readonly bool _isCodeHotspotsEnabled;

        /// <summary>
        /// _traceContextPtr points to a structure with this layout
        /// The structure is as follow:
        /// offset size(bytes)
        ///                    |--------------------|
        ///   0        8       |     WriteGuard     |    // 8-byte for the alignment
        ///                    |--------------------|
        ///   8        8       | Local Root Span Id |
        ///                    |--------------------|
        ///   16       8       |       Span Id      |
        ///                    |--------------------|
        /// This allows us to inform the profiler sampling thread if we are writing or not
        /// the datastructure using memory barriers
        /// </summary>
        private readonly ThreadLocal<IntPtr> _traceContextPtr;

        public ContextTracker(ProfilerStatus status)
        {
            _status = status;
            _isCodeHotspotsEnabled = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CodeHotspotsEnabled)?.ToBoolean() ?? false;
            _traceContextPtr = new ThreadLocal<IntPtr>();
            Log.Information("CodeHotspots feature is {IsEnabled}.", _isCodeHotspotsEnabled ? "enabled" : "disabled");
        }

        public bool IsEnabled
        {
            get
            {
                return _status.IsProfilerReady && _isCodeHotspotsEnabled;
            }
        }

        public void Set(ulong localRootSpanId, ulong spanId)
        {
            WriteToNative(new SpanContext(localRootSpanId, spanId));
        }

        public void Reset()
        {
            WriteToNative(SpanContext.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteContext(IntPtr ptr, in SpanContext ctx)
        {
            Marshal.WriteInt64(ptr, 1);
            Thread.MemoryBarrier();

            Marshal.StructureToPtr(ctx, ptr + 8, false);

            Thread.MemoryBarrier();
            Marshal.WriteInt64(ptr, 0);
        }

        private void EnsureIsInitialized()
        {
            if (_traceContextPtr.IsValueCreated)
            {
                return;
            }

            try
            {
                _traceContextPtr.Value = NativeInterop.GetTraceContextNativePointer();
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unable to get the tracing context pointer for the thread {ThreadID}", Environment.CurrentManagedThreadId.ToString());
                _traceContextPtr.Value = IntPtr.Zero;
            }
        }

        private void WriteToNative(in SpanContext ctx)
        {
            if (!IsEnabled)
            {
                return;
            }

            EnsureIsInitialized();

            var ctxPtr = _traceContextPtr.Value;

            if (ctxPtr == IntPtr.Zero)
            {
                return;
            }

            try
            {
                WriteContext(ctxPtr, ctx);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Failed to write tracing context at {CtxPtr} for {ThreadID}", ctxPtr, Environment.CurrentManagedThreadId.ToString());
            }
        }

        // We use this struct to write to native memory pointed by _tracingContextPtr + 8
        // See the description above more explanation
        [StructLayout(LayoutKind.Explicit)]
        private readonly struct SpanContext
        {
            public static readonly SpanContext Zero = new(0, 0);

            [FieldOffset(0)]
            public readonly ulong LocalRootSpanId;

            [FieldOffset(8)]
            public readonly ulong SpanId;

            public SpanContext(ulong localRootSpanId, ulong spanId)
            {
                LocalRootSpanId = localRootSpanId;
                SpanId = spanId;
            }
        }
    }
}
