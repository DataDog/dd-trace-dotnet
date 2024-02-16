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
    /// <param name="checkpointKind">The type of checkpoint we're setting</param>
    /// <param name="edgeTags">The edge tags for this checkpoint. NOTE: These MUST be sorted alphabetically</param>
    /// <param name="payloadSizeBytes">Payload size in bytes</param>
    /// <param name="timeInQueueMs">Edge start time extracted from the message metadata. Used only if this is start of the pathway</param>
    /// <param name="parent">The parent context, if it was read from a message header for instance</param>
    internal static void SetDataStreamsCheckpoint(this Span span, DataStreamsManager? manager, CheckpointKind checkpointKind, string[] edgeTags, long payloadSizeBytes, long timeInQueueMs, PathwayContext? parent = null)
    {
        if (manager == null)
        {
            return;
        }

        span.Context.SetCheckpoint(manager, checkpointKind, edgeTags, payloadSizeBytes, timeInQueueMs, parent);
        var hash = span.Context.PathwayContext?.Hash.Value ?? 0;
        if (hash != 0)
        {
            span.SetTag("pathway.hash", hash.ToString());
        }
    }
}
