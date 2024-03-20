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
    internal readonly record struct ProbeData(string ProbeId, IAdaptiveSampler Sampler, IProbeProcessor Processor)
    {
        internal static ProbeData Empty = new(string.Empty, null, null);

        public string ProbeId { get; } = ProbeId;

        public IAdaptiveSampler Sampler { get; } = Sampler;

        public IProbeProcessor Processor { get; } = Processor;

        public bool IsEmpty() => this == Empty;
    }
}
