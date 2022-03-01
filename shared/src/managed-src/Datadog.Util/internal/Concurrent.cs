// <copyright file="Concurrent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Util
{
    internal static class Concurrent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetIfNull<T>(ref T storageLocation, T value)
            where T : class
        {
            T prevValue = Interlocked.CompareExchange(ref storageLocation, value, null);
            return Object.ReferenceEquals(null, prevValue);
        }

        /// <summary>
        /// If <c>storageLocation</c> is <c>null</c>, sets the contents of <c>storageLocation</c> to <c>value</c>;
        /// otherwise keeps the previous contents (in an atomic, thread-safe manner).
        /// Returns the contents of <c>storageLocation</c> after the operation.
        /// </summary>
        /// <typeparam name="T">The type of <c>value</c> (must be a class type).</typeparam>
        /// <returns>If <c>storageLocation</c> was not <c>null</c>, the original value of <c>storageLocation</c>
        /// (which is unchanged in that case);
        /// otherwise, the new value of <c>storageLocation</c> (which, in that case, is the same as the
        /// specified <c>value</c> parameter).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T TrySetOrGetValue<T>(ref T storageLocation, T value)
            where T : class
        {
            return TrySetOrGetValue(ref storageLocation, value, out bool _);
        }

        /// <summary>
        /// If <c>storageLocation</c> is <c>null</c>, sets the contents of <c>storageLocation</c> to <c>value</c>;
        /// otherwise keeps the previous contents (in an atomic, thread-safe manner).
        /// Returns the contents of <c>storageLocation</c> after the operation.
        /// <c>valueIsAtStorageLocation</c> indicates whether the specified <c>value</c> is stored
        /// at the specified at the specified <c>storageLocation</c> after this operation completes.
        /// </summary>
        /// <typeparam name="T">The type of <c>value</c> (must be a class type).</typeparam>
        /// <param name="valueIsAtStorageLocation"><c>true</c> if after this operation the object reference stored
        /// at the specified <c>storageLocation</c> is the same object as referenced by the specified <c>value</c>;
        /// <c>false</c> otherwise.</param>
        /// <returns>If <c>storageLocation</c> was not <c>null</c>, the original value of <c>storageLocation</c>
        /// (which is unchanged in that case);
        /// otherwise, the new value of <c>storageLocation</c> (which, in that case, is the same as the
        /// specified <c>value</c> parameter).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T TrySetOrGetValue<T>(ref T storageLocation, T value, out bool valueIsAtStorageLocation)
            where T : class
        {
            T valueAtStorageLocation = CompareExchangeResult(ref storageLocation, value, null);
            valueIsAtStorageLocation = Object.ReferenceEquals(valueAtStorageLocation, value);
            return valueAtStorageLocation;
        }

        /// <summary>
        /// Is like <c>Interlocked.CompareExchange(..)</c>, but returns the final/resulting value,
        /// instead of the previous/original value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CompareExchangeResult<T>(ref T storageLocation, T value, T comparand)
            where T : class
        {
            T prevValue = Interlocked.CompareExchange(ref storageLocation, value, comparand);
            return Object.ReferenceEquals(prevValue, comparand) ? value : prevValue;
        }
    }
}