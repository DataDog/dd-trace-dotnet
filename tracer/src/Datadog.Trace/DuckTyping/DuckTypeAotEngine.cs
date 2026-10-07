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
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
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
        /// The forward registrations that also serve runtime types a registry doesn't register, by proxy definition type (see
        /// <see cref="TryGetFallbackResult"/>): those of array types, and those of core library types (the proxies that receive
        /// the target type, and the failures of classes), for the types of the runtime's own core library a registry can't name.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, FallbackRegistration[]> ForwardFallbackTargets = new();

        /// <summary>
        /// The results created from <see cref="ForwardFallbackTargets"/>, by proxy definition type and runtime type.
        /// </summary>
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ForwardFallbackResults = new();

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
        /// Counts method-handle registrations that bind directly to object activators.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _directObjectActivatorHandleCount;

        /// <summary>
        /// Incremented when <see cref="ForwardFallbackTargets"/> changes, so a result computed from a previous state isn't cached.
        /// </summary>
        private static int _fallbackTargetsVersion;

        /// <summary>
        /// The number of registrations of the registry that failed (see <see cref="RecordRegistrationFailure"/>).
        /// </summary>
        private static int _failedRegistrationCount;

        /// <summary>
        /// The type and message of the exception of the first registration that failed.
        /// </summary>
        private static string? _firstRegistrationFailure;

        /// <summary>
        /// The module of the last registration's activator, and its registry identity: a registry registers all its mappings
        /// from one module, so the identity is resolved once.
        /// </summary>
        private static Module? _lastRegistryModule;

        private static string? _lastRegistryModuleIdentity;

        /// <summary>
        /// Gets the number of method-handle registrations that resolved directly to object activators.
        /// </summary>
        internal static int DirectObjectActivatorHandleCount => Volatile.Read(ref _directObjectActivatorHandleCount);

        /// <summary>
        /// Gets a value indicating whether the runtime supports dynamic code. The module initializer of a generated registry
        /// initializes it only when it doesn't (NativeAOT): under the JIT, the application keeps dynamic duck typing unless it
        /// calls the registry's Initialize(). Runtimes without RuntimeFeature.IsDynamicCodeSupported always support it.
        /// </summary>
        internal static bool IsDynamicCodeSupported
#if NETCOREAPP3_0_OR_GREATER
            => RuntimeFeature.IsDynamicCodeSupported;
#else
            => true;
