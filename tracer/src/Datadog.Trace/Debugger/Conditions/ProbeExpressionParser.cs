// <copyright file="ProbeExpressionParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Conditions
{
    internal class ProbeExpressionParser
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionParser));

        private static readonly Type UndefinedValueType = typeof(UndefinedValue);

        private static readonly ConcurrentDictionary<ReflectionMethodIdentifier, MethodInfo> Methods = new();

        private int _arrayStack;

        private delegate Expression Combiner(Expression left, Expression right);

        private Expression ParseHasAny(JsonTextReader reader, List<ParameterExpression> parameters)
        {
            var any = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2);
            return CollectionPredicate(reader, parameters, any);
        }

        private Expression ParseHasAll(JsonTextReader reader, List<ParameterExpression> parameters)
        {
            var all = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.All) && m.GetParameters().Length == 2);
            return CollectionPredicate(reader, parameters, all);
        }

        private Expression ParseFilter(JsonTextReader reader, List<ParameterExpression> parameters)
        {
            var where = typeof(Enumerable).GetMethods().Single(m => m.Name == nameof(Enumerable.Where) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.GenericTypeArguments.Length == 2);
            return CollectionPredicate(reader, parameters, where);
        }

        private Expression CollectionPredicate(JsonTextReader reader, List<ParameterExpression> parameters, MethodInfo predicateMethod)
        {
            var source = ParseTree(reader, parameters, null);
            if (source.Type == UndefinedValueType || (source is ConstantExpression constant && constant.Value.ToString() == "True"))
            {
                return TrueEqualExpression();
            }

            if (!IsSafeCollection(source))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
            }

            var itParameter = Expression.Parameter(source.Type.GetGenericArguments()[0]);
            var predicate = ParseTree(reader, new List<ParameterExpression> { Expression.Parameter(source.Type) }, itParameter);
            var lambda = Expression.Lambda<Func<string, bool>>(predicate, itParameter);
            var genericPredicateMethod = predicateMethod.MakeGenericMethod(source.Type.GetGenericArguments()[0]);
            var callExpression = Expression.Call(null, genericPredicateMethod, source, lambda);
            if (IsCollection(callExpression))
            {
                var toListMethod = GetMethodByReflection(typeof(Enumerable), nameof(Enumerable.ToList), null);
                var genericToListMethod = toListMethod.MakeGenericMethod(source.Type.GetGenericArguments()[0]);
                callExpression = Expression.Call(null, genericToListMethod, callExpression);
            }

            return callExpression;
        }

        private Expression ConditionalOperator(JsonTextReader reader, Combiner combiner, List<ParameterExpression> parameters)
        {
            Expression Combine(Expression leftOperand, Expression rightOperand) =>
                leftOperand == null ? rightOperand : combiner(leftOperand, rightOperand.Type != UndefinedValueType ? rightOperand : Expression.Constant(true));

            _arrayStack++;
            reader.Read();
            var right = ParseTree(reader, parameters, null);
            var left = Combine(null, right);

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    _arrayStack++;
                    continue;
                }
                else if (reader.TokenType == JsonToken.EndArray)
                {
                    _arrayStack--;
                    continue;
                }

                if (reader.TokenType is JsonToken.StartObject or JsonToken.EndObject)
                {
                    continue;
                }

                if (_arrayStack == 0)
                {
                    break;
                }

                right = ParseTree(reader, parameters, null, false);
                left = Combine(left, right);
            }

            return left;
        }

        private Expression ParseRoot(
            JsonTextReader reader,
            List<ParameterExpression> parameters)
        {
            var readerValue = reader.Value?.ToString();
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    switch (readerValue)
                    {
                        case "and":
                            {
                                return ConditionalOperator(reader, Expression.AndAlso, parameters);
                            }

                        case "or":
                            {
                                return ConditionalOperator(reader, Expression.OrElse, parameters);
                            }

                        default:
                            return ParseTree(reader, parameters, null, false);
                    }
            }

            return null;
        }

        private Expression ParseTree(
            JsonTextReader reader,
            List<ParameterExpression> parameters,
            ParameterExpression itParameter,
            bool shouldAdvanceReader = true)
        {
            while (ConditionalRead(reader, shouldAdvanceReader))
            {
                var readerValue = reader.Value?.ToString();

                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        switch (readerValue)
                        {
                            // operators
                            case "and":
                            case "&&":
                                {
                                    var right = ParseRoot(reader, parameters);
                                    return right;
                                }

                            case "or":
                            case "||":
                                {
                                    var right = ParseRoot(reader, parameters);
                                    return right;
                                }

                            case "eq":
                            case "==":
                                {
                                    var leftEq = ParseTree(reader, parameters, itParameter);
                                    if (leftEq.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightEq = ParseTree(reader, parameters, itParameter);
                                    return Expression.Equal(leftEq, rightEq);
                                }

                            case "!=":
                            case "neq":
                                {
                                    var leftEq = ParseTree(reader, parameters, itParameter);
                                    if (leftEq.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightEq = ParseTree(reader, parameters, itParameter);
                                    return Expression.NotEqual(leftEq, rightEq);
                                }

                            case ">":
                            case "gt":
                                {
                                    var leftGt = ParseTree(reader, parameters, itParameter);
                                    if (leftGt.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightGt = ParseTree(reader, parameters, itParameter);
                                    return Expression.GreaterThan(leftGt, rightGt);
                                }

                            case ">=":
                            case "ge":
                                {
                                    var leftEq = ParseTree(reader, parameters, itParameter);
                                    if (leftEq.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightEq = ParseTree(reader, parameters, itParameter);
                                    return Expression.GreaterThanOrEqual(leftEq, rightEq);
                                }

                            case "<":
                            case "lt":
                                {
                                    var leftEq = ParseTree(reader, parameters, itParameter);
                                    if (leftEq.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightEq = ParseTree(reader, parameters, itParameter);
                                    return Expression.LessThan(leftEq, rightEq);
                                }

                            case "<=":
                            case "le":
                                {
                                    var leftEq = ParseTree(reader, parameters, itParameter);
                                    if (leftEq.Type == UndefinedValueType)
                                    {
                                        return TrueEqualExpression();
                                    }

                                    var rightEq = ParseTree(reader, parameters, itParameter);
                                    return Expression.LessThanOrEqual(leftEq, rightEq);
                                }

                            case "not":
                            case "!":
                                {
                                    var ex = ParseTree(reader, parameters, itParameter);
                                    return Expression.Not(ex);
                                }

                            // string operations
                            case "isEmpty":
                                {
                                    var source = ParseTree(reader, parameters, itParameter);
                                    if (source.Type == UndefinedValueType)
                                    {
                                        return source;
                                    }

                                    if (source.Type == typeof(string))
                                    {
                                        var emptyMethod = GetMethodByReflection(typeof(string), nameof(string.IsNullOrEmpty), Type.EmptyTypes);
                                        return Expression.Call(source, emptyMethod);
                                    }

                                    // not sure about that, it seems from the RFC that isEmpty should support also collections
                                    var collectionCount = CollectionCountExpression(source);
                                    return Expression.Equal(collectionCount, Expression.Constant(0));
                                }

                            case "len":
                                {
                                    var lengthMethod = GetMethodByReflection(typeof(string), "get_Length", Type.EmptyTypes);
                                    var source = ParseTree(reader, parameters, itParameter);
                                    if (source.Type == UndefinedValueType)
                                    {
                                        return source;
                                    }

                                    return Expression.Call(source, lengthMethod);
                                }

                            case "substring":
                                {
                                    var substringMethod = GetMethodByReflection(typeof(string), nameof(string.Substring), new[] { typeof(int), typeof(int) });
                                    var source = ParseTree(reader, parameters, itParameter);
                                    if (source.Type == UndefinedValueType)
                                    {
                                        return source;
                                    }

                                    var startIndex = ParseTree(reader, parameters, itParameter);
                                    var endIndex = ParseTree(reader, parameters, itParameter);
                                    var lengthExpr = Expression.Subtract(endIndex, startIndex);
                                    return Expression.Call(source, substringMethod, startIndex, lengthExpr);
                                }

                            case "startWith":
                                {
                                    var startWithMethod = GetMethodByReflection(typeof(string), nameof(string.StartsWith), new[] { typeof(string) });
                                    return StringOperation(reader, parameters, itParameter, startWithMethod);
                                }

                            case "endWith":
                                {
                                    var endWithMethod = GetMethodByReflection(typeof(string), nameof(string.EndsWith), new[] { typeof(string) });
                                    return StringOperation(reader, parameters, itParameter, endWithMethod);
                                }

                            case "contains":
                                {
                                    var containsMethod = GetMethodByReflection(typeof(string), nameof(string.Contains), new[] { typeof(string) });
                                    return StringOperation(reader, parameters, itParameter, containsMethod);
                                }

                            case "matches":
                                {
                                    var matchesMethod = GetMethodByReflection(typeof(Regex), nameof(Regex.Matches), new[] { typeof(string), typeof(string) });
                                    return StringOperation(reader, parameters, itParameter, matchesMethod);
                                }

                            // collection operations
                            case "hasAny":
                                {
                                    return ParseHasAny(reader, parameters);
                                }

                            case "hasAll":
                                {
                                    return ParseHasAll(reader, parameters);
                                }

                            case "filter":
                                {
                                    return ParseFilter(reader, parameters);
                                }

                            case "count":
                                {
                                    var source = ParseTree(reader, parameters, itParameter);
                                    if (source.Type == UndefinedValueType)
                                    {
                                        return source;
                                    }

                                    return CollectionCountExpression(source);
                                }

                            // generic operations
                            case "isUndefined":
                                {
                                    var value = ParseTree(reader, parameters, itParameter);
                                    return Expression.TypeEqual(value, UndefinedValueType);
                                }
                        }

                        if (readerValue?.StartsWith("[") == true)
                        {
                            var index = ParseTree(reader, parameters, itParameter);
                            var source = ParseTree(reader, parameters, itParameter);
                            if (source.Type == UndefinedValueType)
                            {
                                return source;
                            }

                            if (source.Type.GetInterface("IList") == null &&
                                source.Type.GetInterface("IDictionary") == null)
                            {
                                throw new InvalidOperationException();
                            }

                            var itemMethod = GetMethodByReflection(source.Type, "get_Item", Type.EmptyTypes);
                            return Expression.Call(source, itemMethod, index);
                        }
                        else
                        {
                            reader.Read();
                        }

                        break;
                    case JsonToken.String:
                        if (readerValue?.StartsWith(".") == true)
                        {
                            // instance field
                            var fields = readerValue.Substring(1).Split('.');
                            return MemberPathExpression(fields, parameters[0]);
                        }

                        if (readerValue?.StartsWith("#") == true || readerValue?.StartsWith("^") == true)
                        {
                            // method local variable and method argument
                            return ArgOrLocalMemberPathExpression(parameters, readerValue);
                        }

                        if (readerValue == "@return")
                        {
                            return itParameter;
                        }

                        if (readerValue == "@duration")
                        {
                            return itParameter;
                        }

                        if (readerValue == "@it")
                        {
                            // current item in iterator
                            return itParameter ?? Expression.Parameter(UndefinedValueType, UndefinedValue.Instance.ToString());
                        }

                        if (readerValue == "@exceptions")
                        {
                            return itParameter;
                        }

                        return Expression.Constant(readerValue);

                    case JsonToken.Integer:
                        {
                            return Expression.Constant(Convert.ChangeType(readerValue, TypeCode.Int32));
                        }

                    case JsonToken.StartArray:
                        {
                            _arrayStack++;
                            break;
                        }

                    case JsonToken.EndArray:
                        {
                            _arrayStack--;
                            break;
                        }
                }
            }

            return null;
        }

        private BinaryExpression TrueEqualExpression()
        {
            return Expression.Equal(Expression.Constant(UndefinedValue.Instance), Expression.Constant(UndefinedValue.Instance));
        }

        private Expression StringOperation(JsonTextReader reader, List<ParameterExpression> parameters, ParameterExpression itParameter, MethodInfo method)
        {
            var source = ParseTree(reader, parameters, itParameter);
            if (source.Type == UndefinedValueType)
            {
                return source;
            }

            var parameter = ParseTree(reader, parameters, itParameter);
            return Expression.Call(source, method, parameter);
        }

        private bool ConditionalRead(JsonTextReader reader, bool shouldAdvanceReader)
        {
            return !shouldAdvanceReader || reader.Read();
        }

        private Expression ArgOrLocalMemberPathExpression(List<ParameterExpression> parameters, string readerValue)
        {
            var parts = readerValue.Substring(1).Split('.');
            var member = parameters.SingleOrDefault(p => p.Name == parts[0]);

            if (member == null)
            {
                return Expression.Constant(UndefinedValue.Instance);
            }

            if (parts.Length == 1)
            {
                return member;
            }

            return MemberPathExpression(parts.Skip(1).ToArray(), member);
        }

        private Expression MemberPathExpression(string[] parts, ParameterExpression expression)
        {
            try
            {
                var member = Expression.Field(expression, parts[0]);
                foreach (var currentField in parts.Skip(1))
                {
                    member = Expression.Field(member, currentField);
                }

                return member;
            }
            catch
            {
                return Expression.Constant(UndefinedValue.Instance);
            }
        }

        private MethodInfo GetMethodByReflection(Type type, string name, Type[] parametersTypes)
        {
            const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod |
                                              BindingFlags.NonPublic | BindingFlags.Public;

            var reflectionMethodIdentifier = new ReflectionMethodIdentifier(type, name, parametersTypes);
            return Methods.GetOrAdd(reflectionMethodIdentifier, GetMethodByReflectionInternal);

            MethodInfo GetMethodByReflectionInternal(ReflectionMethodIdentifier methodIdentifier)
            {
                var method = parametersTypes == null ? methodIdentifier.Type.GetMethod(methodIdentifier.MethodName, bindingFlags) : methodIdentifier.Type.GetMethod(methodIdentifier.MethodName, bindingFlags, null, methodIdentifier.Parameters, null);

                if (method == null)
                {
                    throw new NullReferenceException($"{methodIdentifier.Type.FullName}.{methodIdentifier.MethodName} method not found");
                }

                return method;
            }
        }

        private MethodCallExpression CollectionCountExpression(Expression source)
        {
            if (!IsTypeSupportCount(source))
            {
                throw new InvalidOperationException("Source must be an array or implement ICollection or IReadOnlyCollection");
            }

            var countOrLength = GetMethodByReflection(source.Type, source.Type.IsArray ? "get_Length" : "get_Count", Type.EmptyTypes);

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

        private bool IsTypeSupportIndex(Expression source)
        {
            return source.Type.GetInterface("IList") != null || source.Type.GetInterface("IReadOnlyList") != null;
        }

        private ExpressionBodyAndParameters ParseProbeExpression(string expressionJson, ScopeMember @this, ScopeMember[] argsOrLocals)
        {
            if (string.IsNullOrEmpty(expressionJson) || @this.Type == null || argsOrLocals == null)
            {
                throw new ArgumentException($"{nameof(ParseProbeExpression)} has been called with an invalid argument");
            }

            var scopeMembers = new List<ParameterExpression>();
            var assigns = new List<Expression>();
            var thisParameterExpression = Expression.Parameter(@this.GetType());
            var thisVariable = Expression.Variable(@this.Type, "this");
            assigns.Add(Expression.Assign(thisVariable, Expression.Convert(Expression.Field(thisParameterExpression, "Value"), @this.Type)));
            scopeMembers.Add(thisVariable);

            var argsOrLocalsParameterExpression = Expression.Parameter(argsOrLocals.GetType());

            for (var index = 0; index < argsOrLocals.Length; index++)
            {
                if (argsOrLocals[index].Type == null)
                {
                    break;
                }

                var argOrLocal = argsOrLocals[index];
                var variable = Expression.Variable(argOrLocal.Type, argOrLocal.Name);
                scopeMembers.Add(variable);

                assigns.Add(
                    Expression.Assign(
                        variable,
                        Expression.Convert(
                        Expression.Field(
                        Expression.ArrayIndex(
                            argsOrLocalsParameterExpression,
                            Expression.Constant(index)),
                        "Value"),
                        argOrLocal.Type)));
            }

            var reader = new JsonTextReader(new StringReader(expressionJson));
            SetReaderAtExpressionStart(reader);

            assigns.Add(ParseRoot(reader, scopeMembers));
            var body = (Expression)Expression.Block(scopeMembers, assigns);
            if (body.CanReduce)
            {
                body = body.ReduceAndCheck();
            }

            return new ExpressionBodyAndParameters(body, thisParameterExpression, argsOrLocalsParameterExpression);
        }

        private void SetReaderAtExpressionStart(JsonTextReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                if ((reader.Value?.ToString() == "json"))
                {
                    reader.Read();
                    reader.Read();
                    return;
                }
            }

            throw new InvalidOperationException("DSL part not found in the json file");
        }

        internal static CompiledExpression<T> ParseExpression<T>(string expressionJson, ScopeMember @this, ScopeMember[] argsOrLocals)
        {
            var parser = new ProbeExpressionParser();
            var parsedExpression = parser.ParseProbeExpression(expressionJson, @this, argsOrLocals);
            var expression = Expression.Lambda<Func<ScopeMember, ScopeMember[], T>>(parsedExpression.ExpressionBody, parsedExpression.ThisParameterExpression, parsedExpression.ArgsAndLocalsParameterExpression);
            return new CompiledExpression<T>(expression.Compile(), expression);
        }

        internal readonly record struct ExpressionBodyAndParameters
        {
            public ExpressionBodyAndParameters(Expression body, ParameterExpression thisParameterExpression, ParameterExpression argsOrLocalsParameterExpression)
            {
                ExpressionBody = body;
                ThisParameterExpression = thisParameterExpression;
                ArgsAndLocalsParameterExpression = argsOrLocalsParameterExpression;
            }

            internal Expression ExpressionBody { get; }

            internal ParameterExpression ThisParameterExpression { get; }

            internal ParameterExpression ArgsAndLocalsParameterExpression { get; }
        }

        internal readonly record struct ReflectionMethodIdentifier
        {
            public ReflectionMethodIdentifier(Type type, string methodName, Type[] parameters)
            {
                Type = type;
                MethodName = methodName;
                Parameters = parameters;
            }

            internal Type Type { get; }

            internal string MethodName { get; }

            internal Type[] Parameters { get; }
        }
    }
}
