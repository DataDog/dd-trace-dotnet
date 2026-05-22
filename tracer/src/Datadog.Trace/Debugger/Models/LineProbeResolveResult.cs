// <copyright file="LineProbeResolveResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements should appear in the correct order - keep result payload types together.
#pragma warning disable SA1402 // File may only contain a single type - keep result payload types together.

namespace Datadog.Trace.Debugger.Models
{
    internal sealed record LineProbeResolveResult(
        LiveProbeResolveStatus Status,
        LineProbeResolveReason Reason = LineProbeResolveReason.None,
        string Message = null,
        LineProbeResolutionDiagnostics Diagnostics = null,
        LineProbeResolveErrorKey ErrorKey = default,
        LineProbeResolveErrorDetails ErrorDetails = default,
        bool ReportError = false);

    internal readonly record struct LineProbeResolveErrorKey(
        LineProbeResolveReason Reason,
        LineProbeFallbackFailureReason FallbackFailureReason = LineProbeFallbackFailureReason.None)
    {
        public bool IsEmpty => Reason == LineProbeResolveReason.None;
    }

    internal readonly record struct LineProbeResolveErrorDetails(
        LineProbeResolveErrorKey Key,
        int BestMatchingTrailingSegments = 0,
        int QualifiedFallbackMatchCount = 0,
        int SameFileNameMatchCount = 0)
    {
        public bool IsEmpty => Key.IsEmpty;
    }
}

#pragma warning restore SA1402
#pragma warning restore SA1201
