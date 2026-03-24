// <copyright file="DataStreams.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// Provides manual Data Streams Monitoring operations for cases where auto-instrumentation
    /// cannot propagate transaction tracking automatically.
    /// </summary>
    public static class DataStreams
    {
        /// <summary>
        /// Records a Data Streams Monitoring transaction checkpoint on the given span.
        /// Sets the <c>dsm.transaction.id</c> tag on the span and sends the transaction
        /// to the Data Streams backend (if DSM is enabled).
        /// </summary>
        /// <param name="span">The active span representing the current operation.</param>
        /// <param name="transactionId">A stable identifier for the transaction being tracked (e.g. a message ID or trace ID).</param>
        /// <param name="checkpointName">The logical name of the checkpoint (e.g. "kafka-produce", "http-send").</param>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TrackTransaction(ISpan span, string transactionId, string checkpointName)
        {
        }
    }
}
