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

        public static System.Type GetInstrumentedInterface(
            this object runtimeObject,
            string instrumentedInterfaceName)
        {
            if (runtimeObject == null)
            {
                return null;
            }

            var currentType = runtimeObject.GetType();
            var implementedInterfaces = currentType.GetInterfaces();

            foreach (var interfaceType in implementedInterfaces)
            {
                if ($"{currentType.Namespace}.{currentType.Name}" == instrumentedInterfaceName)
                {
                    return interfaceType;
                }
            }

            return null;
        }
    }
}
