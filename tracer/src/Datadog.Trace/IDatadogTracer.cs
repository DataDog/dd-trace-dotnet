// <copyright file="IDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    /// <summary>
    /// Internal interface used for mocking the Tracer in <see cref="TraceContext"/> and its associated tests
    /// </summary>
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        ISampler Sampler { get; }

        ImmutableTracerSettings Settings { get; }

        Span StartSpan(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

        void Write(ArraySegment<Span> span);
    }
}
