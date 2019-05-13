using System;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Convenience properties and methods for integration definitions.
    /// </summary>
    internal static class Interception
    {
        internal const Type[] NullTypeArray = null;
        internal static readonly Type[] EmptyTypes = Type.EmptyTypes;
        internal static readonly Type VoidType = typeof(void);

        internal static Type[] ParamsToTypes(params object[] objectsToCheck)
        {
            var types = new Type[objectsToCheck.Length];

            for (var i = 0; i < objectsToCheck.Length; i++)
            {
                types[i] = objectsToCheck[i].GetType();
            }

            return types;
        }

        internal static Type[] Types(params Type[] types)
        {
            return types;
        }

        internal static string MethodKey(Type returnType, Type[] genericTypes, Type[] parameterTypes)
        {
            var key = $"m_r{returnType.AssemblyQualifiedName}";

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
