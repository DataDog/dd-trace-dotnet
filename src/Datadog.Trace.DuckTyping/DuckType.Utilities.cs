using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Checks and ensures the arguments for the Create methods
        /// </summary>
        /// <param name="proxyType">Duck type</param>
        /// <param name="instance">Instance value</param>
        /// <exception cref="ArgumentNullException">If the duck type or the instance value is null</exception>
        private static void EnsureArguments(Type proxyType, object instance)
        {
            if (proxyType is null)
            {
                throw new ArgumentNullException(nameof(proxyType), "The proxy type can't be null");
            }

            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance), "The object instance can't be null");
            }

            if (!proxyType.IsPublic && !proxyType.IsNestedPublic)
            {
                throw new DuckTypeTypeIsNotPublicException(proxyType, nameof(proxyType));
            }
        }

        private static RuntimeMethodHandle GetRuntimeHandle(DynamicMethod dynamicMethod)
        {
            _dynamicGetMethodDescriptor ??= (Func<DynamicMethod, RuntimeMethodHandle>)typeof(DynamicMethod)
                .GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate(typeof(Func<DynamicMethod, RuntimeMethodHandle>));
            return _dynamicGetMethodDescriptor(dynamicMethod);
        }
    }
}
