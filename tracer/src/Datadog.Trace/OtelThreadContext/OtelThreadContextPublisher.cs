// <copyright file="OtelThreadContextPublisher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Publishes the active trace context of the current thread as an OTEP 4947 <i>Thread-Local Context Record</i>.
/// <para>
/// Each OS thread owns one record. The address of that record is installed into the thread's
/// <c>otel_thread_ctx_v1</c> slot once, the first time the thread carries an active span, and is never
/// changed afterwards - so the only native call on the whole feature happens once per thread, and every
/// subsequent context change is a handful of managed writes into unmanaged memory. See
/// docs/OTelContextPropagation.md.
/// </para>
/// </summary>
internal sealed unsafe class OtelThreadContextPublisher : IOtelThreadContextPublisher
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OtelThreadContextPublisher>();

    // Per-OS-thread state. This is a [ThreadStatic] rather than a ThreadLocal<T> because it is read on
    // every span activation, and a static field access is materially cheaper. It holds the publisher that
    // created it so that a new publisher instance never reuses another one's record (which matters for
    // tests more than for production, where there is a single publisher).
    [ThreadStatic]
    private static ThreadRecord? _threadRecord;

    private readonly IOtelThreadContextSlotProvider _slotProvider;
    private int _disabled;

    internal OtelThreadContextPublisher(IOtelThreadContextSlotProvider slotProvider)
    {
        _slotProvider = slotProvider;
    }

    public bool IsEnabled => Volatile.Read(ref _disabled) == 0;

    /// <summary>
    /// Creates a publisher, or <see cref="NullOtelThreadContextPublisher"/> when the feature is turned off
    /// or cannot work in this process.
    /// </summary>
    internal static IOtelThreadContextPublisher Create(TracerSettings settings)
    {
        if (!settings.OtelThreadContextEnabled)
        {
            return NullOtelThreadContextPublisher.Instance;
        }

        var framework = FrameworkDescription.Instance;
        var platformSupported = IsPlatformSupported();

        // The P/Invoke to the native tracer is only usable under automatic instrumentation, because that
        // is what rewrites the P/Invoke map to point at the deployed native library.
        var instrumentationAttached = EnvironmentHelpersNoLogging.IsClrProfilerAttachedSafe();

        if (!platformSupported || !instrumentationAttached)
        {
            Log.Information<string, string, bool>(
                "OpenTelemetry thread context publication was requested but is unavailable on {OSPlatform}/{ProcessArchitecture} (instrumentation attached: {InstrumentationAttached}).",
                framework.OSPlatform,
                framework.ProcessArchitecture,
                instrumentationAttached);

            return NullOtelThreadContextPublisher.Instance;
        }

        return new OtelThreadContextPublisher(OtelThreadContextSlotProvider.Instance);
    }

    /// <summary>
    /// Gets a value indicating whether the current platform can publish thread contexts at all. OTEP 4947
    /// is deliberately Linux-only: it relies on ELF thread-local storage, and its readers are themselves
    /// Linux-specific (the OpenTelemetry eBPF profiler, OBI).
    /// </summary>
    internal static bool IsPlatformSupported()
    {
        var framework = FrameworkDescription.Instance;

        return string.Equals(framework.OSPlatform, OSPlatformName.Linux, StringComparison.OrdinalIgnoreCase) &&
               (framework.ProcessArchitecture == ProcessArchitecture.X64 ||
                framework.ProcessArchitecture == ProcessArchitecture.Arm64);
    }

    public void Set(Span span)
    {
        var record = GetThreadRecord();

        if (record is null)
        {
            return;
        }

        try
        {
            OtelThreadContextRecord.Write(
                (byte*)record.Address,
                span.Context.TraceId128,
                span.SpanId,
                span.RootSpanId,
                GetTraceFlags(span));
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    public void Reset()
    {
        // Deliberately does not initialize a record: a thread that has never published a context has a
        // null slot, which already means "no context" to a reader.
        var record = _threadRecord;

        if (record is null || record.Owner != this || !IsEnabled)
        {
            return;
        }

        try
        {
            OtelThreadContextRecord.Invalidate((byte*)record.Address);
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    /// <summary>
    /// Builds the W3C trace-flags byte.
    /// <para>
    /// This reads the sampling decision only if one has already been made. Calling
    /// <c>GetOrMakeSamplingDecision()</c> here - as the W3C propagator does - would force the decision at
    /// span activation time instead of when the trace is propagated or flushed, which is an observable
    /// change in tracer behaviour. An undecided trace is reported as not sampled.
    /// </para>
    /// </summary>
    private static byte GetTraceFlags(Span span)
    {
        var samplingPriority = span.Context.TraceContext?.SamplingPriority ?? span.Context.SamplingPriority;

        return samplingPriority is { } priority && SamplingPriorityValues.IsKeep(priority)
                   ? (byte)1
                   : (byte)0;
    }

    private ThreadRecord? GetThreadRecord()
    {
        var record = _threadRecord;

        if (record is not null && record.Owner == this)
        {
            return record;
        }

        return IsEnabled ? InitializeThreadRecord() : null;
    }

    private ThreadRecord? InitializeThreadRecord()
    {
        try
        {
            var slot = _slotProvider.GetSlot();

            if (slot == IntPtr.Zero)
            {
                Disable("the native tracer did not provide a thread context slot");
                return null;
            }

            var block = OtelThreadContextRecordPool.Instance.Rent();

            // Publishing the pointer is a plain store: only this thread writes this slot, and the record
            // it points at is already initialized and marked invalid, so a reader that samples between
            // this store and the first Write() sees "no context" rather than garbage.
            *(byte**)slot = block;

            var record = new ThreadRecord(this, (IntPtr)block);
            _threadRecord = record;
            return record;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return null;
        }
    }

    private void Disable(Exception exception)
    {
        if (Interlocked.Exchange(ref _disabled, 1) == 0)
        {
            Log.Warning(exception, "Unable to publish the OpenTelemetry thread context. Publication is now disabled.");
        }
    }

    private void Disable(string reason)
    {
        if (Interlocked.Exchange(ref _disabled, 1) == 0)
        {
            Log.Warning("Unable to publish the OpenTelemetry thread context because {Reason}. Publication is now disabled.", reason);
        }
    }

    /// <summary>
    /// Owns one thread's record. Reachable only from a <c>[ThreadStatic]</c> field, so it becomes
    /// collectable when its thread dies, and the finalizer hands the record back to the pool.
    /// </summary>
    private sealed class ThreadRecord
    {
        public ThreadRecord(OtelThreadContextPublisher owner, IntPtr address)
        {
            Owner = owner;
            Address = address;
        }

        ~ThreadRecord()
        {
            // Note we do NOT clear the thread's slot here: this runs on the finalizer thread, so the
            // cached slot address belongs to a thread whose TLS block pthread has already reclaimed, and
            // writing to it would be a use-after-free. Nothing is left dangling, because the slot dies
            // together with the thread that owned it.
            //
            // Finalizers can also run at process shutdown while threads are still alive. Recycling a live
            // thread's record then makes it read as "no context", which is harmless at that point.
            OtelThreadContextRecordPool.Instance.Return((byte*)Address);
        }

        public OtelThreadContextPublisher Owner { get; }

        public IntPtr Address { get; }
    }
}
