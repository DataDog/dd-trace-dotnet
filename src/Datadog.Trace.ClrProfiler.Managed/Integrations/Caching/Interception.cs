using System;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Convenience properties and methods for integration definitions.
    /// </summary>
    internal static class Interception
    {
        internal static readonly Type[] NoArgs = Type.EmptyTypes;
        internal static readonly Type VoidType = typeof(void);

        internal static Type FindType(string assemblyName, string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (assemblyName == assemblies[i].GetName().Name)
                {
                    return assemblies[i].GetType(typeName);
                }
            }

            return null;
        }

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
