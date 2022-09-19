// <copyright file="MockTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.TestHelpers;

internal class MockTracer : IDatadogTracer
{
    public MockTracer(string defaultServiceName = null, ISampler sampler = null, ImmutableTracerSettings settings = null)
    {
        DefaultServiceName = defaultServiceName;
        Sampler = sampler;
        Settings = settings ?? new ImmutableTracerSettings(new TracerSettings());
    }

    public string DefaultServiceName { get; }

    public ISampler Sampler { get; }

    public ImmutableTracerSettings Settings { get; }

    public ArraySegment<Span> TraceChunk { get; private set; }

    public void Write(ArraySegment<Span> traceChunk)
    {
        TraceChunk = traceChunk;
    }
}
