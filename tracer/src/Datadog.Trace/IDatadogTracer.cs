// <copyright file="IDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        IScopeManager ScopeManager { get; }

        ISampler Sampler { get; }

        ImmutableTracerSettings Settings { get; }

        IScope StartActive(string operationName);

        IScope StartActive(string operationName, ISpanContext parent);

        IScope StartActive(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope, bool finishOnClose);

        void Write(ArraySegment<Span> span);
    }
}
