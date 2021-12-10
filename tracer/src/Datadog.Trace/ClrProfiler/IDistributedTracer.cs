// <copyright file="IDistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler
{
    internal interface IDistributedTracer
    {
        IReadOnlyDictionary<string, string> GetSpanContextRaw();

        SpanContext GetSpanContext();

        IScope GetActiveScope();

        void SetSpanContext(IReadOnlyDictionary<string, string> value);

        void LockSamplingPriority();

        SamplingPriority? TrySetSamplingPriority(SamplingPriority? samplingPriority);
    }
}
