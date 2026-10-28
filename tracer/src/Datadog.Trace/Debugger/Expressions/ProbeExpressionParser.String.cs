// <copyright file="ProbeExpressionParser.String.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private static bool RegexIsMatch(string source, string pattern, ref EvaluationBudget budget)
    {
        budget.ThrowIfExceeded();
        try
        {
            return Regex.IsMatch(source, pattern, RegexOptions.None, budget.GetRemainingTimeout());
        }
        catch (RegexMatchTimeoutException ex)
        {
            budget.MarkTimedOut();
            throw new EvaluationTimeBudgetExceededException(ex);
        }
    }

    private Expression RegexMatches(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var source = ParseTree(reader, parameters, itParameter);
        if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return source;
        }

        var pattern = ParseTree(reader, parameters, itParameter);
        if (IsRedactedValue(source) || IsRedactedValue(pattern))
        {
            return Expression.Constant(false);
        }

        var matchesMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(RegexIsMatch), [typeof(string), typeof(string), typeof(EvaluationBudget).MakeByRefType()]);
        return RedactOperation(source, pattern, Expression.Call(null, matchesMethod, source, pattern, _evaluationBudgetParameterExpression));
    }

    private Expression Contains(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var containsMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), nameof(string.Contains), new[] { typeof(string) });
        return CallStringMethod(reader, parameters, itParameter, containsMethod);
    }

    private Expression EndsWith(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var endWithMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), nameof(string.EndsWith), new[] { typeof(string) });
        return CallStringMethod(reader, parameters, itParameter, endWithMethod);
    }

    private Expression StartsWith(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var startWithMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), nameof(string.StartsWith), new[] { typeof(string) });
        return CallStringMethod(reader, parameters, itParameter, startWithMethod);
    }

    private Expression Substring(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var substringMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), nameof(string.Substring), new[] { typeof(int), typeof(int) });
        var source = ParseTree(reader, parameters, itParameter);
        if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return source;
        }

        var startIndex = ParseTree(reader, parameters, itParameter);
        var endIndex = ParseTree(reader, parameters, itParameter);
        if (IsRedactedValue(source) || IsRedactedValue(startIndex) || IsRedactedValue(endIndex))
        {
            return RedactedValue();
        }

        var lengthExpr = Expression.Subtract(endIndex, startIndex);
        return RedactOperationWithCheck(
            CombineRedactionChecks(CombineRedactionChecks(CombineRedactionChecks(null, source), startIndex), endIndex),
            Expression.Block(
                BudgetCheck(),
                Expression.Call(source, substringMethod, startIndex, lengthExpr)));
    }

    private Expression IsEmpty(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var source = ParseTree(reader, parameters, itParameter);
        if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return source;
        }

        if (IsRedactedValue(source))
        {
            return Expression.Constant(false);
        }

        if (source.Type == typeof(string))
        {
            var emptyMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), nameof(string.IsNullOrEmpty), [typeof(string)]);
            return RedactOperation(
                source,
                Expression.Block(
                    BudgetCheck(),
                    Expression.Call(null, emptyMethod, source)));
        }

        try
        {
            var collectionCount = CollectionAndStringLengthExpression(source);
            return RedactOperation(
                source,
                Expression.Block(
                    BudgetCheck(),
                    Expression.Equal(collectionCount, Expression.Constant(0))));
        }
        catch (InvalidOperationException e)
        {
            AddError($"{source?.ToString() ?? "N/A"}.IsEmpty", e.Message + ", or a string");
            return ReturnDefaultValueExpression();
        }
    }

    private Expression CallStringMethod(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter, MethodInfo method)
    {
        var source = ParseTree(reader, parameters, itParameter);
        if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
        {
            return source;
        }

        var parameter = ParseTree(reader, parameters, itParameter);
        if (IsRedactedValue(source) || IsRedactedValue(parameter))
        {
            return Expression.Constant(false);
        }

        return RedactOperation(
            source,
            parameter,
            Expression.Block(
                BudgetCheck(),
                Expression.Call(source, method, parameter)));
    }
}
