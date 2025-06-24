// <copyright file="PropagatorType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Propagators;

#nullable enable

internal enum PropagatorType
{
    Undefined = 0,

    /// <summary>
    /// A propagator that extracts and injects trace context as <see cref="Datadog.Trace.SpanContext"/>.
    /// </summary>
    TraceContext,

    /// <summary>
    /// A propagator that extracts and injects baggage as <see cref="Datadog.Trace.Baggage"/>.
    /// </summary>
    Baggage,
}
