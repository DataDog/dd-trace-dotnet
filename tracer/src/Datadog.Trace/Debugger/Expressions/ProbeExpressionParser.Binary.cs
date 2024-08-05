// <copyright file="ProbeExpressionParser.Binary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Datadog.Trace.Internal.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Internal.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private Expression NotEqual(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, "!=");
    }

    private Expression Equal(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, "==");
    }

    private Expression LessThanOrEqual(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, "<=");
    }

    private Expression LessThan(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, "<");
    }

    private Expression GreaterThanOrEqual(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, ">=");
    }

    private Expression GreaterThan(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return BinaryOperation(reader, parameters, itParameter, ">");
    }

    private Expression BinaryOperation(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter, string operand)
    {
        Expression left = null, right = null;
        try
        {
            left = ParseTree(reader, parameters, itParameter);
            if (left.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            right = ParseTree(reader, parameters, itParameter);
            if (right.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (left.Type == typeof(string) && right.Type == typeof(string))
            {
                return StringLexicographicComparison(left, right, operand);
            }

            HandleDurationBinaryOperation(ref left, ref right);

            switch (operand)
            {
                case ">":
                    return Expression.GreaterThan(left, right);
                case ">=":
                    return Expression.GreaterThanOrEqual(left, right);
                case "<":
                    return Expression.LessThan(left, right);
                case "<=":
                    return Expression.LessThanOrEqual(left, right);
                case "==":
                    return Expression.Equal(left, right);
                case "!=":
                    return Expression.NotEqual(left, right);
                default:
                    throw new ArgumentException("Unknown operand" + operand, nameof(operand));
            }
        }
        catch (Exception e)
        {
            AddError($"{left?.ToString() ?? "N/A"} {operand} {right?.ToString() ?? "N/A"}", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private void HandleDurationBinaryOperation(ref Expression left, ref Expression right)
    {
        if (left is ParameterExpression { Name: Duration })
        {
            right = ConvertToDouble(right);
        }

        if (right is ParameterExpression { Name: Duration })
        {
            left = ConvertToDouble(left);
        }
    }

    private Expression StringLexicographicComparison(Expression left, Expression right, string operand)
    {
        switch (operand)
        {
            case "==":
                return Expression.Equal(left, right);
            case "!=":
                return Expression.NotEqual(left, right);
        }

        var compareOrdinal = Expression.Call(CompareOrdinalMethod(), new[] { left, right });
        var zeroConstant = Expression.Constant(0);
        return operand switch
        {
            ">" => Expression.GreaterThan(compareOrdinal, zeroConstant),
            ">=" => Expression.GreaterThanOrEqual(compareOrdinal, zeroConstant),
            "<" => Expression.LessThan(compareOrdinal, zeroConstant),
            "<=" => Expression.LessThanOrEqual(compareOrdinal, zeroConstant),
            _ => throw new ArgumentException("Unknown operand" + operand, nameof(operand))
        };

        System.Reflection.MethodInfo CompareOrdinalMethod() => ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), "CompareOrdinal", new[] { typeof(string), typeof(string) });
    }
}
