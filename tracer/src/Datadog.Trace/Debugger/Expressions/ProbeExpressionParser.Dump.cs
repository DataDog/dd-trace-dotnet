// <copyright file="ProbeExpressionParser.Dump.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Util;
using static Datadog.Trace.Debugger.Expressions.ProbeExpressionParserHelper;
using Enumerable = System.Linq.Enumerable;

namespace Datadog.Trace.Debugger.Expressions;

internal partial class ProbeExpressionParser<T>
{
    private Expression DumpExpression(Expression expression, List<ParameterExpression> scopeMembers)
    {
        if (Datadog.Trace.Debugger.Helpers.TypeExtensions.IsSimple(expression.Type) ||
            Redaction.AllowedTypesSafeToCallToString.Contains(expression.Type))
        {
            return Expression.Call(expression, GetMethodByReflection(typeof(object), nameof(object.ToString), Type.EmptyTypes));
        }

        var ifNull = Expression.Equal(expression, Expression.Constant(null));

        if (IsSafeException(expression.Type))
        {
            // for known Exception types we can assume it's safe to call .Message
            // whereas the others might have overriden it in a way that could cause side effects
            var stringConcat = GetMethodByReflection(typeof(string), nameof(string.Concat), new[] { typeof(object[]) });
            var typeNameExpression = Expression.Constant(expression.Type.FullName, typeof(string));
            var exceptionAsString = Expression.Call(
               stringConcat,
               Expression.NewArrayInit(
                   typeof(string),
                   typeNameExpression,
                   Expression.Constant(", "),
                   Expression.Property(expression, nameof(Exception.Message)),
                   Expression.Constant(", "),
                   Expression.Property(expression, nameof(Exception.StackTrace))));

            return Expression.Condition(ifNull, typeNameExpression, exceptionAsString);
        }

        var dumpExpression = IsSafeCollection(expression.Type) ?
                                 DumpCollectionExpression(expression, scopeMembers) :
                                 DumpFieldsExpression(expression, scopeMembers);

        return Expression.Condition(ifNull, Expression.Constant("null"), dumpExpression);
    }

