// <copyright file="ProbeInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Expressions
{
    internal readonly record struct ProbeInfo(
        string ProbeId,
        ProbeType ProbeType,
        ProbeLocation ProbeLocation,
        EvaluateAt EvaluateAt,
        DebuggerExpression[] Templates,
        DebuggerExpression? Condition,
        DebuggerExpression? Metric)
    {
        internal string ProbeId { get; } = ProbeId;

        internal ProbeType ProbeType { get; } = ProbeType;

        internal ProbeLocation ProbeLocation { get; } = ProbeLocation;

        internal EvaluateAt EvaluateAt { get; } = EvaluateAt;

        internal bool IsFullSnapshot { get; } = ProbeType == ProbeType.Snapshot;

        internal DebuggerExpression[] Templates { get; } = Templates;

        internal DebuggerExpression? Condition { get; } = Condition;

        internal DebuggerExpression? Metric { get; } = Metric;
    }
}
