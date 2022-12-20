// <copyright file="ProbeInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Conditions
{
    internal readonly record struct ProbeInfo(string ProbeId, ProbeType ProbeType, EvaluateAt EvaluateAt)
    {
        internal string ProbeId { get; } = ProbeId;

        internal ProbeType ProbeType { get; } = ProbeType;

        internal EvaluateAt EvaluateAt { get; } = EvaluateAt;
    }
}
