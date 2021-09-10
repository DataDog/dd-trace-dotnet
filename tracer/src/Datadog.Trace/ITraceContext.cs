// <copyright file="ITraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    internal interface ITraceContext
    {
        DateTimeOffset UtcNow { get; }

        SamplingPriority? SamplingPriority { get; set; }

        Span RootSpan { get; }

        void AddSpan(Span span);

        void CloseSpan(Span span);

        void LockSamplingPriority();

        TimeSpan ElapsedSince(DateTimeOffset date);
    }
}
