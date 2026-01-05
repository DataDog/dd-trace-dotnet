// <copyright file="SpanEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// A SpanEvent represents an event that occurred during the execution of a Span.
    /// A Span may have multiple SpanEvents, each with a name, timestamp, and optional attributes.
    /// </summary>
    internal sealed class SpanEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanEvent"/> class.
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="timestamp">The timestamp when the event occurred</param>
        /// <param name="attributes">Optional dictionary of attributes for the event</param>
        public SpanEvent(string name, DateTimeOffset timestamp = default, List<KeyValuePair<string, object>>? attributes = default)
        {
            Name = name;
            Timestamp = timestamp != default ? timestamp : DateTimeOffset.UtcNow;
            Attributes = attributes;
        }

        /// <summary>
        /// Gets the name of the event.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the timestamp when the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the attributes associated with the event.
        /// </summary>
        public List<KeyValuePair<string, object>>? Attributes { get; }
    }
}
