// <copyright file="LambdaHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class LambdaHandler
{
    internal const string Separator = "::";
    private static readonly string[] Separators = { "::" };

    internal LambdaHandler(string? handlerName)
    {
        if (handlerName is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(handlerName));
        }

        var handlerTokens = handlerName.Split(Separators, StringSplitOptions.None);
        if (handlerTokens.Length != 3)
        {
            ThrowHelper.ThrowArgumentException($"The handler name {handlerName} did not have the expected format A::B::C");
        }

        MethodName = handlerTokens[2];

        var handlerType = Type.GetType($"{handlerTokens[1]},{handlerTokens[0]}");

        var handlerMethod = handlerType?.GetMethod(MethodName);
        if (handlerMethod is null)
        {
            throw new Exception($"Could not find handler method for {handlerName}");
        }

        if (handlerMethod.IsGenericMethod || handlerMethod.DeclaringType?.IsGenericType == true)
        {
            throw new Exception($"Unable to instrument generic handler method {handlerMethod.Name} declared on {handlerMethod.DeclaringType}");
        }

        // The method body may be in a different type, e.g. a base type
        // In that case we need the FullType to point to the base type
        FullType = handlerMethod.DeclaringType?.FullName ?? handlerTokens[1];
        // If the handlerType == the declaring type, skip calling Assembly.GetName and use handler token directly
        Assembly = handlerType == handlerMethod.DeclaringType
                       ? handlerTokens[0]
                       : handlerMethod.DeclaringType?.Assembly.GetName().Name ?? handlerTokens[0];

        var methodParameters = handlerMethod.GetParameters();

        var paramType = new string[methodParameters.Length + 1];
        paramType[0] = GetTypeFullName(handlerMethod.ReturnType);
        for (var i = 0; i < methodParameters.Length; i++)
        {
            paramType[i + 1] = GetTypeFullName(methodParameters[i].ParameterType); // assumes it's not a generic type return etc
        }

        ParamTypeArray = paramType;
    }

    internal string[] ParamTypeArray { get; }

    internal string Assembly { get; }

    internal string FullType { get; }

    internal string MethodName { get; }

    // We need the following rules:
    // - Standard types: Name including namespace (i.e. ToString() OR FullName)
    // - Nested types: Name must _not_ include qualifying prefix (namespace or parent type)
    // - Generic types: Name including namespace (ToString() ONLY - FullName includes assembly reference)
    // - Generic args: each argument must follow the above rules recursively
    // includes the assembly name in the parameters
    // but ToString() does not
    private static string GetTypeFullName(Type type)
        => type.IsNested switch
        {
            true when type.IsGenericType => $"{type.Name}[{GetGenericTypeArguments(type)}]",
            true => type.Name,
            _ => type.ToString()
        };

    private static string GetGenericTypeArguments(Type type)
    {
        // This isn't ideal, as if we have nested type arguments we
        // Get the SB twice and concatenate but it avoids annoying
        // recursive complexity, and is an edge case, so I think it's fine
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        var doneFirst = false;
        foreach (var argument in type.GenericTypeArguments)
        {
            if (doneFirst)
            {
                sb.Append(',');
            }
            else
            {
                doneFirst = true;
            }

            sb.Append(GetTypeFullName(argument));
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }
}
