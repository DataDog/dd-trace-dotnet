// <copyright file="ProbeMetadataInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Debugger.Instrumentation.Registry
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal readonly record struct ProbeMetadataInfo(string ProbeId, AdaptiveSampler Sampler, ProbeProcessor Processor)
    {
        public string ProbeId { get; } = ProbeId;

        public AdaptiveSampler Sampler { get; } = Sampler;

        /// <summary>
        /// Gets the names of the method's local variable, in the same order as they appear in the method's LocalVarSig.
        /// May contain null entries to denote compiler generated locals whose names are meaningless.
        /// </summary>
        public ProbeProcessor Processor { get; } = Processor;
    }
}
