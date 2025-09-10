// <copyright file="ITraceContextTuple.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for the ValueTuple(string TraceParent, string TraceState) returned by Azure SDK
    /// </summary>
    internal interface ITraceContextTuple : IDuckType
    {
        /// <summary>
        /// Gets the trace parent (Item1 of the tuple)
        /// </summary>
        string Item1 { get; }

        /// <summary>
        /// Gets the trace state (Item2 of the tuple)
        /// </summary>
        string Item2 { get; }
    }
}
