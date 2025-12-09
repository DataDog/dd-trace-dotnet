// <copyright file="ContextTracker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ContinuousProfiler
{
    internal sealed class ContextTracker : IContextTracker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextTracker));

        private readonly IProfilerStatus _status;
        private readonly bool _isCodeHotspotsEnabled;
        private readonly bool _isEndpointProfilingEnabled;

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
        /// This allows us to inform the profiler sampling thread when we are writing or not the data
        /// and avoid torn read/write (Using memory barriers).
        /// We take advantage of this layout in SpanContext.Write
        /// </summary>
        private readonly ThreadLocal<IntPtr> _traceContextPtr;

        private bool _firstEndpointFailure = true;

        public ContextTracker(IProfilerStatus status)
        {
            _status = status;
            _isCodeHotspotsEnabled = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.Profiler.CodeHotspotsEnabled)?.ToBoolean() ?? true;
            _isEndpointProfilingEnabled = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.Profiler.EndpointProfilingEnabled)?.ToBoolean() ?? true;
            _traceContextPtr = new ThreadLocal<IntPtr>();
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

        public void SetEndpoint(ulong localRootSpanId, string endpoint)
        {
            if (IsEnabled && _isEndpointProfilingEnabled && !string.IsNullOrEmpty(endpoint))
            {
                try
                {
                    NativeInterop.SetEndpoint(RuntimeId.Get(), localRootSpanId, endpoint);
                }
                catch (Exception e)
                {
                    if (_firstEndpointFailure)
                    {
                        Log.Warning(e, "Unable to set the endpoint for span {SpanId}", localRootSpanId);
                        _firstEndpointFailure = false;
                    }
                }
            }
        }

        public void Reset()
        {
            WriteToNative(SpanContext.Zero);
        }

        private bool EnsureIsInitialized()
        {
            try
            {
                // try to avoid thread abort deadly exceptions
                if (
                    ((Thread.CurrentThread.ThreadState & ThreadState.AbortRequested) == ThreadState.AbortRequested) ||
                    ((Thread.CurrentThread.ThreadState & ThreadState.Aborted) == ThreadState.Aborted))
                {
                    return false;
                }

                if (_traceContextPtr.IsValueCreated)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                // seen in a crash: weird but possible on shutdown probably if the object is disposed
                Log.Warning(e, "Disposed tracing context pointer wrapper for the thread {ThreadID}", Environment.CurrentManagedThreadId.ToString());
                _traceContextPtr.Value = IntPtr.Zero;
                return false;
            }

            try
            {
                _traceContextPtr.Value = NativeInterop.GetTraceContextNativePointer();
            }
            catch (Exception e)
            {
                Log.Warning(e, "Unable to get the tracing context pointer for the thread {ThreadID}", Environment.CurrentManagedThreadId.ToString());
                _traceContextPtr.Value = IntPtr.Zero;
                return false;
            }

            return true;
        }

        private void WriteToNative(in SpanContext ctx)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (!EnsureIsInitialized())
            {
                return;
            }

            var ctxPtr = _traceContextPtr.Value;

            if (ctxPtr == IntPtr.Zero)
            {
                return;
            }

            try
            {
                ctx.Write(ctxPtr);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to write tracing context at {CtxPtr} for {ThreadID}", ctxPtr, Environment.CurrentManagedThreadId.ToString());
            }
        }

        // See the description and the layout depicted above
        private readonly struct SpanContext
        {
            public static readonly SpanContext Zero = new(0, 0);

            public readonly ulong LocalRootSpanId;
            public readonly ulong SpanId;

            public SpanContext(ulong localRootSpanId, ulong spanId)
            {
                LocalRootSpanId = localRootSpanId;
                SpanId = spanId;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Write(IntPtr ptr)
            {
                // Set the WriteGuard
                Marshal.WriteInt64(ptr, 1);
                Thread.MemoryBarrier();

                // Using WriteInt64 to write 2 long values is ~8x faster than using Marshal.StructureToPtr
                // For the offset, we follow the layout depicted above
                Marshal.WriteInt64(ptr + 8, (long)LocalRootSpanId);
                Marshal.WriteInt64(ptr + 16, (long)SpanId);

                // Reset the WriteGuard
                Thread.MemoryBarrier();
                Marshal.WriteInt64(ptr, 0);
            }
        }
    }
}
