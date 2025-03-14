// <copyright file="SpanEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// A SpanEvent represents an event that occurred during the execution of a Span.
/// A Span may have multiple SpanEvents, each with a name, timestamp, and optional attributes.
/// </summary>
internal class SpanEvent
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanEvent>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanEvent"/> class.
    /// </summary>
    /// <param name="name">The name of the event</param>
    /// <param name="timestamp">The timestamp when the event occurred</param>
    /// <param name="attributes">Optional dictionary of attributes for the event</param>
    public SpanEvent(string name, DateTimeOffset timestamp, List<KeyValuePair<string, string>>? attributes = null)
    {
        Name = name;
        Timestamp = timestamp;
        Attributes = attributes;
    }

    public string Name { get; }
    public DateTimeOffset Timestamp { get; }
    public List<KeyValuePair<string, string>>? Attributes { get; private set; }
} 
