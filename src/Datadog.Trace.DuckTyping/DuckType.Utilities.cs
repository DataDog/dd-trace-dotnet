using System;
using System.ComponentModel;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Gets the DuckType value for a DuckType chaining value
        /// </summary>
        /// <param name="originalValue">Original obscure value</param>
        /// <param name="proxyType">Proxy type</param>
        /// <returns>IDuckType instance</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IDuckType GetDuckTypeChainningValue(object originalValue, Type proxyType)
        {
            if (originalValue is null)
            {
                return null;
            }

            CreateTypeResult result = GetOrCreateProxyType(proxyType, originalValue.GetType());
            return result.CreateInstance(originalValue);
        }

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
    }
}
