// <copyright file="IDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace
{
    /// <summary>
    /// Internal interface used for mocking the Tracer in <see cref="TraceContext"/>, its associated tests,
    /// and the AgentWriterTests
    /// </summary>
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        IGitMetadataTagsProvider GitMetadataTagsProvider { get; }

        ImmutableTracerSettings Settings { get; }

        PerTraceSettings PerTraceSettings { get; }

        void Write(ArraySegment<Span> span);
    }
}
