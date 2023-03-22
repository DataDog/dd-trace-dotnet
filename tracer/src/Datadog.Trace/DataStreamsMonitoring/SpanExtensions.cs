// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.DataStreamsMonitoring;

internal static class SpanExtensions
{
    /// <summary>
    /// Sets a DataStreams checkpoint and adds pathway tag to the span
    /// </summary>
    /// <param name="span">The span instance</param>
    /// <param name="manager">The <see cref="DataStreamsManager"/> to use</param>
    /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
    internal static void SetDataStreamsCheckpoint(this Span span, DataStreamsManager manager, string[] edgeTags)
    {
       span.Context.SetCheckpoint(manager, edgeTags);
       var hash = span.Context.PathwayContext?.Hash.Value ?? 0;
       if (hash != 0)
       {
           span.SetTag("pathway.hash", hash.ToString());
       }
    }
}
