// <copyright file="DataStreamsCheckpointOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Options for a Data Streams Monitoring checkpoint set through <c>SpanContextInjector.InjectIncludingDsm</c>
    /// or <c>SpanContextExtractor.ExtractIncludingDsm</c>. Use them for stages that have no instrumented
    /// producer, such as data read from a database through CDC or polling, so the time spent there is
    /// included in the pathway. A checkpoint with options is set even when the carrier has no trace headers.
    /// </summary>
    public struct DataStreamsCheckpointOptions
    {
        /// <summary>
        /// Gets or sets when this checkpoint happened, for example when a change was committed in a database.
        /// If not set, uses the current time.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets when the data entered the source this checkpoint reads from, for example when a row
        /// was originally written. Only used when the checkpoint starts a pathway: the first edge is then
        /// measured from this time.
        /// </summary>
        public DateTimeOffset? SourceTimestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to link this checkpoint to the previous checkpoint in the same async flow
        /// when the carrier has no pathway context. Use it to record several stages in a row before a produce.
        /// If not set, a checkpoint with no pathway in its carrier starts a new pathway.
        /// </summary>
        public bool ChainFromPrevious { get; set; }
    }
}
