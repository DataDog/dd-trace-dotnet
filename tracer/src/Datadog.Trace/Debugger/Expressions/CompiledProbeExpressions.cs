// <copyright file="CompiledProbeExpressions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Expressions;

internal readonly struct CompiledProbeExpressions
{
    public CompiledProbeExpressions(
        CompiledExpression<string>[]? templates,
        CompiledExpression<bool>? condition,
        CompiledExpression<double>? metric,
        KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[]? decorations)
    {
        Templates = templates;
        Condition = condition;
        Metric = metric;
        Decorations = decorations;
    }

    public CompiledExpression<string>[]? Templates { get; }

    public CompiledExpression<bool>? Condition { get; }

    public CompiledExpression<double>? Metric { get; }

    public KeyValuePair<CompiledExpression<bool>, KeyValuePair<string?, CompiledExpression<string>[]>[]>[]? Decorations { get; }
}
