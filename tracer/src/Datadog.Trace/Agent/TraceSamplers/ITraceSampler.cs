// <copyright file="ITraceSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Agent
{
    internal interface ITraceSampler
    {
        /// <summary>
        /// Determines if a trace chunk should be sampled
        /// </summary>
        /// <param name="trace">The trace chunk to sample</param>
        /// <returns>True if the trace chunk should be sampled, false otherwise.</returns>
        bool Sample(ArraySegment<Span> trace);
    }
}
