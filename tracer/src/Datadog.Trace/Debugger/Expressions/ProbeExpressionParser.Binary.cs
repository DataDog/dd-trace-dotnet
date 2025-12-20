// <copyright file="ProbeExpressionParser.Binary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed partial class ProbeExpressionParser<T>
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

            NumericImplicitConversion(ref left, ref right);

            return operand switch
            {
                ">" => Expression.GreaterThan(left, right),
                ">=" => Expression.GreaterThanOrEqual(left, right),
                "<" => Expression.LessThan(left, right),
                "<=" => Expression.LessThanOrEqual(left, right),
                "==" or "!=" => EqualExpression(),
                _ => throw new ArgumentException("Unknown operand" + operand, nameof(operand))
            };
        }
        catch (Exception e)
        {
            AddError($"{left?.ToString() ?? "N/A"} {operand} {right?.ToString() ?? "N/A"}", e.Message);
            return ReturnDefaultValueExpression();
        }

        Expression EqualExpression()
        {
            // Check if comparing non-nullable value type to null constant
            if ((right is ConstantExpression { Value: null } && left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) == null) ||
                (left is ConstantExpression { Value: null } && right.Type.IsValueType && Nullable.GetUnderlyingType(right.Type) == null))
            {
                // Non-nullable value types can never be null
                return operand == "==" ? Expression.Constant(false) : Expression.Constant(true);
            }

            // For ANY reference type compared to null, use object reference equality
            // This avoids InvalidCastException when runtime type differs from compile-time type
            // (e.g., probe compiled for LimitedInputStream but invoked with MemoryStream)
            if (right is ConstantExpression { Value: null } && !left.Type.IsValueType)
            {
                var leftAsObject = left.Type == typeof(object) ? left : Expression.Convert(left, typeof(object));
                return operand == "=="
                    ? Expression.ReferenceEqual(leftAsObject, Expression.Constant(null, typeof(object)))
                    : Expression.Not(Expression.ReferenceEqual(leftAsObject, Expression.Constant(null, typeof(object))));
            }

            if (left is ConstantExpression { Value: null } && !right.Type.IsValueType)
            {
                var rightAsObject = right.Type == typeof(object) ? right : Expression.Convert(right, typeof(object));
                return operand == "=="
                    ? Expression.ReferenceEqual(Expression.Constant(null, typeof(object)), rightAsObject)
                    : Expression.Not(Expression.ReferenceEqual(Expression.Constant(null, typeof(object)), rightAsObject));
            }

            return operand == "==" ? Expression.Equal(left, right) : Expression.NotEqual(left, right);
        }
    }

    private void NumericImplicitConversion(ref Expression left, ref Expression right)
    {
        if (left.Type == right.Type)
        {
            return;
        }

        if (left.Type.IsNumeric() && right.Type.IsNumeric())
        {
            var type = GetWiderNumericType(left.Type, right.Type);
            left = Expression.Convert(left, type);
            right = Expression.Convert(right, type);
        }
    }

    private void HandleDurationBinaryOperation(ref Expression left, ref Expression right)
    {
        // Duration is double
        if (left is ParameterExpression { Name: Duration } && right.Type.IsNumeric() && right.Type != typeof(double) && right.Type != typeof(decimal))
        {
            right = Expression.Convert(right, typeof(double));
        }

        if (right is ParameterExpression { Name: Duration } && left.Type.IsNumeric() && left.Type != typeof(double) && left.Type != typeof(decimal))
        {
            left = Expression.Convert(right, typeof(double));
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
