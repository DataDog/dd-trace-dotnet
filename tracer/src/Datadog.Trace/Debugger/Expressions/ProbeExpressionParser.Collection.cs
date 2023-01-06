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
using Datadog.Trace.Vendors.Newtonsoft.Json;

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

            if (!IsSafeCollection(source))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
            }

            var itParameter = Expression.Parameter(source.Type.GetGenericArguments()[0]);
            var predicate = ParseTree(reader, new List<ParameterExpression> { Expression.Parameter(source.Type) }, itParameter);
            var lambda = Expression.Lambda<Func<string, bool>>(predicate, itParameter);
            var genericPredicateMethod = predicateMethod.MakeGenericMethod(source.Type.GetGenericArguments()[0]);
            callExpression = Expression.Call(null, genericPredicateMethod, source, lambda);
            if (IsCollection(callExpression))
            {
                var toListMethod = ProbeExpressionParserHelper.GetMethodByReflection(typeof(Enumerable), nameof(Enumerable.ToList), null);
                var genericToListMethod = toListMethod.MakeGenericMethod(source.Type.GetGenericArguments()[0]);
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

            if (!IsTypeSupportIndex(source, out var assignableFrom))
            {
                throw new InvalidOperationException("Source must implement IList or IDictionary");
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
            if (genericTypeArguments.Length > 0)
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

        var getItemCall = Expression.Call(source, getItemMethod, indexOrKey);
        if (getItemCall.Type == convertToType)
        {
            return getItemCall;
        }

        return Expression.Convert(getItemCall, convertToType);
    }

    private Expression Count(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter)
    {
        Expression source = null;
        try
        {
            source = ParseTree(reader, parameters, itParameter);
            if (source.Type == ProbeExpressionParserHelper.UndefinedValueType)
            {
                return ReturnDefaultValueExpression();
            }

            return CollectionCountExpression(source);
        }
        catch (Exception e)
        {
            AddError($"{source?.ToString() ?? "N/A"}.Count", e.Message);
            return ReturnDefaultValueExpression();
        }
    }

    private MethodCallExpression CollectionCountExpression(Expression source)
    {
        if (!IsTypeSupportCount(source))
        {
            throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
        }

        var countOrLength = ProbeExpressionParserHelper.GetMethodByReflection(source.Type, source.Type.IsArray ? "get_Length" : "get_Count", Type.EmptyTypes);

        return Expression.Call(source, countOrLength);
    }

    private bool IsSafeCollection(Expression source)
    {
        return source.Type.GetInterface("ICollection") != null ||
               source.Type.GetInterface("IReadOnlyCollection") != null ||
               source.Type.IsArray;
    }

    private bool IsCollection(Expression source)
    {
        return IsSafeCollection(source) || source.Type.GetInterface("IEnumerable") != null;
    }

    private bool IsTypeSupportCount(Expression source)
    {
        return source.Type.GetInterface("ICollection") != null ||
               source.Type.GetInterface("IReadOnlyCollection") != null ||
               source.Type.IsArray;
    }

    private bool IsTypeSupportIndex(Expression source, out Type assignableFrom)
    {
        if (source.Type.GetInterface("IList") != null)
        {
            assignableFrom = typeof(IList);
            return true;
        }

        if (source.Type.GetInterface("IReadOnlyList<>") != null)
        {
            assignableFrom = typeof(IReadOnlyList<>);
            return true;
        }

        if (source.Type.GetInterface("IDictionary") != null)
        {
            assignableFrom = typeof(IDictionary);
            return true;
        }

        assignableFrom = null;
        return false;
    }
}
