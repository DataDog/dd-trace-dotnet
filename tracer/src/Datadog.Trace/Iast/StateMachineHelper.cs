// <copyright file="StateMachineHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Iast;

internal static class StateMachineHelper
{
    private const string AsyncStateMachineAttributeName = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string IteratorStateMachineAttributeName = "System.Runtime.CompilerServices.IteratorStateMachineAttribute";
    private const string AsyncIteratorStateMachineAttributeName = "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute";

    private static readonly ConditionalWeakTable<Type, StrongBox<MethodBase?>> SourceMethods = new();

    /// <summary>
    /// Resolves a method of a compiler-generated async or iterator state machine (such as its MoveNext) to the
    /// method of the source code it was generated from, as System.Diagnostics.StackTrace does for its ToString.
    /// Only metadata is read: no attribute is instantiated and no customer code runs.
    /// </summary>
    /// <param name="method">the method of a stack frame</param>
    /// <param name="sourceMethod">the method the state machine was generated from</param>
    /// <returns>whether <paramref name="method"/> belongs to a state machine whose source method was found</returns>
    public static bool TryGetSourceMethod(MethodBase method, [NotNullWhen(true)] out MethodBase? sourceMethod)
    {
        sourceMethod = null;

        // not checking CompilerGeneratedAttribute: the state machines of lambdas, nested in their display class, don't have it.
        // What proves the type is a state machine is the attribute of the method below that points to it.
        var stateMachineType = method.DeclaringType;
        var parentType = stateMachineType?.DeclaringType;
        if (parentType is null
         || !(typeof(IAsyncStateMachine).IsAssignableFrom(stateMachineType) || typeof(IEnumerator).IsAssignableFrom(stateMachineType)))
        {
            return false;
        }

        // the attributes of every method of the parent type are read to find it, so the result is kept per state machine.
        // The weak keys don't keep collectible assemblies alive.
        sourceMethod = SourceMethods.GetValue(stateMachineType!, static type => new StrongBox<MethodBase?>(FindSourceMethod(type))).Value;
        return sourceMethod is not null;
    }

    private static MethodInfo? FindSourceMethod(Type stateMachineType)
    {
        var methods = stateMachineType.DeclaringType!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var candidateMethod in methods)
        {
            if (GetStateMachineType(candidateMethod) == stateMachineType)
            {
                return candidateMethod;
            }
        }

        return null;
    }

    private static Type? GetStateMachineType(MethodBase method)
    {
        foreach (var attributeData in CustomAttributeData.GetCustomAttributes(method))
        {
            var attributeName = attributeData.AttributeType.FullName;
            if ((attributeName == AsyncStateMachineAttributeName || attributeName == IteratorStateMachineAttributeName || attributeName == AsyncIteratorStateMachineAttributeName)
             && attributeData.ConstructorArguments.Count == 1
             && attributeData.ConstructorArguments[0].Value is Type type)
            {
                return type;
            }
        }

        return null;
    }
}
