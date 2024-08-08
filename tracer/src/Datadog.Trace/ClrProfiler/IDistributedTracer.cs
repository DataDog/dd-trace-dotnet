// <copyright file="IDistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Internal;

namespace Datadog.Trace.ClrProfiler
{
    internal interface IDistributedTracer
    {
        bool IsChildTracer { get; }

        IReadOnlyDictionary<string, string> GetSpanContextRaw();

        InternalSpanContext GetSpanContext();

        IInternalScope GetActiveScope();

        void SetSpanContext(IReadOnlyDictionary<string, string> value);

        int? GetSamplingPriority();

        void SetSamplingPriority(int? samplingPriority);

        string GetRuntimeId();
    }
}
