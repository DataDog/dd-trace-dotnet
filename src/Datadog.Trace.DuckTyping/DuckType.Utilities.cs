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
                DuckTypeProxyTypeDefinitionIsNull.Throw();
            }

            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            if (!proxyType.IsPublic && !proxyType.IsNestedPublic)
            {
                DuckTypeTypeIsNotPublicException.Throw(proxyType, nameof(proxyType));
            }
        }

        private static bool NeedsDuckChaining(Type targetType, Type proxyType)
        {
            // The condition to apply duck chaining is:
            // 1. Both types must be differents.
            // 2. The proxy type (duck chaining proxy definition type) can't be a struct
            // 3. The proxy type can't be a generic parameter (should be a well known type)
            // 4. Can't be a base type or an iterface implemented by the targetType type.
            return
                proxyType != targetType &&
                !proxyType.IsValueType &&
                !proxyType.IsGenericParameter &&
                !proxyType.IsAssignableFrom(targetType);
        }
    }
}
