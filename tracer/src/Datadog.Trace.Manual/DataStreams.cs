// <copyright file="DataStreams.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// Provides manual Data Streams Monitoring operations for cases where transaction tracking
/// is not working / missing extractors.
/// </summary>
public static class DataStreams
{
    /// <summary>
    /// Records a Data Streams Monitoring transaction checkpoint on the current active span.
    /// Sets the <c>dsm.transaction.id</c> tag on the span.
    /// </summary>
    /// <param name="transactionId">A stable identifier for the transaction being tracked (e.g. a message ID or trace ID).</param>
    /// <param name="checkpointName">The logical name of the checkpoint (e.g. "kafka-produce", "http-send").</param>
    public static void TrackTransaction(string transactionId, string checkpointName)
    {
        if (string.IsNullOrEmpty(transactionId))
        {
            throw new ArgumentException("Argument is null or empty", nameof(transactionId));
        }

        if (string.IsNullOrEmpty(checkpointName))
        {
            throw new ArgumentException("Argument is null or empty", nameof(checkpointName));
        }

        TrackTransactionInternal(transactionId, checkpointName);
    }

    [Instrumented]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackTransactionInternal(string transactionId, string checkpointName)
    {
    }
}
