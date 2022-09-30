// <copyright file="LambdaHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class LambdaHandler
{
    private static readonly string[] Separator = { "::" };

    internal LambdaHandler(string? handlerName)
    {
        if (handlerName is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(handlerName));
        }

        var handlerTokens = handlerName.Split(Separator, StringSplitOptions.None);
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
        paramType[0] = handlerMethod.ReturnType.FullName!; // assumes it's not a generic type return etc
        for (var i = 0; i < methodParameters.Length; i++)
        {
            paramType[i + 1] = methodParameters[i].ParameterType.FullName!; // assumes it's not a generic type return etc
        }

        ParamTypeArray = paramType;
    }

    internal string[] ParamTypeArray { get; }

    internal string Assembly { get; }

    internal string FullType { get; }

    internal string MethodName { get; }
}