    private Expression DumpCollectionExpression(Expression collection, List<ParameterExpression> scopeMembers)
    {
        var loopItemType = collection.Type.IsGenericType ? collection.Type.GetGenericArguments()[0] : typeof(object);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(loopItemType);
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(loopItemType);
        var stringBuilderAppend = GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.Append), new[] { typeof(string) });
        var getEnumeratorCall = Expression.Call(collection, GetMethodByReflection(enumerableType, nameof(IEnumerable.GetEnumerator), Type.EmptyTypes));

        var expressions = new List<Expression>();
        var loopItem = Expression.Variable(loopItemType, "loopItem");
        scopeMembers.Add(loopItem);

        var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
        scopeMembers.Add(enumeratorVar);
        expressions.Add(enumeratorVar);
        var moveNextCall = Expression.Call(enumeratorVar, GetMethodByReflection(typeof(IEnumerator), nameof(IEnumerator.MoveNext), Type.EmptyTypes));

        expressions.Add(Expression.Assign(enumeratorVar, getEnumeratorCall));

        var index = Expression.Variable(typeof(int), "index");
        scopeMembers.Add(index);
        expressions.Add(Expression.Assign(index, Expression.Constant(0)));

        var result = Expression.Variable(typeof(StringBuilder), "itemValues");
        scopeMembers.Add(result);
        expressions.Add(Expression.Assign(result, Expression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes))));

        expressions.Add(Expression.Call(result, stringBuilderAppend, Expression.Constant("[")));

        var dumpObjectCallExpression = Expression.Call(
            result,
            stringBuilderAppend,
            Expression.Call(
                Expression.Constant(this),
                GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(DumpObject), new[] { typeof(object), typeof(Type), typeof(string), typeof(int) }),
                loopItem,
                Expression.Constant(loopItem.Type),
                Expression.Constant(string.Empty),
                Expression.Constant(1) /* no nested collection */));

        var condition = Expression.AndAlso(
            Expression.Equal(moveNextCall, Expression.Constant(true)),
            Expression.LessThan(index, Expression.Constant(3)));

        var breakLabel = Expression.Label("loopBreak");
        var loopBodyExpression = Expression.IfThenElse(
            Expression.Equal(moveNextCall, Expression.Constant(true)),
            Expression.Block(
                new[] { loopItem },
                Expression.IfThenElse(
                    Expression.LessThan(index, Expression.Constant(3)),
                    Expression.Block(
                        Expression.IfThen(
                            Expression.GreaterThan(index, Expression.Constant(0)),
                            Expression.Call(result, stringBuilderAppend, Expression.Constant(", "))),
                        Expression.Assign(loopItem, Expression.Property(enumeratorVar, "Current")),
                        dumpObjectCallExpression,
                        Expression.PostIncrementAssign(index)),
                    Expression.Block(
                        Expression.Call(result, stringBuilderAppend, Expression.Constant(", ...")),
                        Expression.Break(breakLabel)))),
            Expression.Break(breakLabel));

        expressions.Add(Expression.Loop(loopBodyExpression, breakLabel));
        expressions.Add(Expression.Call(result, stringBuilderAppend, Expression.Constant("]")));
        expressions.Add(Expression.Call(result, GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.ToString), Type.EmptyTypes)));
        return Expression.Block(expressions);
    }

    private Expression DumpFieldsExpression(Expression expression, List<ParameterExpression> scopeMembers)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var getTypeMethod = GetMethodByReflection(typeof(object), nameof(object.GetType), Type.EmptyTypes);
        var getFieldsMethod = GetMethodByReflection(typeof(Type), nameof(Type.GetFields), [typeof(BindingFlags)]);
        var orderByMethod = GetMethodByReflection(typeof(System.Linq.Enumerable), nameof(System.Linq.Enumerable.OrderBy), [typeof(IEnumerable<>), typeof(Func<,>)], [typeof(FieldInfo), typeof(int)]);
        var toArray = GetMethodByReflection(typeof(System.Linq.Enumerable), nameof(System.Linq.Enumerable.ToArray), [typeof(IEnumerable<>)], [typeof(FieldInfo)]);

        ParameterExpression parameterExp = Expression.Parameter(typeof(FieldInfo), "fieldInfo");
        MemberExpression propertyExp = Expression.Property(parameterExp, "MetadataToken");
        Expression<Func<FieldInfo, int>> lambdaExp = Expression.Lambda<Func<FieldInfo, int>>(propertyExp, parameterExp);
        var fieldInfoArray = Expression.Call(Expression.Call(expression, getTypeMethod), getFieldsMethod, Expression.Constant(flags));
        var fieldInfoOrderedArray = Expression.Call(null, toArray, Expression.Call(null, orderByMethod, fieldInfoArray, lambdaExp));
        var stringBuilderAppend = GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.Append), [typeof(string)]);
        var expressions = new List<Expression>();

        var fields = Expression.Variable(typeof(FieldInfo[]), "fieldsArray");
        scopeMembers.Add(fields);
        expressions.Add(Expression.Assign(fields, fieldInfoOrderedArray));

        var result = Expression.Variable(typeof(StringBuilder), "fieldValues");
        scopeMembers.Add(result);
        expressions.Add(Expression.Assign(result, Expression.New(typeof(StringBuilder).GetConstructor(Type.EmptyTypes))));

        var index = Expression.Variable(typeof(int), "index");
        scopeMembers.Add(index);
        expressions.Add(Expression.Assign(index, Expression.Constant(0)));

        // Loop Content
        var fieldAtIndex = Expression.ArrayIndex(fields, index);
        var fieldTypeExpression = Expression.Property(fieldAtIndex, typeof(FieldInfo), nameof(FieldInfo.FieldType));
        var fieldNameExpression = Expression.Property(fieldAtIndex, typeof(FieldInfo), nameof(FieldInfo.Name));

        var fieldGetValueExpression =
           Expression.Call(
               fieldAtIndex,
               GetMethodByReflection(typeof(FieldInfo), nameof(FieldInfo.GetValue), new[] { typeof(object) }),
               Expression.Convert(expression, typeof(object)));

        var dumpObjectCallExpression = Expression.Call(
            result,
            stringBuilderAppend,
            Expression.Call(
                Expression.Constant(this),
                GetMethodByReflection(typeof(ProbeExpressionParser<T>), nameof(DumpObject), new[] { typeof(Expression), typeof(Type), typeof(string), typeof(int) }),
                fieldGetValueExpression,
                fieldTypeExpression,
                fieldNameExpression,
                Expression.Constant(0)));

        // End Loop Content

        var breakLabel = Expression.Label("loopBreak");
        var condition = Expression.AndAlso(
            Expression.LessThan(index, Expression.Property(fields, nameof(Array.Length))),
            Expression.LessThan(index, Expression.Constant(5)));

        var loopBodyExpression = Expression.IfThenElse(
           condition,
           Expression.Block(
               dumpObjectCallExpression,
               Expression.PostIncrementAssign(index),
               Expression.IfThen(
                   condition,
                   Expression.Call(result, stringBuilderAppend, Expression.Constant(", ")))),
           Expression.Block(
               Expression.IfThen(
                   Expression.LessThan(index, Expression.Property(fields, nameof(Array.Length))),
                   Expression.Call(result, stringBuilderAppend, Expression.Constant(", ..."))),
               Expression.Break(breakLabel)));

        expressions.Add(Expression.Loop(loopBodyExpression, breakLabel));
        expressions.Add(Expression.Call(result, GetMethodByReflection(typeof(StringBuilder), nameof(StringBuilder.ToString), Type.EmptyTypes)));

        return Expression.Block(expressions);
    }

    private string DumpObject(object value, Type type, string name, int depth = 0)
    {
        // only one level depth of collection
        if (depth == 0 && IsTypeSupportIndex(type, out var assignableFrom))
        {
            return DumpCollection(value, assignableFrom);
        }

        if (!string.IsNullOrEmpty(name))
        {
            name += "=";
        }

        if (IsSafeException(type))
        {
            return value is not Exception ex
                       ? $"{name}{type?.FullName}"
                       : $"{name}{ex.GetType().FullName}, {ex.Message}, {ex.StackTrace}";
        }

        return Redaction.IsSafeToCallToString(type) ?
                   $"{name}{value?.ToString() ?? type?.FullName}" :
                   $"{name}{type?.FullName}";
    }

    private string DumpCollection(object value, Type assignableFrom)
    {
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        try
        {
            if (value == null)
            {
                sb.Append("null");
                return sb.ToString();
            }

            int count = 0;
            if (assignableFrom == typeof(IList))
            {
                sb.Append("[");
                foreach (var item in (value as IList))
                {
                    sb.Append($"{DumpObject(item, item.GetType(), null, 1)}");
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append("]");
                return sb.ToString();
            }
            else if (assignableFrom == typeof(IReadOnlyList<>))
            {
                sb.Append("[");
                foreach (var item in (value as IEnumerable))
                {
                    sb.Append($"{DumpObject(item, item.GetType(), null, 1)}");
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append("]");
                return sb.ToString();
            }
            else if (assignableFrom == typeof(IDictionary))
            {
                sb.Append("{");
                foreach (DictionaryEntry entry in (value as IDictionary))
                {
                    sb.Append($"[{DumpObject(entry.Key, entry.Key?.GetType(), null, 1)}, {DumpObject(entry.Value, entry.Value?.GetType(), null, 1)}]");
                    if (++count == 3)
                    {
                        sb.Append(", ...");
                        break;
                    }

                    sb.Append(", ");
                }

                sb.Append("}");
                return sb.ToString();
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }
}
