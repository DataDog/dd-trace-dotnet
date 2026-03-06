// <copyright file="DuckType.AOT.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// AOT-specific DuckType APIs and runtime-mode state.
    /// </summary>
    public static partial class DuckType
    {
        private const string ManualRegistrationObsoleteMessage = "Reserved for generated NativeAOT registry bootstrap. Manual registration and registry mixing are unsupported.";
        private const int RuntimeModeStateUninitialized = 0;

        private static int _runtimeModeState;

        internal static DuckTypeRuntimeMode RuntimeMode => Volatile.Read(ref _runtimeModeState) == (int)DuckTypeRuntimeMode.Aot
                                                               ? DuckTypeRuntimeMode.Aot
                                                               : DuckTypeRuntimeMode.Dynamic;

        /// <summary>
        /// Enables NativeAOT runtime mode.
        /// </summary>
        public static void EnableAotMode()
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
        }

        /// <summary>
        /// Registers a forward AOT proxy.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="generatedProxyType">Generated proxy implementation type.</param>
        /// <param name="activator">Activator used to create proxy instances.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxy(proxyDefinitionType, targetType, generatedProxyType, activator);
        }

        /// <summary>
        /// Registers a forward AOT proxy using a method handle.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="generatedProxyType">Generated proxy implementation type.</param>
        /// <param name="activatorMethodHandle">Static activator method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxy(proxyDefinitionType, targetType, generatedProxyType, activatorMethodHandle);
        }

        /// <summary>
        /// Registers a reverse AOT proxy.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="generatedProxyType">Generated reverse proxy implementation type.</param>
        /// <param name="activator">Activator used to create reverse proxy instances.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Func<object?, object?> activator)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxy(typeToDeriveFrom, delegationType, generatedProxyType, activator);
        }

        /// <summary>
        /// Registers a reverse AOT proxy using a method handle.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="generatedProxyType">Generated reverse proxy implementation type.</param>
        /// <param name="activatorMethodHandle">Static reverse activator method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxy(typeToDeriveFrom, delegationType, generatedProxyType, activatorMethodHandle);
        }

        /// <summary>
        /// Registers a forward mapping failure in AOT mode.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="exceptionType">Exception type to throw for this mapping.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailure(Type proxyDefinitionType, Type targetType, Type exceptionType)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailure(proxyDefinitionType, targetType, exceptionType);
        }

        /// <summary>
        /// Registers a forward mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="throwerMethodHandle">Static failure thrower method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailure(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle throwerMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailure(proxyDefinitionType, targetType, throwerMethodHandle);
        }

        /// <summary>
        /// Registers a reverse mapping failure in AOT mode.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="exceptionType">Exception type to throw for this mapping.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, Type exceptionType)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailure(typeToDeriveFrom, delegationType, exceptionType);
        }

        /// <summary>
        /// Registers a reverse mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="throwerMethodHandle">Static reverse failure thrower method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, RuntimeMethodHandle throwerMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailure(typeToDeriveFrom, delegationType, throwerMethodHandle);
        }

        /// <summary>
        /// Validates an AOT-generated registry contract.
        /// </summary>
        /// <param name="schemaVersion">AOT contract schema version.</param>
        /// <param name="datadogTraceAssemblyVersion">Datadog.Trace assembly version used by the generator.</param>
        /// <param name="datadogTraceAssemblyMvid">Datadog.Trace assembly MVID used by the generator.</param>
        /// <param name="registryAssemblyFullName">Generated registry assembly full name.</param>
        /// <param name="registryAssemblyMvid">Generated registry assembly module MVID.</param>
        public static void ValidateAotRegistryContract(
            string schemaVersion,
            string datadogTraceAssemblyVersion,
            string datadogTraceAssemblyMvid,
            string registryAssemblyFullName,
            string registryAssemblyMvid)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract(schemaVersion, datadogTraceAssemblyVersion, datadogTraceAssemblyMvid),
                new DuckTypeAotAssemblyMetadata(registryAssemblyFullName, registryAssemblyMvid));
        }

        /// <summary>
        /// Returns true when DuckType is running in AOT mode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAotMode()
        {
            return RuntimeMode == DuckTypeRuntimeMode.Aot;
        }

        /// <summary>
        /// Test-only reset for DuckType runtime mode and shared caches.
        /// </summary>
        internal static void ResetRuntimeModeForTests()
        {
            DuckTypeAotEngine.ResetForTests();
            DuckTypeCache.Clear();
            lock (Locker)
            {
                ActiveBuilders.Clear();
                IgnoresAccessChecksToAssembliesSetDictionary.Clear();
                _assemblyCount = 0;
                _typeCount = 0;
            }

            Volatile.Write(ref _runtimeModeState, RuntimeModeStateUninitialized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DuckTypeRuntimeMode EnsureRuntimeModeIsInitialized()
        {
            var currentState = Volatile.Read(ref _runtimeModeState);
            if (currentState == RuntimeModeStateUninitialized)
            {
                currentState = Interlocked.CompareExchange(ref _runtimeModeState, (int)DuckTypeRuntimeMode.Dynamic, RuntimeModeStateUninitialized);
                if (currentState == RuntimeModeStateUninitialized)
                {
                    currentState = (int)DuckTypeRuntimeMode.Dynamic;
                }
            }

            return currentState == (int)DuckTypeRuntimeMode.Aot ? DuckTypeRuntimeMode.Aot : DuckTypeRuntimeMode.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode mode)
        {
            var requestedState = (int)mode;
            var currentState = Interlocked.CompareExchange(ref _runtimeModeState, requestedState, RuntimeModeStateUninitialized);
            if (currentState == RuntimeModeStateUninitialized)
            {
                return;
            }

            var currentMode = currentState == (int)DuckTypeRuntimeMode.Aot ? DuckTypeRuntimeMode.Aot : DuckTypeRuntimeMode.Dynamic;
            if (currentMode != mode)
            {
                DuckTypeRuntimeModeConflictException.Throw(currentMode, mode);
            }
        }

        private static CreateTypeResult GetOrCreateDynamicProxyType(Type proxyType, Type targetType)
        {
            DuckTypeAotDiscoveryRecorder.Record(proxyType, targetType, reverse: false);

            return DuckTypeCache.GetOrAdd(
                new TypesTuple(proxyType, targetType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    var dryResult = CreateProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
        }

        private static CreateTypeResult GetOrCreateDynamicReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            DuckTypeAotDiscoveryRecorder.Record(typeToDeriveFrom, delegationType, reverse: true);

            return DuckTypeCache.GetOrAdd(
                new TypesTuple(typeToDeriveFrom, delegationType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    var dryResult = CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
        }
    }
}