#endif

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
        /// Handle to a static generated object-bridge activator method. The method must accept <see cref="object"/>
        /// and must return a type assignable to <paramref name="proxyDefinitionType"/>.
        /// </param>
        internal static void RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            Register(proxyDefinitionType, targetType, generatedProxyType, CreateObjectBridgeActivator(proxyDefinitionType, targetType, activatorMethodHandle), reverse: false);
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
        /// Registers a forward proxy of the generated registry with its typed activator: a CreateProxyInstance&lt;TProxy&gt; bound to
        /// its object activator (Func&lt;object?, object?&gt;), so CreateInstance&lt;TProxy&gt; calls it like dynamic duck typing's,
        /// and object-based creation calls the object activator (see DuckType.CreateTypeResult).
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="generatedProxyType">The generated proxy type.</param>
        /// <param name="typedActivator">The typed activator.</param>
        internal static void RegisterTypedProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Delegate typedActivator)
            => Register(proxyDefinitionType, targetType, generatedProxyType, typedActivator, reverse: false, fromValidatedRegistry: true);

        /// <summary>
        /// Registers a reverse proxy of the generated registry with its typed activator (see <see cref="RegisterTypedProxy"/>).
        /// </summary>
        /// <param name="typeToDeriveFrom">The type the reverse proxy derives from.</param>
        /// <param name="delegationType">The delegation type.</param>
        /// <param name="generatedProxyType">The generated proxy type.</param>
        /// <param name="typedActivator">The typed activator.</param>
        internal static void RegisterTypedReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Delegate typedActivator)
            => Register(typeToDeriveFrom, delegationType, generatedProxyType, typedActivator, reverse: true, fromValidatedRegistry: true);

        /// <summary>
        /// Registers a reverse AOT proxy using a generated static activator method handle.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activatorMethodHandle">
        /// Handle to a static generated object-bridge activator method. The method must accept <see cref="object"/>
        /// and must return a type assignable to <paramref name="typeToDeriveFrom"/>.
        /// </param>
        internal static void RegisterReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            Register(typeToDeriveFrom, delegationType, generatedProxyType, CreateObjectBridgeActivator(typeToDeriveFrom, delegationType, activatorMethodHandle), reverse: true);
        }

        /// <summary>
        /// Registers the activator of a forward AOT proxy for the runtime types a registry doesn't register, besides its target
        /// type: the other array types for an array target type (dynamic duck typing binds the members of System.Array for any of
        /// them), and the classes of the runtime's own core library a registry can't name, for a core library target type they
        /// derive from or implement. The activator receives the instance and the type the proxy reports as IDuckType.Type.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type of the registration: an array type, or an interface or a class that isn't sealed of the core library.</param>
        /// <param name="generatedProxyType">The generated proxy type.</param>
        /// <param name="activator">The activator.</param>
        internal static void RegisterFallbackProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, Type, object?> activator)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (generatedProxyType is null) { ThrowHelper.ThrowArgumentNullException(nameof(generatedProxyType)); }
            if (activator is null) { ThrowHelper.ThrowArgumentNullException(nameof(activator)); }

            if (!IsFallbackTarget(targetType))
            {
                throw new ArgumentException($"AOT duck typing fallback target type '{targetType}' must be an array type, or an interface or a class that isn't sealed of the core library.", nameof(targetType));
            }

            lock (RegistrationLock)
            {
                EnsureSingleRegistryAssemblyPerProcess(activator);
                var result = ForwardRegistry.TryGetValue(new TypesTuple(proxyDefinitionType, targetType), out var registration)
                                 ? registration.CreateTypeResult
                                 : new DuckType.CreateTypeResult(proxyDefinitionType, generatedProxyType, targetType, new Func<object?, object?>(instance => activator(instance, targetType)), exceptionInfo: null);
                SetFallbackTarget(proxyDefinitionType, targetType, result, activator);
                DuckType.InvalidateFastPaths();
            }
        }

        /// <summary>
        /// Registers a forward AOT mapping failure with a cached exception.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="exceptionFactory">The nonthrowing exception factory.</param>
        internal static void RegisterProxyFailureFactory(Type proxyDefinitionType, Type targetType, Func<Exception> exceptionFactory)
            => RegisterFailureFactory(proxyDefinitionType, targetType, exceptionFactory, reverse: false);

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
        /// Registers a forward AOT mapping failure using a static thrower delegate.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="failureThrower">The static failure thrower delegate.</param>
        internal static void RegisterProxyFailure(Type proxyDefinitionType, Type targetType, Action failureThrower)
        {
            RegisterFailure(proxyDefinitionType, targetType, failureThrower, reverse: false);
        }

        /// <summary>
        /// Registers a reverse AOT mapping failure with a cached exception.
        /// </summary>
        /// <param name="typeToDeriveFrom">The proxy definition type.</param>
        /// <param name="delegationType">The target type.</param>
        /// <param name="exceptionFactory">The nonthrowing exception factory.</param>
        internal static void RegisterReverseProxyFailureFactory(Type typeToDeriveFrom, Type delegationType, Func<Exception> exceptionFactory)
            => RegisterFailureFactory(typeToDeriveFrom, delegationType, exceptionFactory, reverse: true);

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
        /// Registers a reverse AOT mapping failure using a static thrower delegate.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="failureThrower">The static failure thrower delegate.</param>
        internal static void RegisterReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, Action failureThrower)
        {
            RegisterFailure(typeToDeriveFrom, delegationType, failureThrower, reverse: true);
        }

        /// <summary>
        /// Validates that generated registry contract metadata matches the currently loaded Datadog.Trace runtime.
        /// </summary>
        /// <param name="contract">Contract payload emitted into the generated registry bootstrap.</param>
        /// <param name="metadata">Registry assembly identity metadata emitted by the generator.</param>
        internal static void ValidateContract(DuckTypeAotContract contract, DuckTypeAotAssemblyMetadata metadata)
        {
            // Contract fields must be present before any identity comparisons.
            if (StringUtil.IsNullOrWhiteSpace(contract.SchemaVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract schema version is missing.");
            }

            if (StringUtil.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly version is missing.");
            }

            if (StringUtil.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyMvid))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly MVID is missing.");
            }

            if (StringUtil.IsNullOrWhiteSpace(metadata.RegistryAssemblyFullName))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT registry assembly full name is missing.");
            }

            if (StringUtil.IsNullOrWhiteSpace(metadata.RegistryAssemblyMvid))
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
                if (StringUtil.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
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
                ForwardFallbackTargets.Clear();
                ForwardFallbackResults.Clear();
                Interlocked.Increment(ref _fallbackTargetsVersion);
                _registeredRegistryAssemblyIdentity = null;
                _validatedRegistryAssemblyIdentity = null;
                _lastRegistryModule = null;
                _lastRegistryModuleIdentity = null;
                Volatile.Write(ref _directObjectActivatorHandleCount, 0);
                Volatile.Write(ref _failedRegistrationCount, 0);
                _firstRegistrationFailure = null;
                DuckType.InvalidateFastPaths();
            }
        }

        /// <summary>
        /// Records a registration of the generated registry that failed at startup: it references a type the application's runtime
        /// can't load (e.g. a type of the generator's core library NativeAOT's doesn't define, or of an assembly the application
        /// doesn't ship). The other registrations are made; a lookup without registration names this failure.
        /// </summary>
        /// <param name="exception">The exception of the registration.</param>
        internal static void RecordRegistrationFailure(Exception exception)
        {
            if (Interlocked.Increment(ref _failedRegistrationCount) == 1)
            {
                Volatile.Write(ref _firstRegistrationFailure, $"{exception.GetType().FullName}: {exception.Message}");
            }

            // The lookups that missed may have been for it.
            ForwardMissCache.Clear();
            ReverseMissCache.Clear();
        }

        /// <summary>
        /// Captures the current registry state into a reusable test-only snapshot.
        /// </summary>
        /// <param name="snapshotKey">Stable snapshot key, typically the generated registry path.</param>
        internal static void CaptureSnapshotForTests(string snapshotKey)
        {
            if (StringUtil.IsNullOrWhiteSpace(snapshotKey))
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
                    [.. ForwardFallbackTargets],
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
            if (StringUtil.IsNullOrWhiteSpace(snapshotKey))
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
                ForwardFallbackTargets.Clear();
                ForwardFallbackResults.Clear();
                Interlocked.Increment(ref _fallbackTargetsVersion);

                foreach (var entry in snapshot.ForwardFallbackTargets)
                {
                    ForwardFallbackTargets[entry.Key] = entry.Value;
                }

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
                DuckType.InvalidateFastPaths();
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

            if (!reverse && TryGetFallbackResult(key, out var fallbackResult))
            {
                return fallbackResult;
            }

            // Misses are cached too, so unsupported mappings fail deterministically across threads and repeated calls.
            var missCache = reverse ? ReverseMissCache : ForwardMissCache;
            return reverse
                       ? missCache.GetOrAdd(key, static missingKey => CreateMissingResult(missingKey, reverse: true))
                       : missCache.GetOrAdd(key, static missingKey => CreateMissingResult(missingKey, reverse: false));
        }

        /// <summary>
        /// Adds a mapping registration into the forward or reverse registry with conflict checks.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">Activator delegate used to create proxy instances for this registration.</param>
        /// <param name="reverse">Whether the registration belongs to the reverse registry.</param>
        /// <param name="fromValidatedRegistry">Whether the registration comes from the bootstrap of a generated registry, which
        /// validates its contract first.</param>
        private static void Register(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Delegate activator, bool reverse, bool fromValidatedRegistry = false)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (generatedProxyType is null) { ThrowHelper.ThrowArgumentNullException(nameof(generatedProxyType)); }
            if (activator is null) { ThrowHelper.ThrowArgumentNullException(nameof(activator)); }

            // Enforce that the generated proxy can always be assigned to the public proxy contract. The proxy type of a
            // [DuckCopy] struct is a struct over the target, like dynamic duck typing's: its activator returns a copy.
            if (!proxyDefinitionType.IsValueType && !proxyDefinitionType.IsAssignableFrom(generatedProxyType))
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
                // The bootstrap of a generated registry validates its contract (and identity) before registering: its activators
                // don't have to be inspected (Delegate.Method is reflection, slow under NativeAOT).
                if (fromValidatedRegistry && _validatedRegistryAssemblyIdentity is { } validatedRegistryAssemblyIdentity)
                {
                    _registeredRegistryAssemblyIdentity ??= validatedRegistryAssemblyIdentity;
                }
                else
                {
                    EnsureSingleRegistryAssemblyPerProcess(activator);
                }

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

                DuckType.InvalidateFastPaths();
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

            RegisterFailure(proxyDefinitionType, targetType, CreateRegisteredFailureThrower(exceptionType, proxyDefinitionType, targetType, reverse), reverse, enforceRegistryIdentity: false);
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
            => RegisterFailure(proxyDefinitionType, targetType, failureThrower, reverse, enforceRegistryIdentity: true);

        private static void RegisterFailure(Type proxyDefinitionType, Type targetType, Action failureThrower, bool reverse, bool enforceRegistryIdentity)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (failureThrower is null) { ThrowHelper.ThrowArgumentNullException(nameof(failureThrower)); }

            var createTypeResult = new DuckType.CreateTypeResult(proxyDefinitionType, proxyType: null, targetType, activator: null, failureThrower);
            RegisterFailureResult(proxyDefinitionType, targetType, createTypeResult, failureThrower, reverse, enforceRegistryIdentity);
        }

        private static void RegisterFailureFactory(Type proxyDefinitionType, Type targetType, Func<Exception> exceptionFactory, bool reverse)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (exceptionFactory is null) { ThrowHelper.ThrowArgumentNullException(nameof(exceptionFactory)); }

            var exception = exceptionFactory() ?? throw new ArgumentException("AOT failure factory returned null.", nameof(exceptionFactory));
            var result = new DuckType.CreateTypeResult(proxyDefinitionType, proxyType: null, targetType, activator: null, ExceptionDispatchInfo.Capture(exception));
            RegisterFailureResult(proxyDefinitionType, targetType, result, exceptionFactory, reverse, enforceRegistryIdentity: true);
        }

        private static void RegisterFailureResult(Type proxyDefinitionType, Type targetType, DuckType.CreateTypeResult createTypeResult, Delegate registrySource, bool reverse, bool enforceRegistryIdentity)
        {
            var key = new TypesTuple(proxyDefinitionType, targetType);

            lock (RegistrationLock)
            {
                if (enforceRegistryIdentity)
                {
                    EnsureSingleRegistryAssemblyPerProcess(registrySource);
                }

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

                // Every array type has the members of System.Array, so the failure of an array type is the one of the others. The
                // failure of a core library class prevents serving the classes deriving from it (see
                // SelectCoreLibraryFallbackRegistration); the one of an interface doesn't.
                if (!reverse && IsFallbackTarget(targetType) && !targetType.IsInterface)
                {
                    SetFallbackTarget(proxyDefinitionType, targetType, createTypeResult, typedActivator: null);
                }

                DuckType.InvalidateFastPaths();
            }
        }

        /// <summary>
        /// Creates an exception instance for a registered AOT failure mapping.
        /// </summary>
        /// <param name="exceptionType">The exception type value.</param>
        /// <param name="proxyDefinitionType">The proxy definition type value, used to describe the failure.</param>
        /// <param name="targetType">The target type value, used to describe the failure.</param>
        /// <param name="reverse">Whether the failure belongs to the reverse registry.</param>
        /// <returns>The resulting failure thrower.</returns>
        private static Action CreateRegisteredFailureThrower(Type exceptionType, Type proxyDefinitionType, Type targetType, bool reverse)
        {
            var failureTypeName = exceptionType.FullName ?? exceptionType.Name ?? "unknown";
            var detail = reverse
                             ? $"The AOT reverse proxy deriving from '{proxyDefinitionType.FullName}' cannot be created for delegation type '{targetType.FullName}'."
                             : $"The AOT proxy for '{proxyDefinitionType.FullName}' cannot be created for target type '{targetType.FullName}'.";
            return () => DuckTypeAotRegisteredFailureException.Throw(failureTypeName, detail);
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
                    $"AOT duck typing failure thrower method '{throwerMethod}' must have signature 'void Method()'.",
                    nameof(throwerMethodHandle));
            }

            try
            {
                return (Action)Delegate.CreateDelegate(typeof(Action), throwerMethod);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"AOT duck typing failure thrower method '{throwerMethod}' could not be converted to Action.",
                    nameof(throwerMethodHandle),
                    ex);
            }
        }

        /// <summary>
        /// Materializes and validates an object-bridge activator delegate from a method handle.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="activatorMethodHandle">The activator method handle value.</param>
        /// <returns>
        /// A closed <see cref="CreateProxyInstance{T}"/> delegate compatible with the registration path.
        /// </returns>
        private static Delegate CreateObjectBridgeActivator(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle activatorMethodHandle)
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
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(object))
            {
                throw new ArgumentException(
                    $"AOT duck typing RuntimeMethodHandle activator method '{activatorMethod}' must declare exactly one parameter of type 'object'. Typed method-handle activators are not supported; register a direct Func<object?, object?> delegate or an object-bridge method handle instead.",
                    nameof(activatorMethodHandle));
            }

            if (proxyDefinitionType.IsValueType)
            {
                throw new ArgumentException(
                    $"AOT duck typing RuntimeMethodHandle activator methods are not supported for value-type proxy definition '{proxyDefinitionType}'. Register a direct Func<object?, object?> delegate instead.",
                    nameof(activatorMethodHandle));
            }

            // This guarantees that cache consumers can treat activator output as the declared proxy contract everywhere.
            if (!proxyDefinitionType.IsAssignableFrom(activatorMethod.ReturnType))
            {
                throw new ArgumentException(
                    $"AOT duck typing activator method '{activatorMethod}' return type '{activatorMethod.ReturnType}' is not assignable to proxy definition '{proxyDefinitionType}'.",
                    nameof(activatorMethodHandle));
            }

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

            // A CreateProxyInstance<TProxyDefinition> (the typed activator of a registry is bound to its object activator).
            var activatorType = activator.GetType();
            return activatorType.IsGenericType &&
                   activatorType.GetGenericTypeDefinition() == typeof(CreateProxyInstance<>) &&
                   activatorType.GetGenericArguments()[0] == proxyDefinitionType;
        }

        /// <summary>
        /// Enforces the single-registry-assembly-per-process rule based on activator assembly identity.
        /// </summary>
        /// <param name="activator">Activator delegate whose declaring assembly identifies the incoming registry.</param>
        private static void EnsureSingleRegistryAssemblyPerProcess(Delegate activator)
        {
            var module = activator.Method.Module;
            if (!ReferenceEquals(module, _lastRegistryModule))
            {
                _lastRegistryModuleIdentity = ResolveRegistryAssemblyIdentity(module);
                _lastRegistryModule = module;
            }

            var incomingRegistryAssemblyIdentity = _lastRegistryModuleIdentity!;
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            // The first registered identity defines the process-wide AOT registry boundary.
            if (StringUtil.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
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
        /// Resolves the normalized identity of the registry assembly that owns an activator method.
        /// </summary>
        /// <param name="module">The module of the activator method from the generated registry.</param>
        /// <returns>Normalized assembly identity string including module MVID.</returns>
        private static string ResolveRegistryAssemblyIdentity(Module module)
        {
            var assembly = module.Assembly;

            var assemblyFullName = assembly.FullName;
            // Fallback path for unusual runtime contexts where Assembly.FullName is unavailable.
            if (StringUtil.IsNullOrWhiteSpace(assemblyFullName))
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
            if (StringUtil.IsNullOrWhiteSpace(assemblyNameOrFullName))
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

        private static bool IsFallbackTarget(Type targetType)
            => targetType.IsArray || (!targetType.IsValueType && !targetType.IsSealed && targetType.Assembly == typeof(object).Assembly);

        /// <summary>
        /// Determines whether a type is a class of the runtime's own core library a registry can't name: a non-public type (e.g.
        /// the runtime's MethodInfo, Stream or enumerator implementations), or a closed generic type over one (e.g. the
        /// Task&lt;VoidTaskResult&gt; boxes of async methods). They differ between runtimes (NativeAOT's aren't CoreCLR's), and
        /// instances have them. Value types aren't served: a proxy copies a value-type instance, which a proxy of a type it
        /// derives from or implements would share.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>true if the type is a class of the core library a registry can't name; otherwise, false.</returns>
        internal static bool IsRuntimeInternalType(Type type)
            => !type.IsArray && !type.IsValueType && type.Assembly == typeof(object).Assembly && IsUnnameableCoreLibraryType(type);

        /// <summary>
        /// Determines whether a type is, or is built on, a non-public type of the core library: its definition, a generic
        /// argument, or the element type of an array, pointer or by-ref.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>true if the type is built on a non-public type of the core library; otherwise, false.</returns>
        private static bool IsUnnameableCoreLibraryType(Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType()!;
            }

            if (type.IsGenericParameter)
            {
                return false;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                if (IsUnnameableCoreLibraryType(type.GetGenericTypeDefinition()))
                {
                    return true;
                }

                foreach (var argument in type.GetGenericArguments())
                {
                    if (IsUnnameableCoreLibraryType(argument))
                    {
                        return true;
                    }
                }

                return false;
            }

            // The visibility of a generic type definition is the one of the definition and its declaring types.
            return type.Assembly == typeof(object).Assembly && !type.IsVisible;
        }

        /// <summary>
        /// Adds or updates the fallback registration of a proxy definition type. Must be called under the registration lock.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="result">The result of the registration (or failure).</param>
        /// <param name="typedActivator">The activator receiving the target type, or null for a failure.</param>
        private static void SetFallbackTarget(Type proxyDefinitionType, Type targetType, DuckType.CreateTypeResult result, Func<object?, Type, object?>? typedActivator)
        {
            var current = ForwardFallbackTargets.TryGetValue(proxyDefinitionType, out var registrations) ? registrations : [];
            var updated = new List<FallbackRegistration>(current.Length + 1);
            var replaced = false;
            foreach (var registration in current)
            {
                if (registration.TargetType == targetType)
                {
                    updated.Add(new FallbackRegistration(targetType, result, result.Success ? typedActivator : null));
                    replaced = true;
                    continue;
                }

                updated.Add(registration);
            }

            if (!replaced)
            {
                updated.Add(new FallbackRegistration(targetType, result, result.Success ? typedActivator : null));
            }

            ForwardFallbackTargets[proxyDefinitionType] = updated.ToArray();

            // The results created for the types these registrations serve, and their misses, may come from another one now.
            Interlocked.Increment(ref _fallbackTargetsVersion);
            ForwardFallbackResults.Clear();
            foreach (var missKey in ForwardMissCache.Keys)
            {
                if (missKey.ProxyDefinitionType == proxyDefinitionType && (missKey.TargetType.IsArray || IsRuntimeInternalType(missKey.TargetType)))
                {
                    _ = ForwardMissCache.TryRemove(missKey, out _);
                }
            }
        }

        /// <summary>
        /// Gets the result for a runtime type without registration that a registry can't name, like dynamic duck typing creates a
        /// proxy for it:
        /// <list type="bullet">
        /// <item>An array type: the registration (or failure) of the most derived registered array type it is assignable to, or
        /// else of any registered array type. Every array type has the members of System.Array, which their proxies bind, so
        /// they behave the same. A failure names the runtime type like dynamic duck typing's.</item>
        /// <item>A class of the core library a registry can't name (one the registry doesn't register, e.g. a type only
        /// NativeAOT has): the proxy of a registered type it derives from or implements (see
        /// <see cref="SelectCoreLibraryFallbackRegistration"/>), bound to the members of that type. A failure isn't replayed: it
        /// would be the one of another type.</item>
        /// </list>
        /// The proxy reports the runtime type as IDuckType.Type.
        /// </summary>
        /// <param name="key">The proxy definition type and the runtime type.</param>
        /// <param name="result">The result for the runtime type.</param>
        /// <returns>true if a registration applies; otherwise, false.</returns>
        private static bool TryGetFallbackResult(TypesTuple key, out DuckType.CreateTypeResult result)
        {
            // The version is read before the registrations, so a result computed from registrations replaced meanwhile isn't cached.
            var version = Volatile.Read(ref _fallbackTargetsVersion);
            if (!ForwardFallbackTargets.TryGetValue(key.ProxyDefinitionType, out var registrations))
            {
                result = default;
                return false;
            }

            var type = key.TargetType;
            var isArray = type.IsArray;
            if (!isArray && !IsRuntimeInternalType(type))
            {
                result = default;
                return false;
            }

            if (ForwardFallbackResults.TryGetValue(key, out result))
            {
                return true;
            }

            var selected = isArray ? SelectArrayFallbackRegistration(registrations, type) : SelectCoreLibraryFallbackRegistration(registrations, type);
            if (selected is null)
            {
                return false;
            }

            if (selected.TypedActivator is { } typedActivator)
            {
                result = selected.Result.WithTargetType(
                    type,
                    new Func<object?, object?>(instance =>
                    {
                        // Like the activator dynamic duck typing creates for this type, which casts the instance to it.
                        if (instance is not null && !type.IsInstanceOfType(instance))
                        {
                            ThrowActivatorInvalidCast(instance, type);
                        }

                        return typedActivator(instance, type);
                    }));
            }
            else
            {
                result = CreateFallbackFailure(selected.Result, selected.TargetType, key);
            }

            lock (RegistrationLock)
            {
                // A registration made while this result was computed may select another one: it's returned, but not cached.
                if (version == Volatile.Read(ref _fallbackTargetsVersion))
                {
                    result = ForwardFallbackResults.GetOrAdd(key, result);
                }
            }

            return true;
        }

        /// <summary>
        /// Throws the exception the activator of a proxy throws for an instance of another type: the InvalidCastException of
        /// CoreCLR's cast, which names both types (NativeAOT's doesn't).
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <param name="type">The type the activator casts the instance to.</param>
        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowActivatorInvalidCast(object instance, Type type)
            => throw new InvalidCastException($"Unable to cast object of type '{instance.GetType()}' to type '{type}'.");

        /// <summary>
        /// Selects the registration serving another array type: the one (or the failure) of the most derived registered array type
        /// it is assignable to, or else of any registered array type (the proxy of an array type stores the instance as
        /// System.Array).
        /// </summary>
        /// <param name="registrations">The fallback registrations of the proxy definition type.</param>
        /// <param name="type">The array type.</param>
        /// <returns>The registration, or null when no array type is registered.</returns>
        private static FallbackRegistration? SelectArrayFallbackRegistration(FallbackRegistration[] registrations, Type type)
        {
            FallbackRegistration? selected = null;
            FallbackRegistration? anyArray = null;
            foreach (var registration in registrations)
            {
                if (!registration.TargetType.IsArray)
                {
                    continue;
                }

                anyArray ??= registration;
                if (registration.TargetType.IsAssignableFrom(type) &&
                    (selected is null || selected.TargetType.IsAssignableFrom(registration.TargetType)))
                {
                    selected = registration;
                }
            }

            return selected ?? anyArray;
        }

        /// <summary>
        /// Selects the registration serving a class of the core library a registry can't name, the closest to the proxy dynamic
        /// duck typing creates for it, which binds the members of that class:
        /// <list type="number">
        /// <item>The most derived registered base class (other than System.Object): the class has its members. When it fails,
        /// the class most likely fails too (it inherits the members that fail it), but differently: no registration.</item>
        /// <item>Else the proxy of the most derived registered interface it implements. The failure of an interface isn't the
        /// one of the class (reflection doesn't find the members of the base interfaces of an interface, a class has them).</item>
        /// <item>Else the proxy of System.Object.</item>
        /// </list>
        /// </summary>
        /// <param name="registrations">The fallback registrations of the proxy definition type.</param>
        /// <param name="type">The class.</param>
        /// <returns>The registration with a proxy, or null.</returns>
        private static FallbackRegistration? SelectCoreLibraryFallbackRegistration(FallbackRegistration[] registrations, Type type)
        {
            for (var baseType = type.BaseType; baseType is not null && baseType != typeof(object); baseType = baseType.BaseType)
            {
                foreach (var registration in registrations)
                {
                    if (registration.TargetType == baseType)
                    {
                        return registration.TypedActivator is null ? null : registration;
                    }
                }
            }

            FallbackRegistration? selected = null;
            FallbackRegistration? objectRegistration = null;
            foreach (var registration in registrations)
            {
                if (registration.TypedActivator is null)
                {
                    continue;
                }

                if (registration.TargetType == typeof(object))
                {
                    objectRegistration = registration;
                }
                else if (registration.TargetType.IsInterface &&
                         registration.TargetType.IsAssignableFrom(type) &&
                         (selected is null || selected.TargetType.IsAssignableFrom(registration.TargetType)))
                {
                    selected = registration;
                }
            }

            return selected ?? objectRegistration;
        }

        /// <summary>
        /// Gets the failure of a registration for another runtime type: dynamic duck typing names the runtime type in the
        /// message of its failures (e.g. "was not found in the instance of type '...'"), the registration names its own.
        /// </summary>
        /// <param name="registeredResult">The failure of the registration.</param>
        /// <param name="registeredType">The target type of the registration.</param>
        /// <param name="key">The proxy definition type and the runtime type.</param>
        /// <returns>The failure for the runtime type.</returns>
        private static DuckType.CreateTypeResult CreateFallbackFailure(DuckType.CreateTypeResult registeredResult, Type registeredType, TypesTuple key)
        {
            var exception = registeredResult.FailureException;
            if (exception is not null && exception.GetType().FullName is { } exceptionTypeName)
            {
                var message = ReplaceQuotedTypeName(ReplaceQuotedTypeName(exception.Message, registeredType.FullName, key.TargetType.FullName), registeredType.ToString(), key.TargetType.ToString());
                if (!string.Equals(message, exception.Message, StringComparison.Ordinal) &&
                    DuckTypeAotRegisteredFailureException.Create(exceptionTypeName, message, exception.InnerException) is { } renamed &&
                    renamed.GetType() == exception.GetType())
                {
                    return new DuckType.CreateTypeResult(key.ProxyDefinitionType, proxyType: null, key.TargetType, activator: null, ExceptionDispatchInfo.Capture(renamed));
                }
            }

            return registeredResult.WithTargetType(key.TargetType, activator: null);

            static string ReplaceQuotedTypeName(string text, string? registeredName, string? runtimeName)
                => StringUtil.IsNullOrEmpty(registeredName) || runtimeName is null ? text : text.Replace($"'{registeredName}'", $"'{runtimeName}'");
        }

        /// <summary>
        /// Creates missing result.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <param name="reverse">The reverse value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckType.CreateTypeResult CreateMissingResult(TypesTuple key, bool reverse)
        {
            // Like dynamic failures, the exception is captured without being thrown: probing an unmapped pair (CanCreate,
            // TryDuckCast...) must not raise first-chance exceptions. It's only thrown if the proxy is actually created.
            return new DuckType.CreateTypeResult(
                key.ProxyDefinitionType,
                proxyType: null,
                key.TargetType,
                activator: null,
                ExceptionDispatchInfo.Capture(DuckTypeAotMissingProxyRegistrationException.Create(key.ProxyDefinitionType, key.TargetType, reverse, Volatile.Read(ref _failedRegistrationCount), Volatile.Read(ref _firstRegistrationFailure))));
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
        /// A forward registration (or failure) serving runtime types a registry can't name, with the activator its proxy has for
        /// them (see <see cref="TryGetFallbackResult"/>).
        /// </summary>
        private sealed class FallbackRegistration
        {
            internal FallbackRegistration(Type targetType, DuckType.CreateTypeResult result, Func<object?, Type, object?>? typedActivator)
            {
                TargetType = targetType;
                Result = result;
                TypedActivator = typedActivator;
            }

            internal Type TargetType { get; }

            internal DuckType.CreateTypeResult Result { get; }

            internal Func<object?, Type, object?>? TypedActivator { get; }
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
                KeyValuePair<Type, FallbackRegistration[]>[] forwardFallbackTargets,
                string? registeredRegistryAssemblyIdentity,
                string? validatedRegistryAssemblyIdentity)
            {
                ForwardRegistrations = forwardRegistrations;
                ReverseRegistrations = reverseRegistrations;
                ForwardFailures = forwardFailures;
                ReverseFailures = reverseFailures;
                ForwardFallbackTargets = forwardFallbackTargets;
                RegisteredRegistryAssemblyIdentity = registeredRegistryAssemblyIdentity;
                ValidatedRegistryAssemblyIdentity = validatedRegistryAssemblyIdentity;
            }

            internal KeyValuePair<TypesTuple, Registration>[] ForwardRegistrations { get; }

            internal KeyValuePair<TypesTuple, Registration>[] ReverseRegistrations { get; }

            internal KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] ForwardFailures { get; }

            internal KeyValuePair<TypesTuple, DuckType.CreateTypeResult>[] ReverseFailures { get; }

            internal KeyValuePair<Type, FallbackRegistration[]>[] ForwardFallbackTargets { get; }

            internal string? RegisteredRegistryAssemblyIdentity { get; }

            internal string? ValidatedRegistryAssemblyIdentity { get; }
        }
    }
}
