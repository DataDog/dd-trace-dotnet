// <copyright file="RuntimeMetricsPolyfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Polyfill for <c>System.Runtime</c> meter instruments on .NET 6-8.
/// On .NET 9+ the runtime itself publishes these instruments, so this class
/// must NOT be instantiated on .NET 9+ to avoid duplicate instruments.
/// The instrument names, units, and tag keys match the .NET 9 source:
/// https://github.com/dotnet/runtime/blob/v9.0.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/RuntimeMetrics.cs
/// .NET 6 does not have ObservableUpDownCounter, so we use ObservableGauge as a fallback
/// for point-in-time values. The OTLP pipeline handles both correctly.
/// </summary>
internal sealed class RuntimeMetricsPolyfill : IDisposable
{
    internal const string MeterName = "System.Runtime";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RuntimeMetricsPolyfill>();
    private static readonly string[] GenNames = ["gen0", "gen1", "gen2", "loh", "poh"];
    private static readonly int MaxGenerations = Math.Min(GC.GetGCMemoryInfo().GenerationInfo.Length, GenNames.Length);

    // Matches the .NET 9 RuntimeMetrics ThreadStatic pattern to prevent recursion
    // in FirstChanceException handler. The t_ prefix is the .NET convention for ThreadStatic fields.
#pragma warning disable SA1308
    [ThreadStatic]
    private static bool t_handlingFirstChanceException;
#pragma warning restore SA1308

    private readonly Meter _meter;
    private readonly Counter<long>? _exceptions;

    public RuntimeMetricsPolyfill()
    {
        _meter = new Meter(MeterName);

        // --- GC ---

        _meter.CreateObservableCounter(
            "dotnet.gc.collections",
            GetGarbageCollectionCounts,
            unit: "{collection}",
            description: "The number of garbage collections that have occurred since the process has started.");

        _meter.CreateObservableGauge(
            "dotnet.process.memory.working_set",
            () => Environment.WorkingSet,
            unit: "By",
            description: "The number of bytes of physical memory mapped to the process context.");

        _meter.CreateObservableCounter(
            "dotnet.gc.heap.total_allocated",
            () => GC.GetTotalAllocatedBytes(),
            unit: "By",
            description: "The approximate number of bytes allocated on the managed GC heap since the process has started. The returned value does not include any native allocations.");

        _meter.CreateObservableGauge(
            "dotnet.gc.last_collection.memory.committed_size",
            () => GC.GetGCMemoryInfo().TotalCommittedBytes,
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
        var getTotalPauseSeconds = CreateGetTotalPauseSecondsDelegate();
        if (getTotalPauseSeconds is not null)
        {
            _meter.CreateObservableCounter(
                "dotnet.gc.pause.time",
                getTotalPauseSeconds,
                unit: "s",
                description: "The total amount of time paused in GC since the process has started.");
        }

        // --- JIT ---

        _meter.CreateObservableCounter(
            "dotnet.jit.compiled_il.size",
            () => JitInfo.GetCompiledILBytes(),
            unit: "By",
            description: "Count of bytes of intermediate language that have been compiled since the process has started.");

        _meter.CreateObservableCounter(
            "dotnet.jit.compiled_methods",
            () => JitInfo.GetCompiledMethodCount(),
            unit: "{method}",
            description: "The number of times the JIT compiler (re)compiled methods since the process has started.");

        _meter.CreateObservableCounter(
            "dotnet.jit.compilation.time",
            () => JitInfo.GetCompilationTime().TotalSeconds,
            unit: "s",
            description: "The amount of time the JIT compiler has spent compiling methods since the process has started.");

        // --- Threading ---

        _meter.CreateObservableCounter(
            "dotnet.monitor.lock_contentions",
            () => Monitor.LockContentionCount,
            unit: "{contention}",
            description: "The number of times there was contention when trying to acquire a monitor lock since the process has started.");

        // .NET 9 uses ObservableCounter here, but thread count can decrease when threads
        // are retired. ObservableCounter + delta temporality produces negative deltas in that
        // case. Use ObservableGauge so the pipeline reports the last observed value directly.
        _meter.CreateObservableGauge(
            "dotnet.thread_pool.thread.count",
            () => (long)ThreadPool.ThreadCount,
            unit: "{thread}",
            description: "The number of thread pool threads that currently exist.");

        _meter.CreateObservableCounter(
            "dotnet.thread_pool.work_item.count",
            () => ThreadPool.CompletedWorkItemCount,
            unit: "{work_item}",
            description: "The number of work items that the thread pool has completed since the process has started.");

        // .NET 9 uses ObservableCounter here, but queue length can decrease as items are
        // dequeued. Use ObservableGauge to avoid negative deltas (same rationale as thread.count).
        _meter.CreateObservableGauge(
            "dotnet.thread_pool.queue.length",
            () => ThreadPool.PendingWorkItemCount,
            unit: "{work_item}",
            description: "The number of work items that are currently queued to be processed by the thread pool.");

        _meter.CreateObservableGauge(
            "dotnet.timer.count",
            () => Timer.ActiveCount,
            unit: "{timer}",
            description: "The number of timer instances that are currently active.");

        // --- Assemblies ---

        _meter.CreateObservableGauge(
            "dotnet.assembly.count",
            () => (long)AppDomain.CurrentDomain.GetAssemblies().Length,
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
            () => (long)Environment.ProcessorCount,
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
    }

    private static Func<double>? CreateGetTotalPauseSecondsDelegate()
    {
        var version = Environment.Version;
        if (version.Major > 6 || version is { Major: 6, Build: >= 21 })
        {
            var methodInfo = typeof(GC).GetMethod("GetTotalPauseDuration", BindingFlags.Public | BindingFlags.Static);
            if (methodInfo is not null)
            {
                var getTotalPauseDuration = methodInfo.CreateDelegate<Func<TimeSpan>>();
                return () => getTotalPauseDuration().TotalSeconds;
            }
        }

        Log.Debug("GC.GetTotalPauseDuration() is not available on this runtime version; dotnet.gc.pause.time will not be reported.");
        return null;
    }

    private static IEnumerable<Measurement<long>> GetGarbageCollectionCounts()
    {
        long collectionsFromHigherGeneration = 0;

        for (int gen = GC.MaxGeneration; gen >= 0; --gen)
        {
            long collectionsFromThisGeneration = GC.CollectionCount(gen);
            yield return new(collectionsFromThisGeneration - collectionsFromHigherGeneration, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[gen]));
            collectionsFromHigherGeneration = collectionsFromThisGeneration;
        }
    }

    private static IEnumerable<Measurement<double>> GetCpuTime()
    {
        using var process = Process.GetCurrentProcess();
        yield return new(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "user"));
        yield return new(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "system"));
    }

    private static IEnumerable<Measurement<long>> GetHeapSizes()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        for (int i = 0; i < MaxGenerations; ++i)
        {
            yield return new(gcInfo.GenerationInfo[i].SizeAfterBytes, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[i]));
        }
    }

    private static IEnumerable<Measurement<long>> GetHeapFragmentation()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        for (int i = 0; i < MaxGenerations; ++i)
        {
            yield return new(gcInfo.GenerationInfo[i].FragmentationAfterBytes, new KeyValuePair<string, object?>("gc.heap.generation", GenNames[i]));
        }
    }

    private void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if (t_handlingFirstChanceException)
        {
            return;
        }

        t_handlingFirstChanceException = true;
        _exceptions?.Add(1, new KeyValuePair<string, object?>("error.type", e.Exception.GetType().Name));
        t_handlingFirstChanceException = false;
    }
}

#endif
