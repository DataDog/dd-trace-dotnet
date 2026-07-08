// <copyright file="ProbeExpressionParser.General.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private const BindingFlags InstanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private const BindingFlags StaticMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags AllMemberFlags = InstanceMemberFlags | StaticMemberFlags;

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

    private static Type CloseOpenGenericType(Type type)
    {
        return CloseOpenGenericType(type, new List<Type>());
    }

    private static Type CloseOpenGenericType(Type type, List<Type> visitedGenericParameters)
    {
        if (!type.ContainsGenericParameters)
        {
            return type;
        }

        if (type.IsGenericParameter)
        {
            return CloseGenericParameter(type, visitedGenericParameters);
        }

        if (type.HasElementType)
        {
            var elementType = CloseOpenGenericType(type.GetElementType(), visitedGenericParameters);
            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                return rank == 1 ? elementType.MakeArrayType() : elementType.MakeArrayType(rank);
            }

            if (type.IsByRef)
            {
                return elementType.MakeByRefType();
            }

            if (type.IsPointer)
            {
                return elementType.MakePointerType();
            }
        }

        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            var concreteTypes = new Type[genericArguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
            {
                concreteTypes[i] = CloseOpenGenericType(genericArguments[i], visitedGenericParameters);
            }

            try
            {
                return type.GetGenericTypeDefinition().MakeGenericType(concreteTypes);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains generic parameters that cannot be safely closed.", ex);
            }
        }

        throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains generic parameters that cannot be safely closed.");
    }

    private static Type CloseGenericParameter(Type type, List<Type> visitedGenericParameters)
    {
        if (visitedGenericParameters.Contains(type))
        {
            throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains recursive generic parameter constraints.");
        }

        visitedGenericParameters.Add(type);
        try
        {
            var attributes = type.GenericParameterAttributes;
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains a generic value type parameter.");
            }

            var constraints = type.GetGenericParameterConstraints();
            if (constraints.Length == 1)
            {
                var constraint = CloseOpenGenericType(constraints[0], visitedGenericParameters);
                if (constraint.IsValueType)
                {
                    throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains a generic value type parameter.");
                }

                return constraint;
            }

            if (constraints.Length > 1)
            {
                throw new InvalidOperationException($"Could not evaluate expression for type {FormatTypeName(type)} because it contains generic parameters with multiple constraints.");
            }

            return typeof(object);
        }
        finally
        {
            visitedGenericParameters.RemoveAt(visitedGenericParameters.Count - 1);
        }
    }

    private static string FormatTypeName(Type type)
    {
        return type.FullName ?? type.Name;
    }

    private static bool TryResolveSafeMemberExpression(Expression source, string memberName, [NotNullWhen(true)] out Expression memberExpression, [NotNullWhen(false)] out string reason)
    {
        memberExpression = null;
        reason = null;

        var sourceType = source?.Type;
        if (sourceType == null)
        {
            reason = "The source expression type is null.";
            return false;
        }

        if (sourceType.ContainsGenericParameters)
        {
            reason = $"The property or field cannot be safely read because {sourceType} contains generic parameters.";
            return false;
        }

        var currentType = sourceType;
        while (currentType != null && currentType != typeof(object))
        {
            try
            {
                var field = currentType.GetField(memberName, AllMemberFlags);
                if (field != null)
                {
                    return TryCreateFieldExpression(source, field, out memberExpression, out reason);
                }
            }
            catch (Exception)
            {
                reason = "The property or field cannot be safely resolved.";
                return false;
            }

            try
            {
                var property = currentType.GetProperty(memberName, AllMemberFlags);
                if (property != null)
                {
                    return TryCreateAutoPropertyBackingFieldExpression(source, property, out memberExpression, out reason);
                }
            }
            catch (Exception)
            {
                reason = "The property or field cannot be safely resolved.";
                return false;
            }

            currentType = currentType.BaseType;
        }

        reason = $"The property or field does not exist in {sourceType}";
        return false;
    }

    private static bool TryCreateAutoPropertyBackingFieldExpression(Expression source, PropertyInfo property, [NotNullWhen(true)] out Expression memberExpression, out string reason)
    {
        memberExpression = null;
        reason = null;

        MethodInfo getMethod;
        Type declaringType;
        try
        {
            getMethod = property.GetGetMethod(true);
            declaringType = property.DeclaringType;
        }
        catch (Exception)
        {
            reason = "The property cannot be safely read without invoking its getter.";
            return false;
        }

        if (getMethod == null || declaringType == null)
        {
            reason = "The property cannot be safely read without invoking its getter.";
            return false;
        }

        if (property.PropertyType.ContainsGenericParameters ||
            declaringType.ContainsGenericParameters ||
            property.ReflectedType?.ContainsGenericParameters == true ||
            property.PropertyType.IsGenericTypeDefinition)
        {
            reason = "The property cannot be safely read without invoking its getter.";
            return false;
        }

        var backingFieldName = "<" + property.Name + ">k__BackingField";
        FieldInfo backingField;
        try
        {
            backingField = declaringType.GetField(backingFieldName, AllMemberFlags);
        }
        catch (Exception)
        {
            reason = "The property cannot be safely read without invoking its getter.";
            return false;
        }

        if (backingField == null ||
            backingField.DeclaringType != declaringType ||
            backingField.FieldType != property.PropertyType ||
            backingField.IsStatic != getMethod.IsStatic ||
            !backingField.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            reason = "The property cannot be safely read without invoking its getter.";
            return false;
        }

        return TryCreateFieldExpression(source, backingField, out memberExpression, out reason);
    }

    private static bool TryCreateFieldExpression(Expression source, FieldInfo field, [NotNullWhen(true)] out Expression memberExpression, out string reason)
    {
        memberExpression = null;
        reason = null;

        if (field.FieldType.ContainsGenericParameters ||
            field.DeclaringType?.ContainsGenericParameters == true ||
            field.ReflectedType?.ContainsGenericParameters == true ||
            field.FieldType.IsGenericTypeDefinition)
        {
            reason = "The field cannot be safely read.";
            return false;
        }

        if (field.IsStatic)
        {
            if (field.IsLiteral)
            {
                memberExpression = Expression.Constant(StaticMemberSafety.GetRawConstantValue(field), field.FieldType);
                return true;
            }

            if (!StaticMemberSafety.CanReadStaticMember(field))
            {
                reason = "Static member access is skipped because it could trigger the declaring type initializer.";
                return false;
            }

            memberExpression = Expression.Field(null, field);
            return true;
        }

        var declaringType = field.DeclaringType;
        if (declaringType == null || !declaringType.IsAssignableFrom(source.Type))
        {
            reason = "The field cannot be safely read.";
            return false;
        }

        memberExpression = Expression.Field(source, field);
        return true;
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

        var instanceOfMethod = ProbeExpressionParserHelper.GetMethodByReflection(
            typeof(InstanceOfHelper),
            nameof(InstanceOfHelper.IsInstanceOf),
            [value.Type, typeof(string)],
            [value.Type]);
        var isInstanceOfExpression = Expression.Call(
            null,
            instanceOfMethod,
            value,
            Expression.Constant(typeName));

        return RedactDictionaryOperation(value, isInstanceOfExpression);
    }

    private Expression GetTypeName(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var source = ParseTree(reader, parameters, itParameter);
        return RedactDictionaryOperation(source, Expression.Property(
            Expression.Call(
                source,
                ProbeExpressionParserHelper.GetMethodByReflection(typeof(object), "GetType", Type.EmptyTypes)),
            nameof(Type.FullName)));
    }

    private Expression IsUndefined(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var value = ParseTree(reader, parameters, itParameter);
        return RedactDictionaryOperation(value, Expression.TypeEqual(value, ProbeExpressionParserHelper.UndefinedValueType));
    }

    private Expression IsDefined(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        var value = ParseTree(reader, parameters, itParameter);
        return RedactDictionaryOperation(value, Expression.Not(Expression.TypeEqual(value, ProbeExpressionParserHelper.UndefinedValueType)));
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

            if (Redaction.Instance.ShouldRedact(constantValue, constant.Type, out _))
            {
                AddError(reader.Value?.ToString() ?? "N/A", "The property or field is redacted.");
                return RedactedValue();
            }

            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter.Name == constantValue)
                {
                    return parameter;
                }
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
            if (Redaction.Instance.ShouldRedact(propertyOrFieldValue, propertyOrField.Type, out _))
            {
                AddError($"{expression}.{propertyOrFieldValue}", "The property or field is redacted.");
                return RedactedValue();
            }

            if (propertyOrFieldValue == nameof(KeyValuePair<int, int>.Value) &&
                TryRedactDictionaryValueMember(expression, out var redactedValue))
            {
                return redactedValue;
            }

            if (TryGetRedactedDictionaryValue(expression, out var redactedDictionaryValue))
            {
                return RedactedDictionaryValueMember(redactedDictionaryValue, propertyOrFieldValue);
            }

            if (!TryResolveSafeMemberExpression(expression, propertyOrFieldValue, out var memberExpression, out var reason))
            {
                AddError($"{expression}.{propertyOrFieldValue}", reason);
                return UndefinedValue();
            }

            return memberExpression;
        }
        catch (Exception e)
        {
            AddError($"{expression}.{propertyOrFieldValue}", e.Message);
            return UndefinedValue();
        }
    }

    private ConstantExpression UndefinedValue()
    {
        return Expression.Constant(Expressions.UndefinedValue.Instance);
    }

    private ConstantExpression RedactedValue()
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
        else if (typeof(T) == typeof(object))
        {
            // capture expression
            return Expression.Return(ReturnTarget, Expression.Constant(Expressions.UndefinedValue.Instance, typeof(object)), typeof(T));
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
