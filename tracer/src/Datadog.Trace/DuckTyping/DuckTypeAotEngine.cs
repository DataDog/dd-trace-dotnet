// <copyright file="DuckTypeAotEngine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Provides helper operations for duck type aot engine.
    /// </summary>
    internal static class DuckTypeAotEngine
    {
        /// <summary>
        /// Synchronizes access to registration lock.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object RegistrationLock = new();

        /// <summary>
        /// Forward AOT mapping registry keyed by (proxy definition type, target type).
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ForwardRegistry = new();

        /// <summary>
        /// Reverse AOT mapping registry keyed by (derive-from type, delegation type).
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ReverseRegistry = new();

        /// <summary>
        /// Forward AOT failure registry keyed by (proxy definition type, target type).
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ForwardFailureRegistry = new();

        /// <summary>
        /// Reverse AOT failure registry keyed by (derive-from type, delegation type).
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ReverseFailureRegistry = new();

        /// <summary>
        /// Forward miss cache that stores deterministic missing-registration failures.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ForwardMissCache = new();

        /// <summary>
        /// Reverse miss cache that stores deterministic missing-registration failures.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ReverseMissCache = new();

        /// <summary>
        /// Datadog.Trace assembly version for the currently loaded runtime.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyVersion = typeof(DuckTypeAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        /// <summary>
        /// Datadog.Trace module MVID for the currently loaded runtime.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyMvid = typeof(DuckTypeAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        /// <summary>
        /// Cached helper used to construct typed-to-object activator bridges without runtime DynamicInvoke.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo CreateObjectBridgeActivatorFactoryMethod =
            typeof(DuckTypeAotEngine).GetMethod(nameof(CreateObjectBridgeActivatorFactory), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Test-only snapshot cache used to restore generated registry state without replaying the bootstrap on every test.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<string, TestSnapshot> TestSnapshots = new(StringComparer.Ordinal);

        /// <summary>
        /// Registry identity captured from activator registration calls.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _registeredRegistryAssemblyIdentity;

        /// <summary>
        /// Registry identity captured from contract validation calls.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _validatedRegistryAssemblyIdentity;

        /// <summary>
        /// Monotonic cache version used to invalidate fast paths after registration changes.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _cacheVersion;

        /// <summary>
        /// Counts method-handle registrations that bind directly to object activators.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _directObjectActivatorHandleCount;

        /// <summary>
        /// Counts method-handle registrations that require a one-time typed-to-object bridge.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _adaptedTypedActivatorHandleCount;

        /// <summary>
        /// Gets cache version.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal static int CacheVersion => Volatile.Read(ref _cacheVersion);

        /// <summary>
        /// Gets the number of method-handle registrations that resolved directly to object activators.
        /// </summary>
        internal static int DirectObjectActivatorHandleCount => Volatile.Read(ref _directObjectActivatorHandleCount);

        /// <summary>
        /// Gets the number of method-handle registrations adapted from typed activators to object activators.
        /// </summary>
        internal static int AdaptedTypedActivatorHandleCount => Volatile.Read(ref _adaptedTypedActivatorHandleCount);

        /// <summary>
        /// Gets the cached forward AOT registration result for a proxy/target pair.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <returns>
        /// A cached <see cref="DuckType.CreateTypeResult"/> containing the generated proxy type and activator,
        /// or a cached missing-registration failure.
        /// </returns>
        internal static DuckType.CreateTypeResult GetOrCreateProxyType(Type proxyDefinitionType, Type targetType)
        {
            return GetOrCreateResult(new TypesTuple(proxyDefinitionType, targetType), reverse: false);
        }

        /// <summary>
        /// Gets the cached reverse AOT registration result for a derive-from/delegation pair.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <returns>
        /// A cached <see cref="DuckType.CreateTypeResult"/> containing the generated reverse proxy type and activator,
        /// or a cached missing-registration failure.
        /// </returns>
        internal static DuckType.CreateTypeResult GetOrCreateReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            return GetOrCreateResult(new TypesTuple(typeToDeriveFrom, delegationType), reverse: true);
        }

        /// <summary>
        /// Registers a forward AOT proxy using the legacy object-based activator delegate.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">
        /// Activator that receives the runtime instance boxed as <see cref="object"/> and returns the proxy instance.
        /// </param>
        internal static void RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(proxyDefinitionType, targetType, generatedProxyType, activator, reverse: false);
        }

        /// <summary>
        /// Registers a forward AOT proxy using a generated static activator method handle.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activatorMethodHandle">
        /// Handle to a static generated activator method. The method must accept either <see cref="object"/>
        /// or the exact target runtime type, and must return a type assignable to <paramref name="proxyDefinitionType"/>.
        /// </param>
        internal static void RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            Register(proxyDefinitionType, targetType, generatedProxyType, CreateTypedActivator(proxyDefinitionType, targetType, activatorMethodHandle), reverse: false);
        }

        /// <summary>
        /// Registers a reverse AOT proxy using the legacy object-based activator delegate.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">
        /// Activator that receives the delegation instance boxed as <see cref="object"/> and returns the proxy instance.
        /// </param>
        internal static void RegisterReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(typeToDeriveFrom, delegationType, generatedProxyType, activator, reverse: true);
        }

        /// <summary>
        /// Registers a reverse AOT proxy using a generated static activator method handle.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activatorMethodHandle">
        /// Handle to a static generated activator method. The method must accept either <see cref="object"/>
        /// or the exact delegation runtime type, and must return a type assignable to <paramref name="typeToDeriveFrom"/>.
        /// </param>
        internal static void RegisterReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            Register(typeToDeriveFrom, delegationType, generatedProxyType, CreateTypedActivator(typeToDeriveFrom, delegationType, activatorMethodHandle), reverse: true);
        }

        /// <summary>
        /// Registers a forward AOT mapping failure that should rethrow a dynamic-equivalent ducktyping exception.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="exceptionType">The exception type to rethrow when the mapping is requested.</param>
        internal static void RegisterProxyFailure(Type proxyDefinitionType, Type targetType, Type exceptionType)
        {
            RegisterFailure(proxyDefinitionType, targetType, exceptionType, reverse: false);
        }

        /// <summary>
        /// Registers a forward AOT mapping failure using a static thrower method.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="throwerMethodHandle">The static failure thrower method handle.</param>
        internal static void RegisterProxyFailure(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle throwerMethodHandle)
        {
            RegisterFailure(proxyDefinitionType, targetType, throwerMethodHandle, reverse: false);
        }

        /// <summary>
        /// Registers a reverse AOT mapping failure that should rethrow a dynamic-equivalent ducktyping exception.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="exceptionType">The exception type to rethrow when the mapping is requested.</param>
        internal static void RegisterReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, Type exceptionType)
        {
            RegisterFailure(typeToDeriveFrom, delegationType, exceptionType, reverse: true);
        }

        /// <summary>
        /// Registers a reverse AOT mapping failure using a static thrower method.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="throwerMethodHandle">The static failure thrower method handle.</param>
        internal static void RegisterReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, RuntimeMethodHandle throwerMethodHandle)
        {
            RegisterFailure(typeToDeriveFrom, delegationType, throwerMethodHandle, reverse: true);
        }

        /// <summary>
        /// Validates that generated registry contract metadata matches the currently loaded Datadog.Trace runtime.
        /// </summary>
        /// <param name="contract">Contract payload emitted into the generated registry bootstrap.</param>
        /// <param name="metadata">Registry assembly identity metadata emitted by the generator.</param>
        internal static void ValidateContract(DuckTypeAotContract contract, DuckTypeAotAssemblyMetadata metadata)
        {
            // Contract fields must be present before any identity comparisons.
            if (string.IsNullOrWhiteSpace(contract.SchemaVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract schema version is missing.");
            }

            if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly version is missing.");
            }

            if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyMvid))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly MVID is missing.");
            }

            if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyFullName))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT registry assembly full name is missing.");
            }

            if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyMvid))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT registry assembly MVID is missing.");
            }

            // Schema version mismatch indicates contract format drift between generator and runtime.
            if (!string.Equals(DuckTypeAotContract.CurrentSchemaVersion, contract.SchemaVersion, StringComparison.Ordinal))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation(
                    $"AOT contract schema version mismatch. Expected '{DuckTypeAotContract.CurrentSchemaVersion}', got '{contract.SchemaVersion}'.");
            }

            // Runtime assembly version/MVID must match exactly to prevent stale registry consumption.
            if (!string.Equals(CurrentDatadogTraceAssemblyVersion, contract.DatadogTraceAssemblyVersion, StringComparison.Ordinal) ||
                !string.Equals(CurrentDatadogTraceAssemblyMvid, contract.DatadogTraceAssemblyMvid, StringComparison.OrdinalIgnoreCase))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation(
                    $"AOT contract Datadog.Trace assembly mismatch. Expected version='{CurrentDatadogTraceAssemblyVersion}', mvid='{CurrentDatadogTraceAssemblyMvid}', got version='{contract.DatadogTraceAssemblyVersion}', mvid='{contract.DatadogTraceAssemblyMvid}'.");
            }

            lock (RegistrationLock)
            {
                var incomingRegistryAssemblyIdentity = NormalizeRegistryAssemblyIdentity(metadata.RegistryAssemblyFullName, metadata.RegistryAssemblyMvid);
                var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
                // First validated registry identity wins for this process.
                if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
                {
                    _validatedRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                    return;
                }

                // Different registry identities in one process are not allowed.
                if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
                {
                    DuckTypeAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
                }
            }
        }

        /// <summary>
        /// Resets reset for tests.
        /// </summary>
        internal static void ResetForTests()
        {
            lock (RegistrationLock)
            {
                ForwardRegistry.Clear();
                ReverseRegistry.Clear();
                ForwardFailureRegistry.Clear();
                ReverseFailureRegistry.Clear();
                ForwardMissCache.Clear();
                ReverseMissCache.Clear();
                _registeredRegistryAssemblyIdentity = null;
                _validatedRegistryAssemblyIdentity = null;
                Volatile.Write(ref _directObjectActivatorHandleCount, 0);
                Volatile.Write(ref _adaptedTypedActivatorHandleCount, 0);
                Interlocked.Increment(ref _cacheVersion);
            }
        }

        /// <summary>
        /// Captures the current registry state into a reusable test-only snapshot.
        /// </summary>
        /// <param name="snapshotKey">Stable snapshot key, typically the generated registry path.</param>
        internal static void CaptureSnapshotForTests(string snapshotKey)
        {
            if (string.IsNullOrWhiteSpace(snapshotKey))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(snapshotKey));
            }

            lock (RegistrationLock)
            {
                TestSnapshots[snapshotKey] = new TestSnapshot(
                    [.. ForwardRegistry],
                    [.. ReverseRegistry],
                    [.. ForwardFailureRegistry],
                    [.. ReverseFailureRegistry],
                    _registeredRegistryAssemblyIdentity,
                    _validatedRegistryAssemblyIdentity);
            }
        }

        /// <summary>
        /// Restores a previously captured registry snapshot for test execution.
        /// </summary>
        /// <param name="snapshotKey">Stable snapshot key, typically the generated registry path.</param>
        /// <returns>true if a snapshot existed and was restored; otherwise, false.</returns>
        internal static bool RestoreSnapshotForTests(string snapshotKey)
        {
            if (string.IsNullOrWhiteSpace(snapshotKey))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(snapshotKey));
            }

            if (!TestSnapshots.TryGetValue(snapshotKey, out var snapshot))
            {
                return false;
            }

            lock (RegistrationLock)
            {
                ForwardRegistry.Clear();
                ReverseRegistry.Clear();
                ForwardFailureRegistry.Clear();
                ReverseFailureRegistry.Clear();
                ForwardMissCache.Clear();
                ReverseMissCache.Clear();

                foreach (var entry in snapshot.ForwardRegistrations)
                {
                    ForwardRegistry[entry.Key] = entry.Value;
                }

                foreach (var entry in snapshot.ReverseRegistrations)
                {
                    ReverseRegistry[entry.Key] = entry.Value;
                }

                foreach (var entry in snapshot.ForwardFailures)
                {
                    ForwardFailureRegistry[entry.Key] = entry.Value;
                }

                foreach (var entry in snapshot.ReverseFailures)
                {
                    ReverseFailureRegistry[entry.Key] = entry.Value;
                }

                _registeredRegistryAssemblyIdentity = snapshot.RegisteredRegistryAssemblyIdentity;
                _validatedRegistryAssemblyIdentity = snapshot.ValidatedRegistryAssemblyIdentity;
                Volatile.Write(ref _directObjectActivatorHandleCount, 0);
                Volatile.Write(ref _adaptedTypedActivatorHandleCount, 0);
                Interlocked.Increment(ref _cacheVersion);
            }

            return true;
        }

        /// <summary>
        /// Gets an existing get or create result or creates it when it is missing.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <param name="reverse">The reverse value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckType.CreateTypeResult GetOrCreateResult(TypesTuple key, bool reverse)
        {
            var registry = reverse ? ReverseRegistry : ForwardRegistry;
            // Global hot path: once a mapping is registered, all future DuckType calls should resolve here without extra work.
            if (registry.TryGetValue(key, out var registration))
            {
                return registration.CreateTypeResult;
            }

            var failureRegistry = reverse ? ReverseFailureRegistry : ForwardFailureRegistry;
            if (failureRegistry.TryGetValue(key, out var failureResult))
            {
                return failureResult;
            }

            // Misses are cached too, so unsupported mappings fail deterministically across threads and repeated calls.
            var missCache = reverse ? ReverseMissCache : ForwardMissCache;
            return missCache.GetOrAdd(key, missingKey => CreateMissingResult(missingKey, reverse));
        }

        /// <summary>
        /// Adds a mapping registration into the forward or reverse registry with conflict checks.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">Activator delegate used to create proxy instances for this registration.</param>
        /// <param name="reverse">Whether the registration belongs to the reverse registry.</param>
        private static void Register(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Delegate activator, bool reverse)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (generatedProxyType is null) { ThrowHelper.ThrowArgumentNullException(nameof(generatedProxyType)); }
            if (activator is null) { ThrowHelper.ThrowArgumentNullException(nameof(activator)); }

            // Enforce that the generated proxy can always be assigned to the public proxy contract.
            if (!proxyDefinitionType.IsAssignableFrom(generatedProxyType))
            {
                DuckTypeAotGeneratedProxyTypeMismatchException.Throw(proxyDefinitionType, generatedProxyType);
            }

            if (!IsObjectCallableActivator(proxyDefinitionType, activator))
            {
                throw new ArgumentException(
                    $"AOT duck typing activator delegate '{activator.GetType()}' must be object-callable. " +
                    $"Supported shapes are 'Func<object?, object?>' and 'CreateProxyInstance<{proxyDefinitionType}>'.",
                    nameof(activator));
            }

            var key = new TypesTuple(proxyDefinitionType, targetType);
            var createTypeResult = new DuckType.CreateTypeResult(proxyDefinitionType, generatedProxyType, targetType, activator, exceptionInfo: null);
            var registration = new Registration(generatedProxyType, createTypeResult);

            lock (RegistrationLock)
            {
                EnsureSingleRegistryAssemblyPerProcess(activator);

                var registry = reverse ? ReverseRegistry : ForwardRegistry;
                if (registry.TryGetValue(key, out var currentRegistration))
                {
                    // Idempotent registration keeps startup resilient when bootstrap runs more than once.
                    if (currentRegistration.IsEquivalent(registration))
                    {
                        return;
                    }

                    // Different proxy for the same key would make process-wide caches non-deterministic, so fail fast.
                    DuckTypeAotProxyRegistrationConflictException.Throw(proxyDefinitionType, targetType, reverse, currentRegistration.ProxyType, generatedProxyType);
                }

                registry[key] = registration;

                // Registration must invalidate prior misses so the global engine can recover from earlier lookup order.
                var failureRegistry = reverse ? ReverseFailureRegistry : ForwardFailureRegistry;
                _ = failureRegistry.TryRemove(key, out _);

                var missCache = reverse ? ReverseMissCache : ForwardMissCache;
                _ = missCache.TryRemove(key, out _);

                Interlocked.Increment(ref _cacheVersion);
            }
        }

        /// <summary>
        /// Adds a failure registration into the forward or reverse failure registry.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="exceptionType">The exception type to rethrow for this mapping.</param>
        /// <param name="reverse">Whether the registration belongs to the reverse registry.</param>
        private static void RegisterFailure(Type proxyDefinitionType, Type targetType, Type exceptionType, bool reverse)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (exceptionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(exceptionType)); }
            if (!typeof(Exception).IsAssignableFrom(exceptionType))
            {
                throw new ArgumentException($"Failure exception type '{exceptionType}' must derive from Exception.", nameof(exceptionType));
            }

            RegisterFailure(proxyDefinitionType, targetType, CreateRegisteredFailureThrower(exceptionType), reverse);
        }

        /// <summary>
        /// Adds a failure registration into the forward or reverse failure registry.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="throwerMethodHandle">The failure thrower method handle.</param>
        /// <param name="reverse">Whether the registration belongs to the reverse registry.</param>
        private static void RegisterFailure(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle throwerMethodHandle, bool reverse)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }

            RegisterFailure(proxyDefinitionType, targetType, CreateRegisteredFailureThrower(throwerMethodHandle), reverse);
        }

        /// <summary>
        /// Adds a failure registration into the forward or reverse failure registry.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="failureThrower">The failure thrower for the registration.</param>
        /// <param name="reverse">Whether the registration belongs to the reverse registry.</param>
        private static void RegisterFailure(Type proxyDefinitionType, Type targetType, Action failureThrower, bool reverse)
        {
            var key = new TypesTuple(proxyDefinitionType, targetType);
            var createTypeResult = new DuckType.CreateTypeResult(proxyDefinitionType, proxyType: null, targetType, activator: null, failureThrower);

            lock (RegistrationLock)
            {
                var registry = reverse ? ReverseRegistry : ForwardRegistry;
                // A concrete registration always takes precedence over a failure registration.
                if (registry.ContainsKey(key))
                {
                    return;
                }

                var failureRegistry = reverse ? ReverseFailureRegistry : ForwardFailureRegistry;
                if (failureRegistry.ContainsKey(key))
                {
                    return;
                }

                failureRegistry[key] = createTypeResult;

                var missCache = reverse ? ReverseMissCache : ForwardMissCache;
                _ = missCache.TryRemove(key, out _);

                Interlocked.Increment(ref _cacheVersion);
            }
        }

        /// <summary>
        /// Creates an exception instance for a registered AOT failure mapping.
        /// </summary>
        /// <param name="exceptionType">The exception type value.</param>
        /// <returns>The resulting failure thrower.</returns>
        private static Action CreateRegisteredFailureThrower(Type exceptionType)
        {
            if (exceptionType == typeof(DuckTypePropertyCantBeWrittenException))
            {
                return ThrowPropertyCantBeWrittenSentinelFailure;
            }

            if (exceptionType == typeof(DuckTypeFieldIsReadonlyException))
            {
                return ThrowFieldReadonlySentinelFailure;
            }

            var failureTypeName = exceptionType.FullName ?? exceptionType.Name ?? "unknown";
            return () => DuckTypeAotRegisteredFailureException.Throw(failureTypeName, detail: string.Empty);
        }

        /// <summary>
        /// Creates an exception instance for a registered AOT failure mapping using a generated thrower.
        /// </summary>
        /// <param name="throwerMethodHandle">The thrower method handle value.</param>
        /// <returns>The resulting failure thrower.</returns>
        private static Action CreateRegisteredFailureThrower(RuntimeMethodHandle throwerMethodHandle)
        {
            if (throwerMethodHandle.Equals(default(RuntimeMethodHandle)))
            {
                throw new ArgumentException("AOT duck typing failure thrower method handle cannot be default.", nameof(throwerMethodHandle));
            }

            MethodInfo? throwerMethod;
            try
            {
                throwerMethod = MethodBase.GetMethodFromHandle(throwerMethodHandle) as MethodInfo;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("AOT duck typing failure thrower method handle could not be resolved.", nameof(throwerMethodHandle), ex);
            }

            if (throwerMethod is null)
            {
                throw new ArgumentException("AOT duck typing failure thrower method handle does not reference a method.", nameof(throwerMethodHandle));
            }

            if (!throwerMethod.IsStatic)
            {
                throw new ArgumentException(
                    $"AOT duck typing failure thrower method '{throwerMethod}' must be static.",
                    nameof(throwerMethodHandle));
            }

            if (throwerMethod.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"AOT duck typing failure thrower method '{throwerMethod}' must be closed (no open generic parameters).",
                    nameof(throwerMethodHandle));
            }

            if (throwerMethod.ReturnType != typeof(void) || throwerMethod.GetParameters().Length != 0)
            {
                throw new ArgumentException(
                    $"AOT duck typing failure thrower method '{throwerMethod}' must declare no parameters and return void.",
                    nameof(throwerMethodHandle));
            }

            try
            {
                return (Action)Delegate.CreateDelegate(typeof(Action), throwerMethod);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"AOT duck typing failure thrower method '{throwerMethod}' could not be converted to delegate '{typeof(Action)}'.",
                    nameof(throwerMethodHandle),
                    ex);
            }
        }

        private static void ThrowPropertyCantBeWrittenSentinelFailure()
        {
            var sentinelProperty = typeof(string).GetProperty(nameof(string.Length), BindingFlags.Public | BindingFlags.Instance);
            if (sentinelProperty is null)
            {
                throw new InvalidOperationException("Unable to resolve sentinel property for DuckTypePropertyCantBeWrittenException.");
            }

            DuckTypePropertyCantBeWrittenException.Throw(sentinelProperty);
        }

        private static void ThrowFieldReadonlySentinelFailure()
        {
            var sentinelField = typeof(string).GetField(nameof(string.Empty), BindingFlags.Public | BindingFlags.Static);
            if (sentinelField is null)
            {
                throw new InvalidOperationException("Unable to resolve sentinel field for DuckTypeFieldIsReadonlyException.");
            }

            DuckTypeFieldIsReadonlyException.Throw(sentinelField);
        }

        /// <summary>
        /// Materializes and validates a strongly typed activator delegate from a method handle.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="activatorMethodHandle">The activator method handle value.</param>
        /// <returns>
        /// A closed delegate compatible with the registration path:
        /// <list type="bullet">
        /// <item><description><see cref="CreateProxyInstance{T}"/> when parameter type is <see cref="object"/>.</description></item>
        /// <item><description><see cref="Func{T1, T2}"/> adapted once into <see cref="Func{T, TResult}"/> for typed compatibility handles.</description></item>
        /// </list>
        /// </returns>
        private static Delegate CreateTypedActivator(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle activatorMethodHandle)
        {
            if (proxyDefinitionType is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType));
            }

            if (targetType is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(targetType));
            }

            if (activatorMethodHandle.Equals(default(RuntimeMethodHandle)))
            {
                throw new ArgumentException("AOT duck typing activator method handle cannot be default.", nameof(activatorMethodHandle));
            }

            MethodInfo? activatorMethod;
            try
            {
                activatorMethod = MethodBase.GetMethodFromHandle(activatorMethodHandle) as MethodInfo;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("AOT duck typing activator method handle could not be resolved.", nameof(activatorMethodHandle), ex);
            }

            if (activatorMethod is null)
            {
                throw new ArgumentException("AOT duck typing activator method handle does not reference a method.", nameof(activatorMethodHandle));
            }

            // Requiring static/closed activators keeps bootstrap deterministic and avoids runtime generic binding surprises.
            if (!activatorMethod.IsStatic)
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' must be static.",
                    nameof(activatorMethodHandle));
            }

            if (activatorMethod.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' must be closed (no open generic parameters).",
                    nameof(activatorMethodHandle));
            }

            var parameters = activatorMethod.GetParameters();
            // Support both generated activation models:
            // 1) object bridge for registration entrypoints, 2) concrete typed parameter for lower-overhead paths.
            if (parameters.Length != 1 ||
                (parameters[0].ParameterType != typeof(object) && parameters[0].ParameterType != targetType))
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' must declare exactly one parameter of type '{targetType}' or 'object'.",
                    nameof(activatorMethodHandle));
            }

            // This guarantees that cache consumers can treat activator output as the declared proxy contract everywhere.
            if (!proxyDefinitionType.IsAssignableFrom(activatorMethod.ReturnType))
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' return type '{activatorMethod.ReturnType}' is not assignable to proxy definition '{proxyDefinitionType}'.",
                    nameof(activatorMethodHandle));
            }

            if (parameters[0].ParameterType == typeof(object))
            {
                var objectDelegateType = typeof(CreateProxyInstance<>).MakeGenericType(proxyDefinitionType);
                try
                {
                    Interlocked.Increment(ref _directObjectActivatorHandleCount);
                    return Delegate.CreateDelegate(objectDelegateType, activatorMethod);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        $"AOT duck typing activator method '{activatorMethod}' could not be converted to delegate '{objectDelegateType}'.",
                        nameof(activatorMethodHandle),
                        ex);
                }
            }

            try
            {
                var bridgeFactory = CreateObjectBridgeActivatorFactoryMethod.MakeGenericMethod(targetType, proxyDefinitionType);
                var adaptedActivator = bridgeFactory.Invoke(obj: null, parameters: [activatorMethod]) as Func<object?, object?>;
                if (adaptedActivator is null)
                {
                    throw new InvalidOperationException("AOT duck typing activator bridge factory returned null.");
                }

                Interlocked.Increment(ref _adaptedTypedActivatorHandleCount);
                return adaptedActivator;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' could not be adapted to an object activator bridge.",
                    nameof(activatorMethodHandle),
                    ex.InnerException);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' could not be adapted to an object activator bridge.",
                    nameof(activatorMethodHandle),
                    ex);
            }
        }

        /// <summary>
        /// Creates an object-callable bridge for a typed AOT activator method handle.
        /// </summary>
        /// <typeparam name="TTarget">The typed target/delegation input.</typeparam>
        /// <typeparam name="TProxy">The proxy contract output.</typeparam>
        /// <param name="activatorMethod">The typed activator method.</param>
        /// <returns>An object-callable activator that performs only a cast/unbox and a direct delegate invocation.</returns>
        private static Func<object?, object?> CreateObjectBridgeActivatorFactory<TTarget, TProxy>(MethodInfo activatorMethod)
        {
            var typedActivator = (Func<TTarget, TProxy>)Delegate.CreateDelegate(typeof(Func<TTarget, TProxy>), activatorMethod);
            return instance => typedActivator((TTarget)instance!);
        }

        /// <summary>
        /// Determines whether an activator is directly callable from the shared object-based CreateTypeResult path.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="activator">The activator delegate.</param>
        /// <returns>true if the activator is object-callable; otherwise, false.</returns>
        private static bool IsObjectCallableActivator(Type proxyDefinitionType, Delegate activator)
        {
            if (activator is Func<object?, object?>)
            {
                return true;
            }

            return activator.GetType() == typeof(CreateProxyInstance<>).MakeGenericType(proxyDefinitionType);
        }

        /// <summary>
        /// Enforces the single-registry-assembly-per-process rule based on activator assembly identity.
        /// </summary>
        /// <param name="activator">Activator delegate whose declaring assembly identifies the incoming registry.</param>
        private static void EnsureSingleRegistryAssemblyPerProcess(Delegate activator)
        {
            var incomingRegistryAssemblyIdentity = ResolveRegistryAssemblyIdentity(activator);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            // The first registered identity defines the process-wide AOT registry boundary.
            if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
            {
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return;
            }

            // Reject mixed registry identities to prevent cross-build mapping contamination in global caches.
            if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                DuckTypeAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }

        /// <summary>
        /// Resolves the normalized identity of the registry assembly that owns the activator method.
        /// </summary>
        /// <param name="activator">Activator delegate from the generated registry.</param>
        /// <returns>Normalized assembly identity string including module MVID.</returns>
        private static string ResolveRegistryAssemblyIdentity(Delegate activator)
        {
            var module = activator.Method.Module;
            var assembly = module.Assembly;

            var assemblyFullName = assembly.FullName;
            // Fallback path for unusual runtime contexts where Assembly.FullName is unavailable.
            if (string.IsNullOrWhiteSpace(assemblyFullName))
            {
                var assemblyName = assembly.GetName();
                assemblyFullName = assemblyName.FullName ?? assemblyName.Name ?? "unknown";
            }

            return NormalizeRegistryAssemblyIdentity(assemblyFullName, module.ModuleVersionId.ToString("D"));
        }

        /// <summary>
        /// Normalizes registry identity into a deterministic format.
        /// </summary>
        /// <param name="assemblyNameOrFullName">Assembly name or full name.</param>
        /// <param name="moduleMvid">Module MVID associated with the registry assembly.</param>
        /// <returns>Normalized identity string in the form <c>name, Version=x; MVID=y</c>.</returns>
        private static string NormalizeRegistryAssemblyIdentity(string assemblyNameOrFullName, string moduleMvid)
        {
            var normalizedAssemblyName = NormalizeAssemblyIdentityName(assemblyNameOrFullName);
            var normalizedMvid = Guid.TryParse(moduleMvid, out var parsedMvid) ? parsedMvid.ToString("D") : moduleMvid;
            return $"{normalizedAssemblyName}; MVID={normalizedMvid}";
        }

        /// <summary>
        /// Extracts stable assembly name/version identity used in registry matching.
        /// </summary>
        /// <param name="assemblyNameOrFullName">Assembly full name or simple name.</param>
        /// <returns>Normalized assembly identity without culture/public key details.</returns>
        private static string NormalizeAssemblyIdentityName(string assemblyNameOrFullName)
        {
            if (string.IsNullOrWhiteSpace(assemblyNameOrFullName))
            {
                return "unknown";
            }

            try
            {
                var assemblyName = new AssemblyName(assemblyNameOrFullName);
                var simpleName = assemblyName.Name ?? assemblyNameOrFullName;
                var version = assemblyName.Version?.ToString() ?? "0.0.0.0";
                return $"{simpleName}, Version={version}";
            }
            catch
            {
                // Preserve raw identity when parsing fails (for example malformed custom assembly names).
                return assemblyNameOrFullName.Trim();
            }
        }

        /// <summary>
        /// Creates missing result.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <param name="reverse">The reverse value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckType.CreateTypeResult CreateMissingResult(TypesTuple key, bool reverse)
        {
            try
            {
                DuckTypeAotMissingProxyRegistrationException.Throw(key.ProxyDefinitionType, key.TargetType, reverse);
                return default;
            }
            catch (Exception ex)
            {
                // Capture the thrown exception once and return a cached failing result for this mapping.
                return new DuckType.CreateTypeResult(
                    key.ProxyDefinitionType,
                    proxyType: null,
                    key.TargetType,
                    activator: null,
                    ExceptionDispatchInfo.Capture(ex));
            }
        }

        /// <summary>
        /// Represents registration.
        /// </summary>
        private readonly struct Registration
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Registration"/> struct.
            /// </summary>
            /// <param name="proxyType">The proxy type value.</param>
            /// <param name="createTypeResult">The create type result value.</param>
            internal Registration(Type proxyType, DuckType.CreateTypeResult createTypeResult)
            {
                ProxyType = proxyType;
                CreateTypeResult = createTypeResult;
            }

            /// <summary>
            /// Gets proxy type.
            /// </summary>
            /// <value>The proxy type value.</value>
            internal Type ProxyType { get; }

            /// <summary>
            /// Gets create type result.
            /// </summary>
            /// <value>The create type result value.</value>
            internal DuckType.CreateTypeResult CreateTypeResult { get; }

            /// <summary>
            /// Determines whether equivalent.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>true if the operation succeeds; otherwise, false.</returns>
            internal bool IsEquivalent(in Registration other)
            {
                return ProxyType == other.ProxyType ||
                       string.Equals(ProxyType.AssemblyQualifiedName, other.ProxyType.AssemblyQualifiedName, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Represents a test-only snapshot of generated registry state.
        /// </summary>
        private sealed class TestSnapshot
        {
            internal TestSnapshot(
                KeyValuePair<TypesTuple, Registration>[] forwardRegistrations,
                KeyValuePair<TypesTuple, Registration>[] reverseRegistrations,
                KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] forwardFailures,
                KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] reverseFailures,
                string? registeredRegistryAssemblyIdentity,
                string? validatedRegistryAssemblyIdentity)
            {
                ForwardRegistrations = forwardRegistrations;
                ReverseRegistrations = reverseRegistrations;
                ForwardFailures = forwardFailures;
                ReverseFailures = reverseFailures;
                RegisteredRegistryAssemblyIdentity = registeredRegistryAssemblyIdentity;
                ValidatedRegistryAssemblyIdentity = validatedRegistryAssemblyIdentity;
            }

            internal KeyValuePair<TypesTuple, Registration>[] ForwardRegistrations { get; }

            internal KeyValuePair<TypesTuple, Registration>[] ReverseRegistrations { get; }

            internal KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] ForwardFailures { get; }

            internal KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] ReverseFailures { get; }

            internal string? RegisteredRegistryAssemblyIdentity { get; }

            internal string? ValidatedRegistryAssemblyIdentity { get; }
        }
    }
}
