// <copyright file="BenchmarkMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using Datadog.Trace.Ci;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet;

internal static class BenchmarkMetadata
{
    private static readonly ConcurrentDictionary<object, Metadata> MetadataByBenchmark;

    static BenchmarkMetadata()
    {
        MetadataByBenchmark = new();
        CIVisibility.InitializeFromManualInstrumentation();
    }

    public static void GetIds(object key, out TraceId traceId, out ulong spanId)
    {
        var value = MetadataByBenchmark.GetOrAdd(key, @case => new());
        if (value.TraceId is null)
        {
            var useAllBits = CIVisibility.Settings.TracerSettings?.TraceId128BitGenerationEnabled ?? true;
            value.TraceId = RandomIdGenerator.Shared.NextTraceId(useAllBits);
            value.SpanId = RandomIdGenerator.Shared.NextSpanId(useAllBits);
        }

        traceId = value.TraceId.Value;
        spanId = value.SpanId;
    }

    public static void SetStartTime(object key, DateTime dateTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(key, @case => new());
        if (value.StartTime is null || dateTime < value.StartTime)
        {
            value.StartTime = dateTime;
        }
    }

    public static void SetEndTime(object key, DateTime dateTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(key, @case => new());
        if (value.EndTime is null || dateTime > value.EndTime)
        {
            value.EndTime = dateTime;
        }
    }

    public static void GetTimes(object key, out DateTime? startTime, out DateTime? endTime)
    {
        var value = MetadataByBenchmark.GetOrAdd(key, @case => new());
        startTime = value.StartTime;
        endTime = value.EndTime;
    }

    private class Metadata
    {
        public TraceId? TraceId { get; set; }

        public ulong SpanId { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }
    }
}
