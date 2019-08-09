namespace Datadog.Trace.ClrProfiler
{
    internal static class TypeExtensions
    {
        public static System.Type GetInstrumentedType(this object runtimeObject, string name)
        {
            if (runtimeObject == null)
            {
                return null;
            }

            var currentType = runtimeObject.GetType();

            while (currentType != null)
            {
                if (currentType.FullName.Contains(name))
                {
                    return currentType;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}
