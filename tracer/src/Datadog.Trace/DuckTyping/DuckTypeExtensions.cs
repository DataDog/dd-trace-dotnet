// <copyright file="DuckTypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck type extensions
    /// </summary>
    internal static class DuckTypeExtensions
    {
        /// <summary>
        /// Gets the duck type instance for the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <typeparam name="T">Target type</typeparam>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull("instance")]
        public static T? DuckCast<T>(this object? instance)
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
        public static bool TryDuckCast<T>(this object? instance, [NotNullWhen(true)] out T? value)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            DuckType.CreateTypeResult proxyResult = DuckType.CreateCache<T>.GetProxy(instance.GetType());
            if (proxyResult.Success)
            {
                value = proxyResult.CreateInstance<T>(instance)!;
                return true;
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
        public static bool TryDuckCast(this object? instance, Type? targetType, [NotNullWhen(true)] out object? value)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            if (targetType != null)
            {
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
        public static T? DuckAs<T>(this object? instance)
            where T : class
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            DuckType.CreateTypeResult proxyResult = DuckType.CreateCache<T>.GetProxy(instance.GetType());
            if (proxyResult.Success)
            {
                return proxyResult.CreateInstance<T>(instance);
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
        public static object? DuckAs(this object? instance, Type? targetType)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            if (targetType != null)
            {
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
        public static bool DuckIs<T>(this object? instance)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            return DuckType.CanCreate<T>(instance);
        }

        /// <summary>
        /// Gets if a proxy can be created
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <param name="targetType">Duck type</param>
        /// <returns>true if the proxy can be created; otherwise, false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DuckIs(this object? instance, Type? targetType)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            if (targetType != null)
            {
                return DuckType.CanCreate(targetType, instance);
            }

            return false;
        }

        /// <summary>
        /// Gets or creates a proxy that implements/derives from <paramref name="typeToDeriveFrom"/>,
        /// and delegates implementations/overrides to <paramref name="instance"/>
        /// </summary>
        /// <param name="instance">The instance containing additional overrides/implementations</param>
        /// <param name="typeToDeriveFrom">The type to derive from</param>
        /// <returns>DuckType instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object DuckImplement(this object instance, Type typeToDeriveFrom)
            => DuckType.CreateReverse(typeToDeriveFrom, instance);

        /// <summary>
        /// Tries to create a proxy that implements/derives from <paramref name="typeToDeriveFrom"/>,
        /// and delegates implementations/overrides to <paramref name="instance"/>
        /// ducktype the object implementing a base class or interface T
        /// </summary>
        /// <param name="instance">The instance containing additional overrides/implementations</param>
        /// <param name="typeToDeriveFrom">The type to derive from</param>
        /// <param name="value">The Ducktype instance</param>
        /// <returns>true if the object instance was ducktyped; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDuckImplement(this object? instance, Type? typeToDeriveFrom, [NotNullWhen(true)] out object? value)
        {
            if (instance is null)
            {
                DuckTypeTargetObjectInstanceIsNull.Throw();
            }

            if (typeToDeriveFrom != null)
            {
                DuckType.CreateTypeResult proxyResult = DuckType.GetOrCreateReverseProxyType(typeToDeriveFrom, instance.GetType());
                if (proxyResult.Success)
                {
                    value = proxyResult.CreateInstance(instance);
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
