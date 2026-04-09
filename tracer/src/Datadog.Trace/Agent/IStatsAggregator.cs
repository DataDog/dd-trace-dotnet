// <copyright file="IStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Agent
{
    internal interface IStatsAggregator
    {
        /// <summary>
        /// Gets a value indicating whether the Datadog agent supports stats
        /// computation in tracers.
        ///
        /// This will return null if 1) enabled by configuration and 2) the
        /// endpoint discovery request has not yet completed.
        ///
        /// This will return true if 1) enabled by configuration, 2) the
        /// endpoint discovery request confirmed that the agent has a stats
        /// endpoint, and 3) the agent confirms it supports the feature
        /// "client_drop_p0s: true".
        ///
        /// This will return false otherwise.
        /// </summary>
        bool? CanComputeStats { get; }

        /// <summary>
        /// Receives an array of spans and computes stats points for them.
        /// </summary>
        /// <param name="spans">The array of spans to process.</param>
        [TestingOnly]
        void Add(params Span[] spans);

        /// <summary>
        /// Receives an array of spans and computes stats points for them.
        /// </summary>
        /// <param name="spans">The ArraySegment of spans to process.</param>
        void AddRange(in SpanCollection spans);

        /// <summary>
        /// Apply normalization, filtering, obfuscation, and sampling, to understand if the
        /// trace should be kept
        /// </summary>
        /// <param name="spans">The spans chunk to process</param>
        /// <returns>An optional trace drop reason, or <c>null</c> if the trace should _not_ be dropped</returns>
        TraceDropReason? ProcessTrace(ref SpanCollection spans);

        Task DisposeAsync();

        StatsAggregationKey BuildKey(Span span, out List<byte[]> utf8PeerTags);
    }
}
