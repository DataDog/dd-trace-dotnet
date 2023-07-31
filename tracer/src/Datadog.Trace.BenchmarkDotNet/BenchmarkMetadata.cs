// <copyright file="BenchmarkMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using BenchmarkDotNet.Running;
using Datadog.Trace.Ci;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet;

internal static class BenchmarkMetadata
{
    private static readonly ConcurrentDictionary<BenchmarkCase, Metadata> MetadataByBenchmark;

    static BenchmarkMetadata()
    {
        MetadataByBenchmark = new();
        CIVisibility.InitializeFromManualInstrumentation();
    }

    public static void GetIds(BenchmarkCase benchmarkCase, out TraceId traceId, out ulong spanId)
    {
        var value = MetadataByBenchmark.GetOrAdd(benchmarkCase, @case => new());
        if (value.TraceId is null)
        {
            var useAllBits = CIVisibility.Settings.TracerSettings?.TraceId128BitGenerationEnabled ?? false;
            value.TraceId = RandomIdGenerator.Shared.NextTraceId(useAllBits);
            value.SpanId = RandomIdGenerator.Shared.NextSpanId(useAllBits);
        }

        traceId = value.TraceId.Value;
        spanId = value.SpanId;
    }

    public static void SetStartTime(BenchmarkCase benchmarkCase, DateTime dateTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(benchmarkCase, @case => new());
        if (dateTime < value.StartTime)
        {
            value.StartTime = dateTime;
        }
    }

    public static void SetEndTime(BenchmarkCase benchmarkCase, DateTime dateTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(benchmarkCase, @case => new());
        if (dateTime > value.EndTime)
        {
            value.EndTime = dateTime;
        }
    }

    public static void GetTimes(BenchmarkCase benchmarkCase, out DateTime startTime, out DateTime endTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(benchmarkCase, @case => new());
        startTime = value.StartTime;
        endTime = value.EndTime;
    }

    private class Metadata
    {
        public TraceId? TraceId { get; set; }

        public ulong SpanId { get; set; }

        public DateTime StartTime { get; set; } = DateTime.MaxValue;

        public DateTime EndTime { get; set; } = DateTime.MinValue;
    }
}
