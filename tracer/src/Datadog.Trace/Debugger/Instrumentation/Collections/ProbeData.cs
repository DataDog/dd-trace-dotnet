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

        /// <summary>
        /// Checks whether there is nothing to run for this probe: <see cref="Empty"/>, a cleared entry,
        /// or an entry copied while it was being cleared, which can miss only some of its fields.
        /// </summary>
        /// <returns>true if the entry has no processor or no sampler</returns>
        public bool IsEmpty() => Processor is null || Sampler is null;
    }
}
