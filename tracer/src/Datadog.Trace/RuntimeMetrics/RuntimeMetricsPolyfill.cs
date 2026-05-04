// <copyright file="RuntimeMetricsPolyfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Threading;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Polyfill for <c>System.Runtime</c> meter instruments on .NET 6-8.
/// On .NET 9+ the runtime itself publishes these instruments, so this class
/// must NOT be instantiated on .NET 9+ to avoid duplicate instruments.
/// The instrument names, units, and tag keys match the .NET 9 source:
/// https://github.com/dotnet/runtime/blob/v9.0.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/RuntimeMetrics.cs
/// <c>ObservableUpDownCounter&lt;T&gt;</c> was added in <c>System.Diagnostics.DiagnosticSource</c> 7.0.
/// <c>Datadog.Trace</c> compiles against the net6.0 ref assembly which doesn't expose it, so we resolve
/// it via reflection (see <see cref="MeterObservableUpDownCounterReflection"/>) and fall back to
/// <c>ObservableGauge</c> on .NET 6 hosts. The OTLP pipeline handles both correctly.
/// </summary>
internal sealed class RuntimeMetricsPolyfill : IDisposable
{
    internal const string MeterName = "System.Runtime";

    private static readonly string[] GenNames = ["gen0", "gen1", "gen2", "loh", "poh"];
    private static readonly int MaxGenerations = Math.Min(GC.GetGCMemoryInfo().GenerationInfo.Length, GenNames.Length);

    [ThreadStatic]
    private static bool _handlingFirstChanceException;

    private readonly Meter _meter;
    private readonly Process _process;
    private readonly Counter<long>? _exceptions;
    private readonly ConcurrentDictionary<string, KeyValuePair<string, object?>> _exceptionTagCache = new();

    private GCMemoryInfo _cachedGcInfo;
    private long _gcInfoTimestamp;

    public RuntimeMetricsPolyfill()
    {
        _meter = new Meter(MeterName);
        _process = Process.GetCurrentProcess();

        // --- GC ---

        _meter.CreateObservableCounter(
            "dotnet.gc.collections",
            GetGarbageCollectionCounts,
            unit: "{collection}",
            description: "The number of garbage collections that have occurred since the process has started.");

        _meter.CreateObservableGauge(
            "dotnet.process.memory.working_set",
            static () => Environment.WorkingSet,
            unit: "By",
            description: "The number of bytes of physical memory mapped to the process context.");

        _meter.CreateObservableCounter(
            "dotnet.gc.heap.total_allocated",
            static () => GC.GetTotalAllocatedBytes(),
            unit: "By",
            description: "The approximate number of bytes allocated on the managed GC heap since the process has started. The returned value does not include any native allocations.");

        _meter.CreateObservableGauge(
            "dotnet.gc.last_collection.memory.committed_size",
            () => GetCachedGcInfo().TotalCommittedBytes,
            unit: "By",
            description: "The amount of committed virtual memory in use by the .NET GC, as observed during the latest garbage collection.");

        _meter.CreateObservableGauge(
            "dotnet.gc.last_collection.heap.size",
            GetHeapSizes,
            unit: "By",
            description: "The managed GC heap size (including fragmentation), as observed during the latest garbage collection.");

        _meter.CreateObservableGauge(
            "dotnet.gc.last_collection.heap.fragmentation.size",
            GetHeapFragmentation,
            unit: "By",
            description: "The heap fragmentation, as observed during the latest garbage collection.");

        // GC pause time: GC.GetTotalPauseDuration() was added in .NET 6.0.21
        var gcPauseDelegate = GcPauseTimeReflection.TryCreateDelegate();
        if (gcPauseDelegate is not null)
        {
            _meter.CreateObservableCounter(
                "dotnet.gc.pause.time",
                () => gcPauseDelegate().TotalSeconds,
                unit: "s",
                description: "The total amount of time paused in GC since the process has started.");
        }

        // --- JIT ---

        _meter.CreateObservableCounter(
            "dotnet.jit.compiled_il.size",
            static () => JitInfo.GetCompiledILBytes(),
            unit: "By",
            description: "Count of bytes of intermediate language that have been compiled since the process has started.");

        _meter.CreateObservableCounter(
            "dotnet.jit.compiled_methods",
            static () => JitInfo.GetCompiledMethodCount(),
            unit: "{method}",
            description: "The number of times the JIT compiler (re)compiled methods since the process has started.");

        // Description intentionally matches the .NET 9 native meter, which appears to have a copy-paste bug
        // (the description here is identical to the one for `dotnet.jit.compiled_methods` above):
        // https://github.com/dotnet/runtime/blob/v9.0.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/RuntimeMetrics.cs#L96
        _meter.CreateObservableCounter(
            "dotnet.jit.compilation.time",
            static () => JitInfo.GetCompilationTime().TotalSeconds,
            unit: "s",
            description: "The number of times the JIT compiler (re)compiled methods since the process has started.");

        // --- Threading ---

        _meter.CreateObservableCounter(
            "dotnet.monitor.lock_contentions",
            static () => Monitor.LockContentionCount,
            unit: "{contention}",
            description: "The number of times there was contention when trying to acquire a monitor lock since the process has started.");

        _meter.CreateObservableCounter(
            "dotnet.thread_pool.thread.count",
            static () => (long)ThreadPool.ThreadCount,
            unit: "{thread}",
            description: "The number of thread pool threads that currently exist.");

        _meter.CreateObservableCounter(
            "dotnet.thread_pool.work_item.count",
            static () => ThreadPool.CompletedWorkItemCount,
            unit: "{work_item}",
            description: "The number of work items that the thread pool has completed since the process has started.");

        _meter.CreateObservableCounter(
            "dotnet.thread_pool.queue.length",
            static () => ThreadPool.PendingWorkItemCount,
            unit: "{work_item}",
            description: "The number of work items that are currently queued to be processed by the thread pool.");

        // Native .NET 9 RuntimeMetrics emits these as ObservableUpDownCounter, not Gauge, since the
        // values can decrease over time. We do the same on .NET 7+ via reflection, and fall back to
        // ObservableGauge on .NET 6 where the API isn't exposed in the ref assembly.
        RegisterObservableUpDownCounterOrGauge(
            _meter,
            "dotnet.timer.count",
            static () => Timer.ActiveCount,
            unit: "{timer}",
            description: "The number of timer instances that are currently active. An active timer is registered to tick at some point in the future and has not yet been canceled.");

        // --- Assemblies ---

        RegisterObservableUpDownCounterOrGauge(
            _meter,
            "dotnet.assembly.count",
            static () => (long)AppDomain.CurrentDomain.GetAssemblies().Length,
            unit: "{assembly}",
            description: "The number of .NET assemblies that are currently loaded.");

        // --- Exceptions ---

        _exceptions = _meter.CreateCounter<long>(
            "dotnet.exceptions",
            unit: "{exception}",
            description: "The number of exceptions that have been thrown in managed code.");

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

        // --- CPU ---

        _meter.CreateObservableGauge(
            "dotnet.process.cpu.count",
            static () => (long)Environment.ProcessorCount,
            unit: "{cpu}",
            description: "The number of processors available to the process.");

        _meter.CreateObservableCounter(
            "dotnet.process.cpu.time",
            GetCpuTime,
            unit: "s",
            description: "CPU time used by the process.");
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        _meter.Dispose();
        _process.Dispose();
    }

