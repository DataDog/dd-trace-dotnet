using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck type extensions
    /// </summary>
    public static class DuckTypeExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DuckType));

        /// <summary>
        /// Gets the duck type instance for the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <typeparam name="T">Target type</typeparam>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T DuckCast<T>(this object instance)
            => DuckType.Create<T>(instance);

        /// <summary>
        /// Gets the duck type instance for the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="targetType">Target type</param>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object DuckCast(this object instance, Type targetType)
            => DuckType.Create(targetType, instance);

        /// <summary>
        /// Tries to ducktype the object implementing a base class or interface T
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <param name="instance">Object instance</param>
        /// <param name="value">Ducktype instance</param>
        /// <returns>true if the object instance was ducktyped; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDuckCast<T>(this object instance, out T value)
        {
            if (instance is not null)
            {
                Type targetType = instance.GetType();

#if NET45
                if (!typeof(T).IsVisible)
                {
                    value = default;
                    return false;
                }
#endif

                DuckType.CreateTypeResult proxyResult = DuckType.CreateCache<T>.GetProxy(targetType);
                if (proxyResult.Success)
                {
                    value = proxyResult.CreateInstance<T>(instance);
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Tries to ducktype the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="targetType">Target type</param>
        /// <param name="value">Ducktype instance</param>
        /// <returns>true if the object instance was ducktyped; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDuckCast(this object instance, Type targetType, out object value)
        {
            if (instance is not null)
            {
#if NET45
                if (!targetType.IsVisible)
                {
                    value = default;
                    return false;
                }
#endif

                DuckType.CreateTypeResult proxyResult = DuckType.GetOrCreateProxyType(targetType, instance.GetType());
                if (proxyResult.Success)
                {
                    value = proxyResult.CreateInstance(instance);
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the duck type instance for the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <typeparam name="T">Target type</typeparam>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T DuckAs<T>(this object instance)
            where T : class
        {
            if (instance is not null)
            {
#if NET45
                if (!typeof(T).IsVisible)
                {
                    return null;
                }
#endif

                DuckType.CreateTypeResult proxyResult = DuckType.CreateCache<T>.GetProxy(instance.GetType());
                if (proxyResult.Success)
                {
                    return proxyResult.CreateInstance<T>(instance);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the duck type instance for the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="targetType">Target type</param>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object DuckAs(this object instance, Type targetType)
        {
            if (instance is not null)
            {
#if NET45
                if (!targetType.IsVisible)
                {
                    return null;
                }
#endif

                DuckType.CreateTypeResult proxyResult = DuckType.GetOrCreateProxyType(targetType, instance.GetType());
                if (proxyResult.Success)
                {
                    return proxyResult.CreateInstance(instance);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets if a proxy can be created
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <typeparam name="T">Duck type</typeparam>
        /// <returns>true if the proxy can be created; otherwise, false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DuckIs<T>(this object instance)
        {
#if NET45
            if (!typeof(T).IsVisible)
            {
                return false;
            }
#endif

            return DuckType.CanCreate<T>(instance);
        }

        /// <summary>
        /// Gets if a proxy can be created
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <param name="targetType">Duck type</param>
        /// <returns>true if the proxy can be created; otherwise, false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DuckIs(this object instance, Type targetType)
        {
#if NET45
            if (!targetType.IsVisible)
            {
                return false;
            }
#endif

            return DuckType.CanCreate(targetType, instance);
        }
    }
}
