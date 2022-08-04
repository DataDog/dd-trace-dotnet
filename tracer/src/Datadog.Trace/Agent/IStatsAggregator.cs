// <copyright file="IStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IStatsAggregator
    {
        /// <summary>
        /// Gets a value indicating whether the Datadog agent supports stats
        /// computation in tracers.
        ///
        /// This will return null if the endpoint discovery request has not
        /// completed.
        /// </summary>
        bool? CanComputeStats { get; }

        /// <summary>
        /// Receives an array of spans and computes stats points for them.
        /// </summary>
        /// <param name="spans">The array of spans to process.</param>
        /// <returns>True if the spans should be kept based on rare stats points or error stats points, false otherwise.</returns>
        bool Add(params Span[] spans);

        /// <summary>
        /// Receives an array of spans and computes stats points for them.
        /// </summary>
        /// <param name="spans">The array of spans to process.</param>
        /// <param name="offset">The array offset of the spans to process.</param>
        /// <param name="count">The number of spans to process.</param>
        /// <returns>True if the spans should be kept based on rare stats points or error stats points, false otherwise.</returns>
        bool AddRange(Span[] spans, int offset, int count);

        Task DisposeAsync();
    }
}
