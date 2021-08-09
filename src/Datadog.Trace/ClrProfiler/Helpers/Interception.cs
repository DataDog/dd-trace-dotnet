// <copyright file="Interception.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    /// <summary>
    /// Convenience properties and methods for integration definitions.
    /// </summary>
    internal static class Interception
    {
        internal const Type[] NullTypeArray = null;
        internal static readonly object[] NoArgObjects = ArrayHelper.Empty<object>();
        internal static readonly Type[] NoArgTypes = Type.EmptyTypes;
        internal static readonly Type VoidType = typeof(void);

        internal static Type[] ParamsToTypes(params object[] objectsToCheck)
        {
            var types = new Type[objectsToCheck.Length];

            for (var i = 0; i < objectsToCheck.Length; i++)
            {
                types[i] = objectsToCheck[i]?.GetType();
            }

            return types;
        }

        internal static string MethodKey(
            Type owningType,
            Type returnType,
            Type[] genericTypes,
            Type[] parameterTypes)
        {
            var key = $"{owningType?.AssemblyQualifiedName}_m_r{returnType?.AssemblyQualifiedName}";

            for (ushort i = 0; i < (genericTypes?.Length ?? 0); i++)
            {
                Debug.Assert(genericTypes != null, nameof(genericTypes) + " != null");
                key = string.Concat(key, $"_g{genericTypes[i].AssemblyQualifiedName}");
            }

            for (ushort i = 0; i < (parameterTypes?.Length ?? 0); i++)
            {
                Debug.Assert(parameterTypes != null, nameof(parameterTypes) + " != null");
                key = string.Concat(key, $"_p{parameterTypes[i].AssemblyQualifiedName}");
            }

            return key;
        }
    }
}
