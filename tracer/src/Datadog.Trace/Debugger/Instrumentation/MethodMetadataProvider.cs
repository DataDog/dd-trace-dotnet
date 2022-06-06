// <copyright file="MethodMetadataProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Acts as a registry of indexed <see cref="MethodMetadataInfo"/>.
    /// Each debugger-instrumented method is given an index (hard-coded into the instrumented bytecode),
    /// and this index is used to perform O(1) lookup for the corresponding <see cref="MethodMetadataInfo"/> instance.
    /// The reason the index is incremented and dictated by the native side is to support multi-AppDomain scenarios.
    /// In these scenario, there will be multiple <see cref="MethodMetadataProvider._items"/> arrays, one for each AppDomain.
    /// In order for us to grab the same <see cref="MethodMetadataInfo"/> across all of them, we need to dereference the same index,
    /// because the same instrumented bytecode could execute in different AppDomains, and static fields are not shared across AppDomains.
    /// </summary>
    internal static class MethodMetadataProvider
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodMetadataProvider));
        private static readonly object ItemsLocker = new object();

        private static MethodMetadataInfo[] _items = new MethodMetadataInfo[16];

        private static int Capacity
        {
            set
            {
                if (value != _items.Length)
                {
                    var newItems = new MethodMetadataInfo[value];

                    // ReSharper disable once InconsistentlySynchronizedField
                    Array.Copy(_items, 0, newItems, 0, _items.Length);

                    // ReSharper disable once InconsistentlySynchronizedField
                    _items = newItems;
                }
            }
        }

        private static void EnsureCapacity(int min)
        {
            // Enlarge the _items array as needed. Note that there is an implicit by-design memory-leak here,
            // in the sense that this unbounded collection can only grow and is never shrunk.
            // This is fine, for the time being, because we are limited to 100 simultaneous probes anyhow, so it is extremely unlikely
            // we will ever reach any substantial memory usage here.
            if (_items.Length < min)
            {
                var newCapacity = _items.Length * 2;
                if (newCapacity < min)
                {
                    newCapacity = min + 1;
                }

                Capacity = newCapacity;
            }
        }

        public static void Remove(int index)
        {
            lock (ItemsLocker)
            {
                _items[index] = default;
            }
        }

        public static bool TryCreateIfNotExists(int index, in RuntimeMethodHandle methodHandle, in RuntimeTypeHandle typeHandle)
        {
            return TryCreateIfNotExists<object>(null, index, in methodHandle, in typeHandle);
        }

        /// <summary>
        /// Tries to create a new <see cref="MethodMetadataInfo"/> at <paramref name="index"/>.
        /// </summary>
        /// <param name="targetObject">The target object</param>
        /// <param name="index">The index of the method inside <see cref="_items"/></param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <returns>true if succeeded (either existed before or just created), false if fails to create</returns>
        public static bool TryCreateIfNotExists<TTarget>(TTarget targetObject, int index, in RuntimeMethodHandle methodHandle, in RuntimeTypeHandle typeHandle)
        {
            // Check if there's a MetadataMethodInfo associated with the given index
            if (index < _items.Length)
            {
                // ReSharper disable once InconsistentlySynchronizedField
                ref var methodMetadataInfo = ref _items[index];

                if (methodMetadataInfo != default)
                {
                    return true;
                }
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                if (index == _items.Length)
                {
                    EnsureCapacity(index + 1);
                }
                else if (index > _items.Length)
                {
                    EnsureCapacity(index);
                }

                var method = MethodBase.GetMethodFromHandle(methodHandle, typeHandle);
                var type = Type.GetTypeFromHandle(typeHandle);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug($"{nameof(MethodMetadataProvider)}.{nameof(TryCreateIfNotExists)}: Creating a new metadata info for method = {method}, index = {index}, Items.Length = {_items.Length}");
                }

                if (method == null)
                {
                    return false;
                }

                MethodMetadataInfo methodMetadataInfo;
                if (targetObject == null)
                {
                    methodMetadataInfo = MethodMetadataInfoFactory.Create(method, type);
                }
                else
                {
                    methodMetadataInfo = MethodMetadataInfoFactory.Create(method, targetObject, type);
                }

                _items[index] = methodMetadataInfo;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref MethodMetadataInfo Get(int index)
        {
            return ref _items[index];
        }
    }
}
