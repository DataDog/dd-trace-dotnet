// <copyright file="StateMachineAttributeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

#nullable enable
namespace Datadog.Trace.Util
{
    internal static class StateMachineAttributeHelper
    {
        private const string AsyncStateMachineAttributeName = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
        private const string IteratorStateMachineAttributeName = "System.Runtime.CompilerServices.IteratorStateMachineAttribute";
        private const string AsyncIteratorStateMachineAttributeName = "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute";

        internal static bool HasAsyncStateMachineAttribute(MethodBase method)
        {
            return TryGetAsyncStateMachineType(method, out _);
        }

        internal static bool TryGetAsyncStateMachineType(MethodBase method, out Type? stateMachineType)
        {
            foreach (var attributeData in CustomAttributeData.GetCustomAttributes(method))
            {
                if (attributeData.AttributeType.FullName == AsyncStateMachineAttributeName)
                {
                    return TryGetStateMachineType(attributeData, out stateMachineType);
                }
            }

            stateMachineType = null;
            return false;
        }

        internal static bool TryGetStateMachineType(MethodBase method, out Type? stateMachineType, out bool isIterator)
        {
            foreach (var attributeData in CustomAttributeData.GetCustomAttributes(method))
            {
                var attributeName = attributeData.AttributeType.FullName;
                if (attributeName != AsyncStateMachineAttributeName &&
                    attributeName != IteratorStateMachineAttributeName &&
                    attributeName != AsyncIteratorStateMachineAttributeName)
                {
                    continue;
                }

                if (TryGetStateMachineType(attributeData, out stateMachineType))
                {
                    isIterator = attributeName != AsyncStateMachineAttributeName;
                    return true;
                }
            }

            stateMachineType = null;
            isIterator = false;
            return false;
        }

        /// <summary>
        /// Resolves a method of a compiler-generated async or iterator state machine (such as its MoveNext) to the
        /// method of the source code it was generated from, as System.Diagnostics.StackTrace does for its ToString.
        /// Only metadata is read: no attribute is instantiated and no customer code runs.
        /// </summary>
        /// <param name="method">the method of a stack frame</param>
        /// <param name="sourceMethod">the method the state machine was generated from</param>
        /// <param name="isIterator">whether the state machine is an iterator, sync or async</param>
        /// <returns>whether <paramref name="method"/> belongs to a state machine whose source method was found</returns>
        internal static bool TryGetStateMachineSourceMethod(MethodBase method, out MethodBase? sourceMethod, out bool isIterator)
        {
            sourceMethod = null;
            isIterator = false;

            var stateMachineType = method.DeclaringType;
            var parentType = stateMachineType?.DeclaringType;
            // not checking CompilerGeneratedAttribute: the state machines of lambdas, nested in their display class, don't have it.
            // What proves the type is a state machine is the attribute of the method below that points to it.
            if (parentType is null
             || !(typeof(IAsyncStateMachine).IsAssignableFrom(stateMachineType) || typeof(IEnumerator).IsAssignableFrom(stateMachineType)))
            {
                return false;
            }

            var methods = parentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var candidateMethod in methods)
            {
                if (TryGetStateMachineType(candidateMethod, out var candidateStateMachineType, out var candidateIsIterator) &&
                    candidateStateMachineType == stateMachineType)
                {
                    sourceMethod = candidateMethod;
                    isIterator = candidateIsIterator;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStateMachineType(CustomAttributeData attributeData, out Type? stateMachineType)
        {
            if (attributeData.ConstructorArguments.Count == 1 &&
                attributeData.ConstructorArguments[0].Value is Type type)
            {
                stateMachineType = type;
                return true;
            }

            stateMachineType = null;
            return false;
        }
    }
}
