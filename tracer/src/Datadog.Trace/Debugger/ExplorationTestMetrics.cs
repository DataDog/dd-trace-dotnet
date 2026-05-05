// <copyright file="ExplorationTestMetrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger;

/// <summary>
/// Collects timing metrics during snapshot exploration tests.
/// Thread-safe counters for expression compilation, evaluation, and serialization.
/// Writes summary to a file at process exit for analysis.
/// </summary>
internal static class ExplorationTestMetrics
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExplorationTestMetrics));

    // Timing accumulators (in ticks for precision)
    private static long _expressionCompilationTicks;
    private static long _expressionEvaluationTicks;
    private static long _snapshotSerializationTicks;
    private static long _snapshotRootEnumerableTicks;
    private static long _snapshotRootObjectTicks;
    private static long _snapshotPruningTicks;
    private static long _snapshotSinkWriteTicks;
    private static long _probeProcessingTicks;

    // Counters
    private static long _expressionCompilationCount;
    private static long _expressionEvaluationCount;
    private static long _snapshotSerializationCount;
    private static long _snapshotRootEnumerableCount;
    private static long _snapshotRootObjectCount;
    private static long _snapshotPruningCount;
    private static long _snapshotSinkWriteCount;
    private static long _probeHitCount;
    private static long _cacheHitCount;
    private static long _cacheMissCount;
    private static long _probesRemovedCount;
    private static long _snapshotsSkippedCount;

    // Not-captured counters (counts only)
    private static long _snapshotTimeoutCount;

    // Output path (set when enabled)
    private static string _metricsFilePath = string.Empty;
    private static bool _isEnabled;
    private static bool _isRegistered;

    public static bool IsEnabled => _isEnabled;

    /// <summary>
    /// Enables metrics collection and registers process exit handler.
    /// </summary>
    public static void Enable(string reportFolderPath)
    {
        if (_isEnabled)
        {
            return;
        }

        _isEnabled = true;
        _metricsFilePath = Path.Combine(reportFolderPath, "exploration_test_metrics.csv");

        if (!_isRegistered)
        {
            _isRegistered = true;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        Log.Information("Exploration test metrics enabled. Output: {Path}", _metricsFilePath);
    }

    /// <summary>
    /// Records expression compilation time.
    /// </summary>
    public static void RecordExpressionCompilation(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _expressionCompilationTicks, elapsedTicks);
        Interlocked.Increment(ref _expressionCompilationCount);
    }

    /// <summary>
    /// Records expression evaluation time.
    /// </summary>
    public static void RecordExpressionEvaluation(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _expressionEvaluationTicks, elapsedTicks);
        Interlocked.Increment(ref _expressionEvaluationCount);
    }

    /// <summary>
    /// Records snapshot serialization time.
    /// </summary>
    public static void RecordSnapshotSerialization(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _snapshotSerializationTicks, elapsedTicks);
        Interlocked.Increment(ref _snapshotSerializationCount);
    }

    /// <summary>
    /// Records snapshot serialization time where the root value is an enumerable/dictionary.
    /// This is measured only at depth==0 to avoid double-counting recursive calls.
    /// </summary>
    public static void RecordSnapshotRootEnumerable(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _snapshotRootEnumerableTicks, elapsedTicks);
        Interlocked.Increment(ref _snapshotRootEnumerableCount);
    }

    /// <summary>
    /// Records snapshot serialization time where the root value is a regular object (non-enumerable).
    /// This is measured only at depth==0 to avoid double-counting recursive calls.
    /// </summary>
    public static void RecordSnapshotRootObject(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _snapshotRootObjectTicks, elapsedTicks);
        Interlocked.Increment(ref _snapshotRootObjectCount);
    }

    /// <summary>
    /// Records snapshot pruning time (size reduction / slicing).
    /// </summary>
    public static void RecordSnapshotPruning(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _snapshotPruningTicks, elapsedTicks);
        Interlocked.Increment(ref _snapshotPruningCount);
    }

    /// <summary>
    /// Records snapshot sink write time (report/file enqueue/write).
    /// </summary>
    public static void RecordSnapshotSinkWrite(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _snapshotSinkWriteTicks, elapsedTicks);
        Interlocked.Increment(ref _snapshotSinkWriteCount);
    }

    /// <summary>
    /// Records when snapshot serialization hits the max serialization timeout.
    /// </summary>
    public static void RecordSnapshotTimeout()
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Increment(ref _snapshotTimeoutCount);
    }

    /// <summary>
    /// Records total probe processing time (from method entry to snapshot complete).
    /// </summary>
    public static void RecordProbeProcessing(long elapsedTicks)
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Add(ref _probeProcessingTicks, elapsedTicks);
        Interlocked.Increment(ref _probeHitCount);
    }

    /// <summary>
    /// Records expression cache hit.
    /// </summary>
    public static void RecordCacheHit()
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Increment(ref _cacheHitCount);
    }

    /// <summary>
    /// Records expression cache miss (compilation needed).
    /// </summary>
    public static void RecordCacheMiss()
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Increment(ref _cacheMissCount);
    }

    /// <summary>
    /// Records when a probe is removed after reaching its snapshot limit.
    /// </summary>
    public static void RecordProbeRemoval()
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Increment(ref _probesRemovedCount);
    }

    /// <summary>
    /// Records when a snapshot is skipped because the probe exceeded its limit.
    /// </summary>
    public static void RecordSnapshotSkipped()
    {
        if (!_isEnabled)
        {
            return;
        }

        Interlocked.Increment(ref _snapshotsSkippedCount);
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (!_isEnabled || string.IsNullOrEmpty(_metricsFilePath))
        {
            return;
        }

        try
        {
            var ticksPerMs = Stopwatch.Frequency / 1000.0;

            var compilationMs = _expressionCompilationTicks / ticksPerMs;
            var evaluationMs = _expressionEvaluationTicks / ticksPerMs;
            var serializationMs = _snapshotSerializationTicks / ticksPerMs;
            var rootEnumerableMs = _snapshotRootEnumerableTicks / ticksPerMs;
            var rootObjectMs = _snapshotRootObjectTicks / ticksPerMs;
            var pruningMs = _snapshotPruningTicks / ticksPerMs;
            var sinkWriteMs = _snapshotSinkWriteTicks / ticksPerMs;
            var processingMs = _probeProcessingTicks / ticksPerMs;

            var avgCompilationMs = _expressionCompilationCount > 0 ? compilationMs / _expressionCompilationCount : 0;
            var avgEvaluationMs = _expressionEvaluationCount > 0 ? evaluationMs / _expressionEvaluationCount : 0;
            var avgSerializationMs = _snapshotSerializationCount > 0 ? serializationMs / _snapshotSerializationCount : 0;
            var avgRootEnumerableMs = _snapshotRootEnumerableCount > 0 ? rootEnumerableMs / _snapshotRootEnumerableCount : 0;
            var avgRootObjectMs = _snapshotRootObjectCount > 0 ? rootObjectMs / _snapshotRootObjectCount : 0;
            var avgPruningMs = _snapshotPruningCount > 0 ? pruningMs / _snapshotPruningCount : 0;
            var avgSinkWriteMs = _snapshotSinkWriteCount > 0 ? sinkWriteMs / _snapshotSinkWriteCount : 0;
            var avgProcessingMs = _probeHitCount > 0 ? processingMs / _probeHitCount : 0;

            var cacheHitRate = (_cacheHitCount + _cacheMissCount) > 0
                ? (double)_cacheHitCount / (_cacheHitCount + _cacheMissCount) * 100
                : 0;

            // Write CSV format for easy parsing
            using var writer = new StreamWriter(_metricsFilePath);
            writer.WriteLine("Metric,TotalMs,Count,AvgMs");
            writer.WriteLine($"ExpressionCompilation,{compilationMs:F2},{_expressionCompilationCount},{avgCompilationMs:F3}");
            writer.WriteLine($"ExpressionEvaluation,{evaluationMs:F2},{_expressionEvaluationCount},{avgEvaluationMs:F3}");
            writer.WriteLine($"SnapshotSerialization,{serializationMs:F2},{_snapshotSerializationCount},{avgSerializationMs:F3}");
            writer.WriteLine($"SnapshotRootEnumerable,{rootEnumerableMs:F2},{_snapshotRootEnumerableCount},{avgRootEnumerableMs:F3}");
            writer.WriteLine($"SnapshotRootObject,{rootObjectMs:F2},{_snapshotRootObjectCount},{avgRootObjectMs:F3}");
            writer.WriteLine($"SnapshotPruning,{pruningMs:F2},{_snapshotPruningCount},{avgPruningMs:F3}");
            writer.WriteLine($"SnapshotSinkWrite,{sinkWriteMs:F2},{_snapshotSinkWriteCount},{avgSinkWriteMs:F3}");
            writer.WriteLine($"ProbeProcessing,{processingMs:F2},{_probeHitCount},{avgProcessingMs:F3}");
            writer.WriteLine($"SnapshotTimeouts,0,{_snapshotTimeoutCount},0");
            writer.WriteLine($"CacheHits,0,{_cacheHitCount},0");
            writer.WriteLine($"CacheMisses,0,{_cacheMissCount},0");
            writer.WriteLine($"CacheHitRate,{cacheHitRate:F2},0,0");
            writer.WriteLine($"ProbesRemoved,0,{_probesRemovedCount},0");
            writer.WriteLine($"SnapshotsSkipped,0,{_snapshotsSkippedCount},0");

            Log.Information("Exploration test metrics written to {Path}", _metricsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to write exploration test metrics");
        }
    }
}