    private static void RegisterObservableUpDownCounterOrGauge<T>(Meter meter, string name, Func<T> observeValue, string unit, string description)
        where T : struct
    {
        if (!MeterObservableUpDownCounterReflection.TryRegister(meter, name, observeValue, unit, description))
        {
            meter.CreateObservableGauge(name, observeValue, unit, description);
        }
    }

    private static Measurement<long>[] GetGarbageCollectionCounts()
    {
        var count = GC.MaxGeneration + 1;
        var measurements = new Measurement<long>[count];
        long collectionsFromHigherGeneration = 0;

        for (int gen = GC.MaxGeneration; gen >= 0; --gen)
        {
            long collectionsFromThisGeneration = GC.CollectionCount(gen);
            measurements[GC.MaxGeneration - gen] = new(collectionsFromThisGeneration - collectionsFromHigherGeneration, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[gen]));
            collectionsFromHigherGeneration = collectionsFromThisGeneration;
        }

        return measurements;
    }

    private GCMemoryInfo GetCachedGcInfo()
    {
        var now = Environment.TickCount64;
        if (now - Volatile.Read(ref _gcInfoTimestamp) > 1000)
        {
            _cachedGcInfo = GC.GetGCMemoryInfo();
            Volatile.Write(ref _gcInfoTimestamp, now);
        }

        return _cachedGcInfo;
    }

    private Measurement<double>[] GetCpuTime()
    {
        _process.Refresh();
        return
        [
            new(_process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "user")),
            new(_process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "system")),
        ];
    }

    private Measurement<long>[] GetHeapSizes()
    {
        var gcInfo = GetCachedGcInfo();
        var measurements = new Measurement<long>[MaxGenerations];

        for (int i = 0; i < MaxGenerations; ++i)
        {
            measurements[i] = new(gcInfo.GenerationInfo[i].SizeAfterBytes, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[i]));
        }

        return measurements;
    }

    private Measurement<long>[] GetHeapFragmentation()
    {
        var gcInfo = GetCachedGcInfo();
        var measurements = new Measurement<long>[MaxGenerations];

        for (int i = 0; i < MaxGenerations; ++i)
        {
            measurements[i] = new(gcInfo.GenerationInfo[i].FragmentationAfterBytes, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[i]));
        }

        return measurements;
    }

    private void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if (_handlingFirstChanceException)
        {
            return;
        }

        _handlingFirstChanceException = true;
        var typeName = e.Exception.GetType().Name;
        var tag = _exceptionTagCache.GetOrAdd(typeName, static name => new KeyValuePair<string, object?>("error.type", name));
        _exceptions?.Add(1, tag);
        _handlingFirstChanceException = false;
    }
}
#endif
