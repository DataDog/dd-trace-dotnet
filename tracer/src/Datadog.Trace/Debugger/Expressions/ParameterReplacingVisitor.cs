// <copyright file="ParameterReplacingVisitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq.Expressions;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class ParameterReplacingVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _source;
    private readonly ParameterExpression _replacement;

    internal ParameterReplacingVisitor(ParameterExpression source, ParameterExpression replacement)
    {
        _source = source;
        _replacement = replacement;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _source ? _replacement : node;
    }
}
