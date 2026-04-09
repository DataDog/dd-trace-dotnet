// <copyright file="TraceDropReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Agent;

internal enum TraceDropReason
{
    TraceFilter,
    Unsampled,
}

#pragma warning disable SA1649 //  File name must match first type name
internal static class TraceDropReasonExtensions
{
    public static MetricTags.DropReason ToTagReason(this TraceDropReason reason) => reason switch
    {
        TraceDropReason.TraceFilter => MetricTags.DropReason.TraceFilter,
        _ => MetricTags.DropReason.P0Drop,
    };
}
