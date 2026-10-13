// <copyright file="CompiledExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq.Expressions;
using Datadog.Trace.Debugger.Models;

namespace Datadog.Trace.Debugger.Expressions
{
    internal readonly record struct CompiledExpression<T>
    {
        internal CompiledExpression(
            CompiledExpressionDelegate<T>? @delegate,
            Expression? parsedExpression,
            string? rawExpression,
            EvaluationError[]? errors)
        {
            BudgetedDelegate = @delegate;
            ParsedExpression = parsedExpression;
            RawExpression = rawExpression;
            Errors = errors;
        }

        internal CompiledExpressionDelegate<T>? BudgetedDelegate { get; }

        internal Expression? ParsedExpression { get; }

        internal string? RawExpression { get; }

        internal EvaluationError[]? Errors { get; }
    }
}
