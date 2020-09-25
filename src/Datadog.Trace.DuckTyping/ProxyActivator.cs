using System;
using System.Reflection.Emit;

#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Proxy activator
    /// </summary>
    public class ProxyActivator
    {
        /// <summary>
        /// Creates a new proxy instance avoiding boxing
        /// </summary>
        /// <typeparam name="TProxyInstance">Proxy type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="value">Instance value</param>
        /// <returns>Proxy instance</returns>
        public static TProxyInstance CreateProxyTypeInstance<TProxyInstance, TInstance>(TInstance value)
        {
            if (typeof(TInstance).IsPublic || typeof(TInstance).IsNestedPublic)
            {
                return Activator<TProxyInstance, TInstance>.CreateInstance(value);
            }

            // On non public instance types we can't use the actual type due the visibility check, so we fallback to object type
            return Activator<TProxyInstance, object>.CreateInstance(value);
        }

        internal static class Activator<TProxy, TInstance>
        {
            private static readonly ConverterDelegate _converter;

            private delegate ref TProxy ConverterDelegate(ref InstanceWrapper wrapper);

            static Activator()
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

            public static TProxy CreateInstance(TInstance instance)
            {
                if (typeof(TProxy).IsValueType)
                {
                    // To avoid boxing we have to create a instance of the struct wrapper
                    // and then change it to the proxy type at IL level
                    InstanceWrapper wrapper = new InstanceWrapper(instance);
                    return _converter(ref wrapper);
                }
                else
                {
                    // Because the proxy is a class we just need to create the instance
                    // It will always allocate.
                    return (TProxy)Activator.CreateInstance(typeof(TProxy), instance);
                }
            }

            /// <summary>
            /// This structure contains the same layout as a struct proxy
            /// Used to convert the value at runtime to the proxy one without boxing.
            /// </summary>
            private readonly struct InstanceWrapper
            {
                private readonly TInstance _currentInstance;

                public InstanceWrapper(TInstance instance)
                {
                    _currentInstance = instance;
                }
            }
        }
    }
}
