// <copyright file="ITimestamp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Timestamp struct for duck-typing
    /// Requires boxing, but necessary as we need to duck-type <see cref="Type"/> too
    /// </summary>
    public interface ITimestamp
    {
        /// <summary>
        /// Gets the timestamp type
        /// </summary>
        public int Type { get; }

        /// <summary>
        /// Gets the UTC DateTime for the timestamp
        /// </summary>
        public DateTime UtcDateTime { get; }
    }
}
