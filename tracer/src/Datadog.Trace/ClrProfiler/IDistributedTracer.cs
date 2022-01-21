// <copyright file="IDistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler
{
    internal interface IDistributedTracer
    {
        bool IsChildTracer { get; }

        IReadOnlyDictionary<string, string> GetSpanContextRaw();

        SpanContext GetSpanContext();

        IScope GetActiveScope();

        void SetSpanContext(IReadOnlyDictionary<string, string> value);

        SamplingDecision? GetSamplingDecision();

        void SetSamplingDecision(SamplingDecision? samplingDecision);

        string GetRuntimeId();
    }
}
