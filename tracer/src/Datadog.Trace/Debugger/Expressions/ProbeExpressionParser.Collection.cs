// <copyright file="ProbeExpressionParser.Collection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Type = System.Type;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private Expression HasAny(JsonTextReader reader, List<ParameterExpression> parameters)
    {
        var any = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2);
        return Predicate(reader, parameters, any);
    }

    private Expression HasAll(JsonTextReader reader, List<ParameterExpression> parameters)
    {
        var all = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.All) && m.GetParameters().Length == 2);
        return Predicate(reader, parameters, all);
    }

    private Expression Filter(JsonTextReader reader, List<ParameterExpression> parameters)
    {
        var where = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.Where) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments.Length == 2);
        return Predicate(reader, parameters, where);
    }

    private Expression Predicate(JsonTextReader reader, List<ParameterExpression> parameters, MethodInfo predicateMethod)
    {
        Expression source = null, callExpression = null;
        try
        {
            source = ParseTree(reader, parameters, null);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                ReturnDefaultValueExpression();
            }

            if (!IsSafeCollection(source.Type))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
            }

            Type itParameterType = null;
            if (source.Type.IsArray)
            {
                itParameterType = source.Type.GetElementType();
            }
            else
            {
                if (source.Type.GetGenericArguments().Length > 0)
                {
                    itParameterType = source.Type.GetGenericArguments()[0];
                }
            }

            if (predicateMethod == null)
            {
                throw new InvalidOperationException("Fail to determined the iterator parameter type");
            }

            ParameterExpression itParameter = Expression.Parameter(itParameterType);
            var predicate = ParseTree(reader, new List<ParameterExpression> { Expression.Parameter(source.Type) }, itParameter);
            var lambda = Expression.Lambda(predicate, itParameter);
            var genericPredicateMethod = predicateMethod.MakeGenericMethod(itParameterType);
            callExpression = Expression.Call(null, genericPredicateMethod, source, lambda);
            if (IsIEnumerable(callExpression.Type))
            {
                var toListMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(Enumerable), nameof(Enumerable.ToList), null);
                var genericToListMethod = toListMethod.MakeGenericMethod(itParameterType);
                callExpression = Expression.Call(null, genericToListMethod, callExpression);
            }

            return callExpression;
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}[{callExpression?.ToString() ?? "N/A"}]", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private Expression GetItemAtIndex(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        Expression indexOrKey = null, source = null;
        try
        {
            source = ParseTree(reader, parameters, itParameter);
            indexOrKey = ParseTree(reader, parameters, itParameter);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            if (!IsTypeSupportIndex(source.Type, out var assignableFrom))
            {
                throw new InvalidOperationException("Source must implement IList or IDictionary");
            }

            if (indexOrKey.Type == typeof(string) &&
                indexOrKey is ConstantExpression expr &&
                Redaction.ShouldRedact(expr.Value?.ToString(), expr.Type, out _))
            {
                AddError($"{source?.ToString() ?? "N/A"}[{indexOrKey?.ToString() ?? "N/A"}]", "The property or field is redacted.");
                return RedactedValue();
            }

            return CallGetItem(source, assignableFrom, indexOrKey);
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}[{indexOrKey?.ToString() ?? "N/A"}]", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private Expression CallGetItem(Expression source, Type assignableFrom, Expression indexOrKey)
    {
        MethodInfo getItemMethod = null;
        Type convertToType = typeof(object);
        var genericTypeArguments = source.Type.GenericTypeArguments;
        if (assignableFrom == typeof(IList) || assignableFrom == typeof(IReadOnlyList<>))
        {
            if (source.Type.IsArray)
            {
                convertToType = source.Type.GetElementType();
            }
            else if (genericTypeArguments.Length > 0)
            {
                convertToType = genericTypeArguments[0];
            }

            getItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(assignableFrom, "get_Item", new[] { typeof(int) });
        }
        else if (assignableFrom == typeof(IDictionary))
        {
            Type keyType;
            switch (genericTypeArguments.Length)
            {
                case 2:
                    keyType = genericTypeArguments[0];
                    convertToType = genericTypeArguments[1];
                    break;
                case 1:
                    keyType = genericTypeArguments[0];
                    break;
                default:
                    keyType = typeof(object);
                    break;
            }

            getItemMethod = ProbeExpressionParserHelper.GetMethodByReflection(assignableFrom, "get_Item", new[] { keyType });
        }

        if (getItemMethod == null)
        {
            throw new InvalidOperationException("Unsupported collection");
        }

        var getItemCall = Expression.Call(source, getItemMethod, indexOrKey);
        if (getItemCall.Type == convertToType)
        {
            return getItemCall;
        }

        return Expression.Convert(getItemCall, convertToType);
    }

    private Expression Length(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        Expression source = null;
        try
        {
            source = ParseTree(reader, parameters, itParameter);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            return CollectionAndStringLengthExpression(source);
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}.Count", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private MethodCallExpression CollectionAndStringLengthExpression(Expression source)
    {
        if (source?.Type == typeof(string))
        {
            var lengthMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(string), "get_Length", Type.EmptyTypes);
            return Expression.Call(source, lengthMethod);
        }

        if (!IsSafeCollection(source?.Type))
        {
            throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
        }

        var countOrLength = ProbeExpressionParserHelper.GetMethodByReflection(source.Type, source.Type.IsArray ? "get_Length" : "get_Count", Type.EmptyTypes);

        return Expression.Call(source, countOrLength);
    }

    private bool IsSafeCollection(Type type)
    {
        if (type == null)
        {
            return false;
        }

        return type.IsArray || (IsMicrosoftType(type) && IsCollection(type));
    }

    private bool IsIEnumerable(Type type)
    {
        return IsSafeCollection(type) || type.GetInterface(nameof(IEnumerable)) != null;
    }

    private bool IsCollection(Type type)
    {
        return type.GetInterfaces()
                   .Any(i =>
                            i.IsGenericType
                         && (i.GetGenericTypeDefinition() == typeof(ICollection)
                          || i.GetGenericTypeDefinition() == typeof(ICollection<>)
                          || i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));
    }

    private bool IsTypeSupportIndex(Type type, out Type assignableFrom)
    {
        if (type.IsArray)
        {
            assignableFrom = typeof(IList);
            return true;
        }

        if (!IsMicrosoftType(type))
        {
            assignableFrom = null;
            return false;
        }

        // Do not use IsInstanceOfType
        if (type == typeof(IList) || type.GetInterface(nameof(IList)) != null)
        {
            assignableFrom = typeof(IList);
            return true;
        }

        if (type == typeof(IDictionary) || type.GetInterface(nameof(IDictionary)) != null)
        {
            assignableFrom = typeof(IDictionary);
            return true;
        }

        if (type == typeof(IReadOnlyList<>) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) ||
            type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            assignableFrom = typeof(IReadOnlyList<>);
            return true;
        }

        assignableFrom = null;
        return false;
    }
}
