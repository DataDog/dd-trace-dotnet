// <copyright file="MethodMetadataCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// Acts as a registry of indexed <see cref="MethodMetadataInfo"/>.
    /// Each debugger-instrumented method is given an index (hard-coded into the instrumented bytecode),
    /// and this index is used to perform O(1) lookup for the corresponding MethodMetadataInfo instance.
    /// The reason the index is incremented and dictated by the native side is to support multi-AppDomain scenarios.
    /// In these scenario, there will be multiple <see cref="EverGrowingCollection{TPayload}.Items"/> arrays, one for each AppDomain.
    /// In order for us to grab the same MethodMetadataInfo across all of them, we need to dereference the same index,
    /// because the same instrumented bytecode could execute in different AppDomains, and static fields are not shared across AppDomains.
    internal class MethodMetadataCollection : EverGrowingCollection<MethodMetadataInfo>
    {
        private static MethodMetadataCollection _instance;
        private static object _instanceLock = new();
        private static bool _instanceInitialized;

        internal static MethodMetadataCollection Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _instanceInitialized,
                    ref _instanceLock);
            }
        }

        /// <summary>
        /// Tries to create a new <see cref="MethodMetadataInfo"/> at <paramref name="index"/>.
        /// </summary>
        /// <param name="targetObject">The target object</param>
        /// <param name="index">The index of the method inside <see cref="EverGrowingCollection{TPayload}.Items"/></param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="asyncKickOffInfo">The info for the async kickoff method</param>
        /// <returns>true if succeeded (either existed before or just created), false if fails to create</returns>
        public bool TryCreateAsyncMethodMetadataIfNotExists<TTarget>(TTarget targetObject, int index, in RuntimeMethodHandle methodHandle, in RuntimeTypeHandle typeHandle, AsyncHelper.AsyncKickoffMethodInfo asyncKickOffInfo)
        {
            if (IndexExists(index))
            {
                return true;
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                if (IndexExists(index))
                {
                    return true;
                }

                EnlargeCapacity(index);

                var method = MethodBase.GetMethodFromHandle(methodHandle, typeHandle);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug<MethodBase, int, int>(nameof(MethodMetadataCollection) + "." + nameof(TryCreateAsyncMethodMetadataIfNotExists) + ": Creating a new metadata info for Async method = {Method}, index = {Index}, Items.Length = {Length}",  method, index, Items.Length);
                }

                if (method == null)
                {
                    return false;
                }

                var targetType = targetObject == null ? Type.GetTypeFromHandle(typeHandle) : targetObject.GetType();
                Items[index] = MethodMetadataInfoFactory.Create(method, targetType, asyncKickOffInfo);
            }

            return true;
        }

        /// <summary>
        /// Tries to create a new <see cref="MethodMetadataInfo"/> at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index of the method inside <see cref="EverGrowingCollection{TPayload}.Items"/></param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <returns>true if succeeded (either existed before or just created), false if fails to create</returns>
        public bool TryCreateNonAsyncMethodMetadataIfNotExists(int index, in RuntimeMethodHandle methodHandle, in RuntimeTypeHandle typeHandle)
        {
            if (IndexExists(index))
            {
                return true;
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                if (IndexExists(index))
                {
                    return true;
                }

                EnlargeCapacity(index);

                var method = MethodBase.GetMethodFromHandle(methodHandle, typeHandle);
                var type = Type.GetTypeFromHandle(typeHandle);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug<MethodBase, int, int>(nameof(MethodMetadataCollection) + "." + nameof(TryCreateNonAsyncMethodMetadataIfNotExists) + ": Creating a new metadata info for Non-Async method = {Method}, index = {Index}, Items.Length = {Length}",  method, index, Items.Length);
                }

                if (method == null)
                {
                    return false;
                }

                Items[index] = MethodMetadataInfoFactory.Create(method, type);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool IsEmpty(ref MethodMetadataInfo payload)
        {
            return payload == default;
        }
    }
}
