// <copyright file="ProbeExpressionParserHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Datadog.Trace.Debugger.Expressions;

internal static class ProbeExpressionParserHelper
{
    private static readonly ConcurrentDictionary<ReflectionMethodIdentifier, MethodInfo> Methods = new();

    internal static readonly Type UndefinedValueType = typeof(UndefinedValue);

    internal static MethodInfo GetMethodByReflection(Type type, string name, Type[] parametersTypes, Type[] genericArguments = null)
    {
        const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod |
                                          BindingFlags.NonPublic | BindingFlags.Public;

        var reflectionMethodIdentifier = new ReflectionMethodIdentifier(type, name, parametersTypes, genericArguments);
        return Methods.GetOrAdd(reflectionMethodIdentifier, GetMethodByReflectionInternal);

        MethodInfo GetMethodByReflectionInternal(ReflectionMethodIdentifier methodIdentifier)
        {
            MethodInfo method = null;
            if (genericArguments != null)
            {
                method = methodIdentifier.Type.GetMethods().
                                          Where(m =>
                                                    m.Name == methodIdentifier.MethodName &&
                                                    m.GetParameters().Length == methodIdentifier.Parameters.Length &&
                                                    m.ContainsGenericParameters &&
                                                    m.GetGenericArguments().Length == methodIdentifier.GenericArguments.Length).
                                          SingleOrDefault(m => m.GetParameters().Select(pi => pi.ParameterType.Name).SequenceEqual(methodIdentifier.Parameters.Select(p => p.Name)));

                method = method?.MakeGenericMethod(methodIdentifier.GenericArguments);
            }
            else
            {
                method = parametersTypes == null ?
                             methodIdentifier.Type.GetMethod(methodIdentifier.MethodName, bindingFlags) :
                             methodIdentifier.Type.GetMethod(methodIdentifier.MethodName, bindingFlags, null, methodIdentifier.Parameters, null);
            }

            if (method == null)
            {
                throw new NullReferenceException($"{methodIdentifier.Type.FullName}.{methodIdentifier.MethodName} method not found");
            }

            return method;
        }
    }

    internal readonly record struct ReflectionMethodIdentifier
    {
        internal ReflectionMethodIdentifier(Type type, string methodName, Type[] parameters, Type[] genericArguments)
        {
            Type = type;
            MethodName = methodName;
            Parameters = parameters;
            GenericArguments = genericArguments;
        }

        internal Type Type { get; }

        internal string MethodName { get; }

        internal Type[] Parameters { get; }

        internal Type[] GenericArguments { get; }
    }

    internal readonly ref struct ExpressionBodyAndParameters
    {
        internal ExpressionBodyAndParameters(
            Expression body,
            ParameterExpression thisParameterExpression,
            ParameterExpression returnParameterExpression,
            ParameterExpression durationParameterExpression,
            ParameterExpression exceptionParameterExpression,
            ParameterExpression argsOrLocalsParameterExpression)
        {
            ExpressionBody = body;
            ThisParameterExpression = thisParameterExpression;
            ReturnParameterExpression = returnParameterExpression;
            DurationParameterExpression = durationParameterExpression;
            ExceptionParameterExpression = exceptionParameterExpression;
            ArgsAndLocalsParameterExpression = argsOrLocalsParameterExpression;
        }

        internal Expression ExpressionBody { get; }

        internal ParameterExpression ThisParameterExpression { get; }

        internal ParameterExpression ReturnParameterExpression { get; }

        internal ParameterExpression DurationParameterExpression { get; }

        internal ParameterExpression ExceptionParameterExpression { get; }

        internal ParameterExpression ArgsAndLocalsParameterExpression { get; }
    }
}
