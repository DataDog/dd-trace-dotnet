// <copyright file="DuckTypeAotEngine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
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
        /// Stores cached forward registry data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ForwardRegistry = new();

        /// <summary>
        /// Stores cached reverse registry data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ReverseRegistry = new();

        /// <summary>
        /// Stores cached forward miss cache data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ForwardMissCache = new();

        /// <summary>
        /// Stores cached reverse miss cache data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ReverseMissCache = new();

        /// <summary>
        /// Stores current datadog trace assembly version.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyVersion = typeof(DuckTypeAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        /// <summary>
        /// Stores current datadog trace assembly mvid.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyMvid = typeof(DuckTypeAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        /// <summary>
        /// Stores cached registered registry assembly identity data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _registeredRegistryAssemblyIdentity;

        /// <summary>
        /// Stores cached validated registry assembly identity data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _validatedRegistryAssemblyIdentity;

        /// <summary>
        /// Stores cached cache version data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _cacheVersion;

        /// <summary>
        /// Gets cache version.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal static int CacheVersion => Volatile.Read(ref _cacheVersion);

        /// <summary>
        /// Gets an existing get or create proxy type or creates it when it is missing.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckType.CreateTypeResult GetOrCreateProxyType(Type proxyDefinitionType, Type targetType)
        {
            return GetOrCreateResult(new TypesTuple(proxyDefinitionType, targetType), reverse: false);
        }

        /// <summary>
        /// Gets an existing get or create reverse proxy type or creates it when it is missing.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckType.CreateTypeResult GetOrCreateReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            return GetOrCreateResult(new TypesTuple(typeToDeriveFrom, delegationType), reverse: true);
        }

        /// <summary>
        /// Executes register proxy.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">The activator value.</param>
        internal static void RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(proxyDefinitionType, targetType, generatedProxyType, activator, reverse: false);
        }

        /// <summary>
        /// Executes register reverse proxy.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from value.</param>
        /// <param name="delegationType">The delegation type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">The activator value.</param>
        internal static void RegisterReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(typeToDeriveFrom, delegationType, generatedProxyType, activator, reverse: true);
        }

        /// <summary>
        /// Validates validate contract.
        /// </summary>
        /// <param name="contract">The contract value.</param>
        /// <param name="metadata">The metadata value.</param>
        internal static void ValidateContract(DuckTypeAotContract contract, DuckTypeAotAssemblyMetadata metadata)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(contract.SchemaVersion)) evaluates to true.
            if (string.IsNullOrWhiteSpace(contract.SchemaVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract schema version is missing.");
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyVersion)) evaluates to true.
            if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyVersion))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly version is missing.");
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyMvid)) evaluates to true.
            if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyMvid))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly MVID is missing.");
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyFullName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyFullName))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT registry assembly full name is missing.");
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyMvid)) evaluates to true.
            if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyMvid))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation("AOT registry assembly MVID is missing.");
            }

            // Branch: take this path when (!string.Equals(DuckTypeAotContract.CurrentSchemaVersion, contract.SchemaVersion, StringComparison.Ordinal)) evaluates to true.
            if (!string.Equals(DuckTypeAotContract.CurrentSchemaVersion, contract.SchemaVersion, StringComparison.Ordinal))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation(
                    $"AOT contract schema version mismatch. Expected '{DuckTypeAotContract.CurrentSchemaVersion}', got '{contract.SchemaVersion}'.");
            }

            // Branch: take this path when (!string.Equals(CurrentDatadogTraceAssemblyVersion, contract.DatadogTraceAssemblyVersion, StringComparison.Ordinal) || evaluates to true.
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
                // Branch: take this path when (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity)) evaluates to true.
                if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
                {
                    _validatedRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                    return;
                }

                // Branch: take this path when (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal)) evaluates to true.
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
                ForwardMissCache.Clear();
                ReverseMissCache.Clear();
                _registeredRegistryAssemblyIdentity = null;
                _validatedRegistryAssemblyIdentity = null;
                Interlocked.Increment(ref _cacheVersion);
            }
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
            // Branch: take this path when (registry.TryGetValue(key, out var registration)) evaluates to true.
            if (registry.TryGetValue(key, out var registration))
            {
                return registration.CreateTypeResult;
            }

            var missCache = reverse ? ReverseMissCache : ForwardMissCache;
            return missCache.GetOrAdd(key, missingKey => CreateMissingResult(missingKey, reverse));
        }

        /// <summary>
        /// Executes register.
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="generatedProxyType">The generated proxy type value.</param>
        /// <param name="activator">The activator value.</param>
        /// <param name="reverse">The reverse value.</param>
        private static void Register(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator, bool reverse)
        {
            // Branch: take this path when (proxyDefinitionType is null) evaluates to true.
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            // Branch: take this path when (targetType is null) evaluates to true.
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            // Branch: take this path when (generatedProxyType is null) evaluates to true.
            if (generatedProxyType is null) { ThrowHelper.ThrowArgumentNullException(nameof(generatedProxyType)); }
            // Branch: take this path when (activator is null) evaluates to true.
            if (activator is null) { ThrowHelper.ThrowArgumentNullException(nameof(activator)); }

            // Branch: take this path when (!proxyDefinitionType.IsAssignableFrom(generatedProxyType)) evaluates to true.
            if (!proxyDefinitionType.IsAssignableFrom(generatedProxyType))
            {
                DuckTypeAotGeneratedProxyTypeMismatchException.Throw(proxyDefinitionType, generatedProxyType);
            }

            var key = new TypesTuple(proxyDefinitionType, targetType);
            var createTypeResult = new DuckType.CreateTypeResult(proxyDefinitionType, generatedProxyType, targetType, activator, exceptionInfo: null);
            var registration = new Registration(generatedProxyType, createTypeResult);

            lock (RegistrationLock)
            {
                EnsureSingleRegistryAssemblyPerProcess(activator);

                var registry = reverse ? ReverseRegistry : ForwardRegistry;
                // Branch: take this path when (registry.TryGetValue(key, out var currentRegistration)) evaluates to true.
                if (registry.TryGetValue(key, out var currentRegistration))
                {
                    // Branch: take this path when (currentRegistration.IsEquivalent(registration)) evaluates to true.
                    if (currentRegistration.IsEquivalent(registration))
                    {
                        return;
                    }

                    DuckTypeAotProxyRegistrationConflictException.Throw(proxyDefinitionType, targetType, reverse, currentRegistration.ProxyType, generatedProxyType);
                }

                registry[key] = registration;

                var missCache = reverse ? ReverseMissCache : ForwardMissCache;
                _ = missCache.TryRemove(key, out _);

                Interlocked.Increment(ref _cacheVersion);
            }
        }

        /// <summary>
        /// Ensures ensure single registry assembly per process.
        /// </summary>
        /// <param name="activator">The activator value.</param>
        private static void EnsureSingleRegistryAssemblyPerProcess(Func<object?, object?> activator)
        {
            var incomingRegistryAssemblyIdentity = ResolveRegistryAssemblyIdentity(activator);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            // Branch: take this path when (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity)) evaluates to true.
            if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
            {
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return;
            }

            // Branch: take this path when (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal)) evaluates to true.
            if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                DuckTypeAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }

        /// <summary>
        /// Resolves resolve registry assembly identity.
        /// </summary>
        /// <param name="activator">The activator value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ResolveRegistryAssemblyIdentity(Delegate activator)
        {
            var module = activator.Method.Module;
            var assembly = module.Assembly;

            var assemblyFullName = assembly.FullName;
            // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyFullName)) evaluates to true.
            if (string.IsNullOrWhiteSpace(assemblyFullName))
            {
                var assemblyName = assembly.GetName();
                assemblyFullName = assemblyName.FullName ?? assemblyName.Name ?? "unknown";
            }

            return NormalizeRegistryAssemblyIdentity(assemblyFullName, module.ModuleVersionId.ToString("D"));
        }

        /// <summary>
        /// Normalizes normalize registry assembly identity.
        /// </summary>
        /// <param name="assemblyNameOrFullName">The assembly name or full name value.</param>
        /// <param name="moduleMvid">The module mvid value.</param>
        /// <returns>The resulting string value.</returns>
        private static string NormalizeRegistryAssemblyIdentity(string assemblyNameOrFullName, string moduleMvid)
        {
            var normalizedAssemblyName = NormalizeAssemblyIdentityName(assemblyNameOrFullName);
            var normalizedMvid = Guid.TryParse(moduleMvid, out var parsedMvid) ? parsedMvid.ToString("D") : moduleMvid;
            return $"{normalizedAssemblyName}; MVID={normalizedMvid}";
        }

        /// <summary>
        /// Normalizes normalize assembly identity name.
        /// </summary>
        /// <param name="assemblyNameOrFullName">The assembly name or full name value.</param>
        /// <returns>The resulting string value.</returns>
        private static string NormalizeAssemblyIdentityName(string assemblyNameOrFullName)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyNameOrFullName)) evaluates to true.
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
                // Branch: handles any exception that reaches this handler.
                return assemblyNameOrFullName.Trim();
            }
        }

        /// <summary>
        /// Creates create missing result.
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
                // Branch: handles exceptions that match Exception ex.
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
            /// Determines whether is equivalent.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>true if the operation succeeds; otherwise, false.</returns>
            internal bool IsEquivalent(in Registration other)
            {
                return ProxyType == other.ProxyType ||
                       string.Equals(ProxyType.AssemblyQualifiedName, other.ProxyType.AssemblyQualifiedName, StringComparison.Ordinal);
            }
        }
    }
}
