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

    // MyFunction::MyFunction.Function::HandlerCustomStructParamSync
    internal LambdaHandler(string? handlerName)
    {
        Console.WriteLine("HARVINDER TEST LOG CHANGED");
        // ThrowHelper.ThrowArgumentException($"The HARVINDER handler name {handlerName} did not have the expected format A::B::C");
        if (handlerName is null)
        {
            Console.WriteLine("handlerName is null");
            ThrowHelper.ThrowArgumentNullException(nameof(handlerName));
        }

        Console.WriteLine($"handlerName is {handlerName}");
        var handlerTokens = handlerName.Split(Separators, StringSplitOptions.None);
        for (var i = 0; i < handlerTokens.Length; i++)
        {
            Console.WriteLine($"handlerTokens[{i}] is {handlerTokens[i]}");
        }

        if (handlerTokens.Length != 3)
        {
            Console.WriteLine($"The handler name {handlerName} did not have the expected format A::B::C");
            ThrowHelper.ThrowArgumentException($"The handler name {handlerName} did not have the expected format A::B::C");
        }

        MethodName = handlerTokens[2];
        Console.WriteLine($"MethodName is {MethodName}");

        var handlerType = Type.GetType($"{handlerTokens[1]},{handlerTokens[0]}");
        Console.WriteLine($"handlerType is {handlerType}");

        var handlerMethod = handlerType?.GetMethod(MethodName);
        Console.WriteLine($"handlerMethod is {handlerMethod}");

        if (handlerMethod is null)
        {
            Console.WriteLine($"Could not find handler method for {handlerName}");
            throw new Exception($"Could not find handler method for {handlerName}");
        }

        // if (handlerMethod.IsGenericMethod || handlerMethod.DeclaringType?.IsGenericType == true)
        // {
        //     Console.WriteLine($"Unable to instrument generic handler method {handlerMethod.Name} declared on {handlerMethod.DeclaringType}");
        //     throw new Exception($"Unable to instrument generic handler method {handlerMethod.Name} declared on {handlerMethod.DeclaringType}");
        // }

        // The method body may be in a different type, e.g. a base type
        // In that case we need the FullType to point to the base type
        FullType = handlerMethod.DeclaringType?.FullName ?? handlerTokens[1];
        if (IsHandlerMethodGeneric(handlerMethod))
        {
            FullType = GetGenericFullType(FullType);
        }

        Console.WriteLine($"FullType is {FullType}");

        // If the handlerType == the declaring type, skip calling Assembly.GetName and use handler token directly
        Assembly = handlerType == handlerMethod.DeclaringType
                       ? handlerTokens[0]
                       : handlerMethod.DeclaringType?.Assembly.GetName().Name ?? handlerTokens[0];
        Console.WriteLine($"Assembly is {Assembly}");

        var methodParameters = handlerMethod.GetParameters();
        Console.WriteLine($"methodParameters is {methodParameters}");

        var paramType = new string[methodParameters.Length + 1];
        Console.WriteLine($"paramType is {paramType}");
        paramType[0] = GetTypeFullName(handlerMethod.ReturnType);
        Console.WriteLine($"paramType[0] is {paramType[0]}");
        for (var i = 0; i < methodParameters.Length; i++)
        {
            paramType[i + 1] = GetTypeFullName(methodParameters[i].ParameterType); // assumes it's not a generic type return etc
            Console.WriteLine($"paramType[{i + 1}] is {paramType[i + 1]}");
        }

        ParamTypeArray = paramType;
        for (var i = 0; i < ParamTypeArray.Length; i++)
        {
            Console.WriteLine($"ParamTypeArray[{i}] is {ParamTypeArray[i]}");
        }
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

    private static string GetGenericFullType(string fullType)
    {
        int fullTypeNameEndIndex = fullType.IndexOf("[", StringComparison.Ordinal);

        if (fullTypeNameEndIndex > 0)
        {
            return fullType.Substring(0, fullTypeNameEndIndex);
        }

        return fullType;
    }

    private static bool IsHandlerMethodGeneric(System.Reflection.MethodInfo handlerMethod)
    {
        return handlerMethod.IsGenericMethod || handlerMethod.DeclaringType?.IsGenericType == true;
    }
}
