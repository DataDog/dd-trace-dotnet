// <copyright file="LambdaHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class LambdaHandler
{
    internal const string Separator = "::";
    private static readonly string[] Separators = { "::" };

    // MyFunction::MyFunction.Function::HandlerCustomStructParamSync
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

        var assemblyToken = handlerTokens[0];
        var typeToken = handlerTokens[1];
        var methodName = handlerTokens[2];

        var handlerType = Type.GetType($"{typeToken},{assemblyToken}");
        if (handlerType is null)
        {
            ThrowHelper.ThrowArgumentException($"Could not find handler type {handlerType}");
        }

        // Flattens hierarchy, because we need to potentially find an implementation on a base type
        var handlerMethod = handlerType.GetMethod(methodName);
        if (handlerMethod is null)
        {
            ThrowHelper.ThrowArgumentException($"Could not find handler method {handlerName} in type {handlerType}");
        }

        // Generic methods aren't supported in AWS Lambda
        // https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html#csharp-handler-restrictions
        if (handlerMethod.IsGenericMethod || handlerMethod.IsGenericMethodDefinition)
        {
            ThrowHelper.ThrowArgumentException($"Unable to instrument generic method {handlerMethod.Name} declared on {handlerMethod.DeclaringType}");
        }

        // The method body may be in a different type, e.g. a base type
        // In that case we need the FullType to point to the base type
        var declaringType = handlerMethod.DeclaringType;

        // We should never be invoking generic _definitions_ (i.e. open generics)
        // because the handlerType should always be a specialised type
        if (declaringType?.IsGenericTypeDefinition == true)
        {
            ThrowHelper.ThrowArgumentException($"Unable to instrument generic method {handlerMethod.Name} declared on generic type definition {declaringType}");
        }

        if (declaringType is not null
         && declaringType != handlerType
         && declaringType.IsGenericType)
        {
            // Handler is declared on a base type, which is generic
            // We need to instrument the method in the generic type, not the specialized version
            var genericTypeDefinition = declaringType.GetGenericTypeDefinition();

            // The handlerMethod _currently_ points to the specialised version of the method
            // But we need to get the non-specialised version
            // I can't find a way to get this directly from genericDefinition or handlerMethod,
            // so we search for it directly instead
            var genericMethod = genericTypeDefinition.GetMethod(handlerMethod.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static);
            if (genericMethod is null || !genericMethod.ContainsGenericParameters)
            {
                ThrowHelper.ThrowArgumentException($"Error retrieving handler method {handlerMethod.Name} from declaring type definition {genericTypeDefinition.FullName}");
            }

            handlerMethod = genericMethod;
            FullType = genericTypeDefinition.FullName ?? typeToken;
            Assembly = declaringType.Assembly.GetName().Name ?? assemblyToken;
        }
        else
        {
            // TODO: Check if lambda allows closing generics
            // E.g. if a type is defined as
            // public class MyFunctionHandlerClass<T>
            // {
            //     void Handler(T input) {}
            // }
            // Can I pass this in the handler?
            // MyFunction::MyFunction.MyFunctionHandlerClass<string>::Handler
            // Assuming that's _not_ valid for now

            FullType = (declaringType ?? handlerType).FullName ?? typeToken;

            // If the handlerType == the declaring type, skip calling Assembly.GetName
            // and use handler token directly, as we know it's the same
            Assembly = handlerType == declaringType
                           ? assemblyToken
                           : declaringType?.Assembly.GetName().Name ?? assemblyToken;
        }

        MethodName = handlerMethod.Name;
        var methodParameters = handlerMethod.GetParameters();

        var paramType = new string[methodParameters.Length + 1];
        paramType[0] = GetParameterTypeFullName(handlerMethod.ReturnType);
        for (var i = 0; i < methodParameters.Length; i++)
        {
            paramType[i + 1] = GetParameterTypeFullName(methodParameters[i].ParameterType); // assumes it's not a generic type return etc
        }

        ParamTypeArray = paramType;
    }

    internal string[] ParamTypeArray { get; }

    internal string Assembly { get; }

    internal string FullType { get; }

    internal string MethodName { get; }

    private static string GetParameterTypeFullName(Type type)
    {
        // If the parameter is `T` or `TRequest` for example, then return `!0` or `!1`
        // As we don't support instrumenting generic methods (because AWS doesn't)
        // we don't need to worry about `!!0` or `!!1`
        if (type.IsGenericParameter)
        {
            return $"!{type.GenericParameterPosition}";
        }

        // If the type is generic e.g. List<T>
        // - If the type is nested, or in the global namespace, don't include the namespace
        // - Otherwise, do include the namespace
        if (type.ContainsGenericParameters || type.IsGenericType)
        {
            return type is { IsNested: false, Namespace: { Length: > 0 } ns }
                       ? $"{ns}.{type.Name}[{GetGenericTypeArguments(type)}]"
                       : $"{type.Name}[{GetGenericTypeArguments(type)}]"; // global namespace
        }

        // Not generic
        // - Don't include namespace for nested types
        // - Do include it for other types
        return type.IsNested ? type.Name : type.ToString();
    }

    private static string GetGenericTypeArguments(Type type)
    {
        // This isn't ideal, as if we have nested type arguments we
        // Get the SB twice and concatenate but it avoids annoying
        // recursive complexity, and is an edge case, so I think it's fine
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        var doneFirst = false;
        foreach (var argument in type.GetGenericArguments())
        {
            if (doneFirst)
            {
                sb.Append(',');
            }
            else
            {
                doneFirst = true;
            }

            sb.Append(GetParameterTypeFullName(argument));
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }
}
