// <copyright file="ProbeExpressionParser.General.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private const BindingFlags GetMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    // https://learn.microsoft.com/en-us/dotnet/standard/base-types/conversion-tables
    private static Type GetWiderNumericType(Type left, Type right)
    {
        if (left == right)
        {
            return left;
        }

        if (left == typeof(decimal) || right == typeof(decimal))
        {
            return typeof(decimal);
        }

        if (left == typeof(double) || right == typeof(double))
        {
            if (left != typeof(ulong) && right != typeof(long))
            {
                return typeof(double);
            }

            return typeof(decimal);
        }

        if (left == typeof(float) || right == typeof(float))
        {
            if ((left != typeof(ulong) && left != typeof(long) && left != typeof(int) && left != typeof(uint)) &&
                (right != typeof(ulong) && right != typeof(long) && right != typeof(int) && right != typeof(uint)))
            {
                return typeof(double);
            }

            return typeof(decimal);
        }

        if (left == typeof(ulong) || right == typeof(ulong))
        {
            return typeof(ulong);
        }

        if (left == typeof(long) || right == typeof(long))
        {
            return typeof(long);
        }

        if (left == typeof(uint) || right == typeof(uint))
        {
            return typeof(uint);
        }

        if (left == typeof(int) || right == typeof(int))
        {
            return typeof(int);
        }

        if (left == typeof(ushort) || right == typeof(ushort))
        {
            return typeof(ushort);
        }

        if (left == typeof(short) || right == typeof(short))
        {
            return typeof(short);
        }

        if (left == typeof(byte) || right == typeof(byte))
        {
            return typeof(byte);
        }

        return typeof(sbyte);
    }

    private static bool TryConvertToNumericType<TNumeric>(Expression finalExpr, [NotNullWhen(true)] out Expression result)
    {
        if (typeof(TNumeric).IsNumeric() && finalExpr.Type.IsNumeric())
        {
            result = Expression.Convert(finalExpr, typeof(TNumeric));
            return true;
        }

        if (typeof(IConvertible).IsAssignableFrom(finalExpr.Type))
        {
            result = CallConvertToNumericType<TNumeric>(finalExpr);
            return true;
        }

        result = null;
        return false;
    }

    private static MethodCallExpression CallConvertToNumericType<TNumeric>(Expression finalExpr)
    {
        var convertMethodName = typeof(TNumeric) switch
        {
            { } t when t == typeof(byte) => nameof(IConvertible.ToByte),
            { } t when t == typeof(sbyte) => nameof(IConvertible.ToSByte),
            { } t when t == typeof(short) => nameof(IConvertible.ToInt16),
            { } t when t == typeof(ushort) => nameof(IConvertible.ToUInt16),
            { } t when t == typeof(int) => nameof(IConvertible.ToInt32),
            { } t when t == typeof(uint) => nameof(IConvertible.ToUInt32),
            { } t when t == typeof(long) => nameof(IConvertible.ToInt64),
            { } t when t == typeof(ulong) => nameof(IConvertible.ToUInt64),
            { } t when t == typeof(float) => nameof(IConvertible.ToSingle),
            { } t when t == typeof(double) => nameof(IConvertible.ToDouble),
            { } t when t == typeof(decimal) => nameof(IConvertible.ToDecimal),
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

    private Expression IsInstanceOf(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var value = ParseTree(reader, parameters, itParameter);
        var instanceOf = (ConstantExpression)ParseTree(reader, parameters, itParameter);
        var typeName = instanceOf.Value?.ToString();
        if (string.IsNullOrEmpty(typeName))
        {
            AddError($"{value} is ?", "failed to parse type name");
            return Expression.Constant(false);
        }

        Type type = null;
        try
        {
            type = Type.GetType(typeName);
        }
        catch (Exception e)
        {
            AddError($"{value} is {typeName}", e.Message);
            return Expression.Constant(false);
        }

        if (type == null)
        {
            AddError($"{value} is {typeName}", $"'{typeName}' is unknown type");
            return Expression.Constant(false);
        }

        return Expression.TypeIs(value, type);
    }

    private Expression GetTypeName(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return Expression.Property(
            Expression.Call(
                ParseTree(reader, parameters, itParameter),
                ProbeExpressionParserHelper.GetMethodByReflection(typeof(object), "GetType", Type.EmptyTypes)),
            nameof(Type.FullName));
    }

    private Expression IsUndefined(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var value = ParseTree(reader, parameters, itParameter);
        return Expression.TypeEqual(value, ProbeExpressionParserHelper.UndefinedValueType);
    }

    private Expression IsDefined(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        return Expression.Not(IsUndefined(reader, parameters, itParameter));
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

            var constantValue = constant.Value?.ToString();

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
        var propertyOrFieldValue = propertyOrField.Value?.ToString();
        if (string.IsNullOrEmpty(propertyOrFieldValue))
        {
            AddError($"{expression}.{propertyOrFieldValue}", "Property or field name is empty.");
            return UndefinedValue();
        }

        try
        {
            if (Redaction.ShouldRedact(propertyOrFieldValue, propertyOrField.Type, out _))
            {
                AddError($"{expression}.{propertyOrFieldValue}", "The property or field is redacted.");
                return RedactedValue();
            }

            var memberInfo = expression.Type.GetMember(propertyOrFieldValue, GetMemberFlags).FirstOrDefault();

            if (memberInfo == null)
            {
                AddError($"{expression}.{propertyOrFieldValue}", $"The property or field does not exist in {expression.Type}");
                return UndefinedValue();
            }

            bool isStatic = (memberInfo is PropertyInfo propertyInfo && propertyInfo.GetGetMethod(true)?.IsStatic == true) ||
                            memberInfo is FieldInfo { IsStatic: true };

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
