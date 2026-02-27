// <copyright file="DuckTypeAotEngine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.DuckTyping
{
    internal static class DuckTypeAotEngine
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly object RegistrationLock = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ForwardRegistry = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, Registration> ReverseRegistry = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ForwardMissCache = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly ConcurrentDictionary<TypesTuple, DuckType.CreateTypeResult> ReverseMissCache = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo? CreateTypedActivatorMethodInfo = typeof(DuckTypeAotEngine)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
                method.Name == nameof(CreateTypedActivator) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(Func<object?, object?>));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyVersion = typeof(DuckTypeAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly string CurrentDatadogTraceAssemblyMvid = typeof(DuckTypeAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _registeredRegistryAssemblyIdentity;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _validatedRegistryAssemblyIdentity;

        private static int _cacheVersion;

        internal static int CacheVersion => Volatile.Read(ref _cacheVersion);

        internal static DuckType.CreateTypeResult GetOrCreateProxyType(Type proxyDefinitionType, Type targetType)
        {
            return GetOrCreateResult(new TypesTuple(proxyDefinitionType, targetType), reverse: false);
        }

        internal static DuckType.CreateTypeResult GetOrCreateReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            return GetOrCreateResult(new TypesTuple(typeToDeriveFrom, delegationType), reverse: true);
        }

        internal static void RegisterProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(proxyDefinitionType, targetType, generatedProxyType, activator, reverse: false);
        }

        internal static void RegisterReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Func<object?, object?> activator)
        {
            Register(typeToDeriveFrom, delegationType, generatedProxyType, activator, reverse: true);
        }

        internal static void ValidateContract(DuckTypeAotContract contract, DuckTypeAotAssemblyMetadata metadata)
        {
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

            if (!string.Equals(DuckTypeAotContract.CurrentSchemaVersion, contract.SchemaVersion, StringComparison.Ordinal))
            {
                DuckTypeAotRegistryContractValidationException.ThrowValidation(
                    $"AOT contract schema version mismatch. Expected '{DuckTypeAotContract.CurrentSchemaVersion}', got '{contract.SchemaVersion}'.");
            }

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
                if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
                {
                    _validatedRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                    return;
                }

                if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
                {
                    DuckTypeAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
                }
            }
        }

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

        private static DuckType.CreateTypeResult GetOrCreateResult(TypesTuple key, bool reverse)
        {
            var registry = reverse ? ReverseRegistry : ForwardRegistry;
            if (registry.TryGetValue(key, out var registration))
            {
                return registration.CreateTypeResult;
            }

            var missCache = reverse ? ReverseMissCache : ForwardMissCache;
            return missCache.GetOrAdd(key, missingKey => CreateMissingResult(missingKey, reverse));
        }

        private static void Register(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator, bool reverse)
        {
            if (proxyDefinitionType is null) { ThrowHelper.ThrowArgumentNullException(nameof(proxyDefinitionType)); }
            if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }
            if (generatedProxyType is null) { ThrowHelper.ThrowArgumentNullException(nameof(generatedProxyType)); }
            if (activator is null) { ThrowHelper.ThrowArgumentNullException(nameof(activator)); }

            if (!proxyDefinitionType.IsAssignableFrom(generatedProxyType))
            {
                DuckTypeAotGeneratedProxyTypeMismatchException.Throw(proxyDefinitionType, generatedProxyType);
            }

            var key = new TypesTuple(proxyDefinitionType, targetType);
            var typedActivator = CreateTypedActivator(proxyDefinitionType, activator);
            var createTypeResult = new DuckType.CreateTypeResult(proxyDefinitionType, generatedProxyType, targetType, typedActivator, exceptionInfo: null);
            var registration = new Registration(generatedProxyType, createTypeResult);

            lock (RegistrationLock)
            {
                EnsureSingleRegistryAssemblyPerProcess(activator);

                var registry = reverse ? ReverseRegistry : ForwardRegistry;
                if (registry.TryGetValue(key, out var currentRegistration))
                {
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

        private static void EnsureSingleRegistryAssemblyPerProcess(Func<object?, object?> activator)
        {
            var incomingRegistryAssemblyIdentity = ResolveRegistryAssemblyIdentity(activator);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
            {
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return;
            }

            if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                DuckTypeAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }

        private static string ResolveRegistryAssemblyIdentity(Delegate activator)
        {
            var module = activator.Method.Module;
            var assembly = module.Assembly;

            var assemblyFullName = assembly.FullName;
            if (string.IsNullOrWhiteSpace(assemblyFullName))
            {
                var assemblyName = assembly.GetName();
                assemblyFullName = assemblyName.FullName ?? assemblyName.Name ?? "unknown";
            }

            return NormalizeRegistryAssemblyIdentity(assemblyFullName, module.ModuleVersionId.ToString("D"));
        }

        private static string NormalizeRegistryAssemblyIdentity(string assemblyNameOrFullName, string moduleMvid)
        {
            var normalizedAssemblyName = NormalizeAssemblyIdentityName(assemblyNameOrFullName);
            var normalizedMvid = Guid.TryParse(moduleMvid, out var parsedMvid) ? parsedMvid.ToString("D") : moduleMvid;
            return $"{normalizedAssemblyName}; MVID={normalizedMvid}";
        }

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
                return assemblyNameOrFullName.Trim();
            }
        }

        private static DuckType.CreateTypeResult CreateMissingResult(TypesTuple key, bool reverse)
        {
            try
            {
                DuckTypeAotMissingProxyRegistrationException.Throw(key.ProxyDefinitionType, key.TargetType, reverse);
                return default;
            }
            catch (Exception ex)
            {
                return new DuckType.CreateTypeResult(
                    key.ProxyDefinitionType,
                    proxyType: null,
                    key.TargetType,
                    activator: null,
                    ExceptionDispatchInfo.Capture(ex));
            }
        }

        private static Delegate CreateTypedActivator(Type proxyDefinitionType, Func<object?, object?> activator)
        {
            if (CreateTypedActivatorMethodInfo is null)
            {
                DuckTypeException.Throw("Unable to resolve typed AOT activator factory method.");
            }

            var typedDelegate = CreateTypedActivatorMethodInfo
                               .MakeGenericMethod(proxyDefinitionType)
                               .Invoke(null, new object[] { activator });

            if (typedDelegate is Delegate del)
            {
                return del;
            }

            DuckTypeException.Throw("Unable to create AOT typed activator delegate.");
            return null!;
        }

        private static CreateProxyInstance<TProxyDefinition> CreateTypedActivator<TProxyDefinition>(Func<object?, object?> activator)
        {
            return ProxyActivator;

            [return: NotNull]
            TProxyDefinition ProxyActivator(object? instance)
            {
                var value = activator(instance);
                if (value is null)
                {
                    ThrowHelper.ThrowNullReferenceException("AOT duck typing activator returned null.");
                }

                return (TProxyDefinition)value;
            }
        }

        private readonly struct Registration
        {
            internal Registration(Type proxyType, DuckType.CreateTypeResult createTypeResult)
            {
                ProxyType = proxyType;
                CreateTypeResult = createTypeResult;
            }

            internal Type ProxyType { get; }

            internal DuckType.CreateTypeResult CreateTypeResult { get; }

            internal bool IsEquivalent(in Registration other)
            {
                return ProxyType == other.ProxyType ||
                       string.Equals(ProxyType.AssemblyQualifiedName, other.ProxyType.AssemblyQualifiedName, StringComparison.Ordinal);
            }
        }
    }
}
