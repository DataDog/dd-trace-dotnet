// <copyright file="TypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler
{
    internal static class TypeExtensions
    {
        public static System.Type GetInstrumentedType(
            this object runtimeObject,
            string instrumentedTypeName)
        {
            if (runtimeObject == null)
            {
                return null;
            }

            var currentType = runtimeObject.GetType();

            while (currentType != null)
            {
                if ($"{currentType.Namespace}.{currentType.Name}" == instrumentedTypeName)
                {
                    return currentType;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        public static System.Type GetInstrumentedType(
            this object runtimeObject,
            string instrumentedNamespace,
            string instrumentedTypeName)
        {
            if (runtimeObject == null)
            {
                return null;
            }

            var currentType = runtimeObject.GetType();

            while (currentType != null)
            {
                if (currentType.Name == instrumentedTypeName && currentType.Namespace == instrumentedNamespace)
                {
                    return currentType;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        public static System.Type GetInstrumentedInterface(
            this object runtimeObject,
            string instrumentedInterfaceName)
        {
            if (runtimeObject == null)
            {
                return null;
            }

            var currentType = runtimeObject.GetType();
            var interfaces = currentType.GetInterfaces();

            foreach (var interfaceType in interfaces)
            {
                if ($"{interfaceType.Namespace}.{interfaceType.Name}" == instrumentedInterfaceName)
                {
                    return interfaceType;
                }
            }

            return null;
        }
    }
}
