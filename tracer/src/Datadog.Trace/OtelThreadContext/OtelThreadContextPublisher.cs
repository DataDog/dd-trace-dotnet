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
/// Each OS thread owns one record. The address of that record is set into the thread's
/// <c>otel_thread_ctx_v1</c> slot once, the first time the thread carries an active span, and remains
/// set until native thread teardown - so the only native call on the whole feature happens once per
/// thread, and every subsequent context change is a handful of direct managed writes into unmanaged memory.
/// See docs/OTelContextPropagation.md.
/// </para>
/// </summary>
internal sealed class OtelThreadContextPublisher : IOtelThreadContextPublisher
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OtelThreadContextPublisher>();

    // Per-OS-thread state. This is a [ThreadStatic] rather than a ThreadLocal<T> because it is read on
    // every span activation, and a static field access is materially cheaper. It holds the publisher that
    // created it so that a new publisher instance never reuses another one's record (which matters for
    // tests more than for production, where there is a single publisher).
    [ThreadStatic]
    private static ThreadRecord? _threadRecord;

    private readonly IOtelThreadContextRecordProvider _recordProvider;
    private int _disabled;

    internal OtelThreadContextPublisher(IOtelThreadContextRecordProvider recordProvider)
    {
        _recordProvider = recordProvider;
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
        if (!IsPlatformSupported(framework))
        {
            Log.Warning<string, string>(
                "OpenTelemetry thread context publication was requested but is unavailable on {OSPlatform}/{ProcessArchitecture}.",
                framework.OSPlatform,
                framework.ProcessArchitecture);
            return NullOtelThreadContextPublisher.Instance;
        }

        // The P/Invoke to the native tracer is only usable under automatic instrumentation, because that
        // is what rewrites the P/Invoke map to point at the deployed native library.
        if (!EnvironmentHelpersNoLogging.IsClrProfilerAttachedSafe())
        {
            Log.Warning("OpenTelemetry thread context publication was requested but is unavailable as instrumentation is not attached.");

            return NullOtelThreadContextPublisher.Instance;
        }

        return new OtelThreadContextPublisher(OtelThreadContextRecordProvider.Instance);
    }

    /// <summary>
    /// Return true if the current platform can publish thread contexts at all. OTEP 4947
    /// is Linux-only: it relies on ELF thread-local storage, and its readers are themselves
    /// Linux-specific (the OpenTelemetry eBPF profiler, OBI).
    /// </summary>
    internal static bool IsPlatformSupported(FrameworkDescription framework)
    {
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
            OtelThreadContextRecord.Write(record.Address, span);
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    public void Reset()
    {
        // Deliberately does not initialize a record: a thread that has never published a context has a
        // null exported per-thread slot, which already means "no context" to a reader.
        var record = _threadRecord;
        if (record is null || record.Owner != this || !IsEnabled)
        {
            return;
        }

        try
        {
            OtelThreadContextRecord.Invalidate(record.Address);
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
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
            var address = _recordProvider.GetRecord();

            if (address == IntPtr.Zero)
            {
                Disable("the native tracer did not provide a thread context record");
                return null;
            }

            // Native code publishes a zeroed, invalid record before returning it.
            // Initialize the fixed record fields before the first context is written.
            OtelThreadContextRecord.Initialize(address);

            var record = new ThreadRecord(this, address);
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
    /// Caches one thread's native-owned record and the publisher that acquired it.
    /// </summary>
    private sealed class ThreadRecord
    {
        public ThreadRecord(OtelThreadContextPublisher owner, IntPtr address)
        {
            Owner = owner;
            Address = address;
        }

        public OtelThreadContextPublisher Owner { get; }

        public IntPtr Address { get; }
    }
}
