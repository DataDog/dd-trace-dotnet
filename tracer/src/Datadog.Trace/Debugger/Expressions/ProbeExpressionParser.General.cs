// <copyright file="ProbeExpressionParser.General.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private static Expression ConvertToDouble(Expression expr)
    {
        if (expr.Type == typeof(double))
        {
            return expr;
        }

        if (expr.Type.IsNumeric())
        {
            return Expression.Convert(expr, typeof(double));
        }

        return expr;
    }

    private static bool IsIntegralNumericType(Type type)
    {
        if (type == typeof(byte)
         || type == typeof(sbyte)
         || type == typeof(short)
         || type == typeof(ushort)
         || type == typeof(int)
         || type == typeof(uint)
         || type == typeof(nint)
         || type == typeof(nuint))
        {
            return true;
        }

        return false;
    }

    private static MethodCallExpression CallConvertToNumericType<TNumeric>(Expression finalExpr)
    {
        var convertMethodName = typeof(TNumeric) switch
        {
            { } @int when @int == typeof(byte) => nameof(IConvertible.ToByte),
            { } @int when @int == typeof(sbyte) => nameof(IConvertible.ToSByte),
            { } @int when @int == typeof(short) => nameof(IConvertible.ToInt16),
            { } @int when @int == typeof(ushort) => nameof(IConvertible.ToUInt16),
            { } @int when @int == typeof(int) => nameof(IConvertible.ToInt32),
            { } @int when @int == typeof(uint) => nameof(IConvertible.ToUInt32),
            { } @int when @int == typeof(long) => nameof(IConvertible.ToInt64),
            { } @int when @int == typeof(ulong) => nameof(IConvertible.ToUInt64),
            { } @int when @int == typeof(float) => nameof(IConvertible.ToSingle),
            { } @int when @int == typeof(double) => nameof(IConvertible.ToDouble),
            { } @int when @int == typeof(decimal) => nameof(IConvertible.ToDecimal),
            _ => null
        };

        return convertMethodName == null
                   ? null
                   : Expression.Call(
                       Expression.Convert(finalExpr, typeof(IConvertible)),
                       ProbeExpressionParserHelper.GetMethodByReflection(
                           typeof(IConvertible), convertMethodName, new[] { typeof(IFormatProvider) }),
                       Expression.Constant(NumberFormatInfo.CurrentInfo));
    }

    private Expression IsUndefined(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var value = ParseTree(reader, parameters, itParameter);
        return Expression.TypeEqual(value, ProbeExpressionParserHelper.UndefinedValueType);
    }

    private Expression GetMember(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var referralMember = ParseTree(reader, parameters, itParameter);
        var refMember = (ConstantExpression)ParseTree(reader, parameters, itParameter);

        return MemberPathExpression(referralMember, refMember);
    }

    private Expression GetReference(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        try
        {
            // method local variable and method argument
            var refMember = ParseTree(reader, parameters, itParameter);
            if (refMember is not ConstantExpression constant)
            {
                return refMember;
            }

            var constantValue = constant.Value.ToString();

            if (Redaction.ShouldRedact(constantValue, constant.Type, out _))
            {
                AddError(reader.Value?.ToString() ?? "N/A", "The property or field is redacted.");
                return RedactedValue();
            }

            var argOrLocal = parameters.FirstOrDefault(p => p.Name == constantValue);
            if (argOrLocal != null)
            {
                return argOrLocal;
            }

            // will return an instance field\property or an UndefinedValue
            return MemberPathExpression(GetParameterExpression(parameters, ScopeMemberKind.This), constant);
        }
        catch (Exception e)
        {
            AddError(reader.Value?.ToString() ?? "N/A", e.Message);
            return UndefinedValue();
        }
    }

    private Expression MemberPathExpression(Expression expression, ConstantExpression propertyOrField)
    {
        var propertyOrFieldValue = propertyOrField.Value.ToString();

        try
        {
            if (Redaction.ShouldRedact(propertyOrFieldValue, propertyOrField.Type, out _))
            {
                AddError($"{expression}.{propertyOrFieldValue}", "The property or field is redacted.");
                return RedactedValue();
            }

            var memberInfo = expression.Type.GetMember(
                propertyOrFieldValue,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .FirstOrDefault();

            if (memberInfo == null)
            {
                AddError($"{expression}.{propertyOrFieldValue}", "The property or field does not exist.");
                return UndefinedValue();
            }

            bool isStatic = (memberInfo is PropertyInfo && ((PropertyInfo)memberInfo).GetGetMethod(true)?.IsStatic == true) ||
                            (memberInfo is FieldInfo && ((FieldInfo)memberInfo).IsStatic);

            if (isStatic)
            {
                return memberInfo.MemberType switch
                {
                    MemberTypes.Field => Expression.Field(null, (FieldInfo)memberInfo),
                    MemberTypes.Property => Expression.Property(null, (PropertyInfo)memberInfo),
                    _ => throw new InvalidOperationException("Unsupported member type for static member access.")
                };
            }
            else
            {
                return Expression.PropertyOrField(expression, propertyOrFieldValue);
            }
        }
        catch (Exception e)
        {
            AddError($"{expression}.{propertyOrFieldValue}", e.Message);
            return UndefinedValue();
        }
    }

    private Expression UndefinedValue()
    {
        return Expression.Constant(Expressions.UndefinedValue.Instance);
    }

    private Expression RedactedValue()
    {
        return Expression.Constant("{REDACTED}");
    }

    private GotoExpression ReturnDefaultValueExpression()
    {
        if (typeof(T) == typeof(bool))
        {
            // condition
            return Expression.Return(ReturnTarget, Expression.Constant(true), typeof(T));
        }
        else if (typeof(T) == typeof(string))
        {
            // template
            return Expression.Return(ReturnTarget, Expression.Constant(nameof(Expressions.UndefinedValue)), typeof(T));
        }
        else if (typeof(T) == typeof(double))
        {
            // metric
            return Expression.Return(ReturnTarget, Expression.Constant(0), typeof(T));
        }
        else
        {
            throw new ArgumentException($"Unsupported type: {typeof(T).FullName}");
        }
    }

    private ParameterExpression GetParameterExpression(List<ParameterExpression> parameters, ScopeMemberKind kind)
    {
        switch (kind)
        {
            case ScopeMemberKind.This:
                return parameters[0];
            case ScopeMemberKind.Return:
                return parameters[1];
            case ScopeMemberKind.Duration:
                return parameters[2];
            case ScopeMemberKind.Exception:
                return parameters[3];
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private bool IsSafeException(Type type)
    {
        return typeof(Exception).IsAssignableFrom(type) && IsMicrosoftType(type);
    }

    private bool IsMicrosoftType(Type type)
    {
        var @namespace = type.Namespace;
        return @namespace != null &&
               (@namespace is "System" or "Microsoft" ||
                @namespace.StartsWith("System.") ||
                @namespace.StartsWith("Microsoft."));
    }
}
