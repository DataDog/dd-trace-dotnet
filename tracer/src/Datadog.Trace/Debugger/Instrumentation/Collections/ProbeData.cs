// <copyright file="ProbeData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal readonly record struct ProbeData(string ProbeId, AdaptiveSampler Sampler, ProbeProcessor Processor)
    {
        internal static ProbeData Empty = new(string.Empty, null, null);

        public string ProbeId { get; } = ProbeId;

        public AdaptiveSampler Sampler { get; } = Sampler;

        public ProbeProcessor Processor { get; } = Processor;

        public bool IsEmpty() => this == Empty;
    }
}
