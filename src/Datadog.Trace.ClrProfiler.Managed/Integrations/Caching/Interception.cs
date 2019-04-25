using System;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Convenience properties and methods for integration definitions.
    /// </summary>
    internal static class Interception
    {
        internal static readonly Type[] EmptyTypes = Type.EmptyTypes;

        internal static Type[] TypeArray(params object[] objectsToCheck)
        {
            var types = new Type[objectsToCheck.Length];

            for (var i = 0; i < objectsToCheck.Length; i++)
            {
                types[i] = objectsToCheck[i].GetType();
            }

            return types;
        }

        internal static string MethodKey(Type[] genericTypes, Type[] parameterTypes)
        {
            var key = "m";

            for (int i = 0; i < (genericTypes?.Length ?? 0); i++)
            {
                Debug.Assert(genericTypes != null, nameof(genericTypes) + " != null");
                key = string.Concat(key, $"_g{genericTypes[i].FullName}");
            }

            for (int i = 0; i < (parameterTypes?.Length ?? 0); i++)
            {
                Debug.Assert(parameterTypes != null, nameof(parameterTypes) + " != null");
                key = string.Concat(key, $"_p{parameterTypes[i].FullName}");
            }

            return key;
        }
    }
}
