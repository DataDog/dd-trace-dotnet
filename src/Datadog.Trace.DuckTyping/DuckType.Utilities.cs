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
            return (IDuckType)Activator.CreateInstance(result.ProxyType, originalValue);
        }

        internal static TProxyInstance CreateProxyTypeInstance<TProxyInstance>(object value)
        {
            return ProxyActivator<TProxyInstance>.CreateInstance(value);
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

        private readonly struct InstanceWrapper
        {
            private readonly object _currentInstance;

            public InstanceWrapper(object instance)
            {
                _currentInstance = instance;
            }
        }

        internal static class ProxyActivator<TProxy>
        {
            private delegate ref TProxy ConverterDelegate(ref InstanceWrapper wrapper);

#pragma warning disable SA1201 // Elements must appear in the correct order
            private static readonly ConverterDelegate _converter;
#pragma warning restore SA1201 // Elements must appear in the correct order

            static ProxyActivator()
            {
                // This dynamic method converts, a InstanceWrapper struct to another struct using IL
                // In order to work both struct must have the same layout, in this case
                // both InstanceWrapper and a Proxy type will have always the same layout.
                DynamicMethod converterMethod = new DynamicMethod(
                    $"WrapperConverter<{typeof(TProxy).Name}>._converter",
                    typeof(TProxy).MakeByRefType(),
                    new[] { typeof(InstanceWrapper).MakeByRefType() });
                ILGenerator il = converterMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ret);
                _converter = (ConverterDelegate)converterMethod.CreateDelegate(typeof(ConverterDelegate));
            }

            public static TProxy CreateInstance(object instance)
            {
                if (typeof(TProxy).IsValueType)
                {
                    InstanceWrapper wrapper = new InstanceWrapper(instance);
                    return _converter(ref wrapper);
                }
                else
                {
                    return (TProxy)Activator.CreateInstance(typeof(TProxy), instance);
                }
            }
        }
    }
}
