// <copyright file="ProbeExpressionParserHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Datadog.Trace.Debugger.Expressions;

internal static class ProbeExpressionParserHelper
{
    internal static readonly Type UndefinedValueType = typeof(UndefinedValue);

    internal static readonly ConcurrentDictionary<ReflectionMethodIdentifier, MethodInfo> Methods = new();

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
