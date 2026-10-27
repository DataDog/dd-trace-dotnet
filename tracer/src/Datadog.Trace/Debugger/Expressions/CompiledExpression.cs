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

        /// <summary>
        /// Gets the compiled delegate. It is null for a literal and for an expression created with
        /// <see cref="WithoutDelegate"/>, which has no value: callers report its <see cref="Errors"/> instead.
        /// </summary>
        internal CompiledExpressionDelegate<T>? BudgetedDelegate { get; }

        internal Expression? ParsedExpression { get; }

        internal string? RawExpression { get; }

        internal EvaluationError[]? Errors { get; }

        /// <summary>
        /// Creates an expression that is never evaluated. Because it has errors and no parsed expression, it is
        /// neither a literal nor an evaluable expression.
        /// </summary>
        internal static CompiledExpression<T> WithoutDelegate(string rawExpression, EvaluationError[] errors)
        {
            return new CompiledExpression<T>(null, null, rawExpression, errors);
        }
    }
}
