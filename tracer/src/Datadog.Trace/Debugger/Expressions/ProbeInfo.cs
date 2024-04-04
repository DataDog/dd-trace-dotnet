// <copyright file="ProbeInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Expressions
{
    internal record struct ProbeInfo(
        string ProbeId,
        int ProbeVersion,
        ProbeType ProbeType,
        ProbeLocation ProbeLocation,
        EvaluateAt EvaluateAt,
        MetricKind? MetricKind,
        string MetricName,
        bool HasCondition,
        string[] Tags,
        TargetSpan? TargetSpan,
        CaptureLimitInfo CaptureLimitInfo,
        bool IsEmitted = false)
    {
        internal string ProbeId { get; } = ProbeId;

        internal int ProbeVersion { get; } = ProbeVersion;

        internal ProbeType ProbeType { get; } = ProbeType;

        internal ProbeLocation ProbeLocation { get; } = ProbeLocation;

        internal EvaluateAt EvaluateAt { get; } = EvaluateAt;

        internal bool IsFullSnapshot { get; } = ProbeType == ProbeType.Snapshot;

        internal MetricKind? MetricKind { get; } = MetricKind;

        internal string MetricName { get; } = MetricName;

        internal bool HasCondition { get; } = HasCondition;

        internal string[] Tags { get; } = Tags;

        public TargetSpan? TargetSpan { get; } = TargetSpan;

        public CaptureLimitInfo CaptureLimitInfo { get; } = CaptureLimitInfo;
    }

    internal readonly record struct CaptureLimitInfo(
        int MaxReferenceDepth,
        int MaxCollectionSize,
        int MaxLength,
        int MaxFieldCount)
    {
        public int MaxReferenceDepth { get; } = MaxReferenceDepth;

        public int MaxCollectionSize { get; } = MaxCollectionSize;

        public int MaxLength { get; } = MaxLength;

        public int MaxFieldCount { get; } = MaxFieldCount;
    }
}
