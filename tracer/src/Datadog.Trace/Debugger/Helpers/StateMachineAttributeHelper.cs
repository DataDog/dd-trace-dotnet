// <copyright file="StateMachineAttributeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
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
