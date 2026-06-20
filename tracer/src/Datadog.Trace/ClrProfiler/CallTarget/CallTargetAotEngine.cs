// <copyright file="CallTargetAotEngine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Stores generated CallTarget AOT registrations and materializes typed delegates on demand.
/// </summary>
internal static class CallTargetAotEngine
{
    private static readonly object RegistrationLock = new();
    private static readonly ConcurrentDictionary<CallTargetAotHandlerKey, CallTargetAotHandlerRegistration> Registrations = new();
    private static readonly string CurrentDatadogTraceAssemblyVersion = typeof(CallTargetAotEngine).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    private static readonly string CurrentDatadogTraceAssemblyMvid = typeof(CallTargetAotEngine).Assembly.ManifestModule.ModuleVersionId.ToString("D");

    private static Type? _registryType;
    private static string? _registeredRegistryAssemblyIdentity;
    private static string? _validatedRegistryAssemblyIdentity;
    private static int _registryInitialized;

    /// <summary>
    /// Ensures the generated CallTarget registry executes its registration logic exactly once.
    /// </summary>
    /// <param name="registryType">The generated registry bootstrap type.</param>
    /// <returns><see langword="true"/> when the caller should execute registration; otherwise <see langword="false"/>.</returns>
    internal static bool TryInitializeGeneratedRegistry(Type registryType)
    {
        lock (RegistrationLock)
        {
            // Prefer the validated contract identity when it is available because NativeAOT publish can remap
            // runtime assembly ownership in ways that make Type.Module.Assembly point at the final app image.
            var incomingRegistryAssemblyIdentity = _validatedRegistryAssemblyIdentity ?? ResolveRegistryAssemblyIdentity(registryType);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            if (!string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity) &&
                !string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                CallTargetAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            if (Interlocked.CompareExchange(ref _registryInitialized, 1, 0) == 0)
            {
                _registryType = registryType;
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return true;
            }

            if (!string.Equals(_registeredRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                CallTargetAotMultipleRegistryAssembliesException.Throw(_registeredRegistryAssemblyIdentity ?? "unknown", incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }

        return false;
    }

    /// <summary>
    /// Validates that the generated CallTarget AOT registry contract matches the currently loaded Datadog.Trace runtime.
    /// </summary>
    /// <param name="contract">The generated contract metadata.</param>
    /// <param name="metadata">The generated registry assembly identity metadata.</param>
    internal static void ValidateContract(CallTargetAotContract contract, CallTargetAotAssemblyMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(contract.SchemaVersion))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation("AOT contract schema version is missing.");
        }

        if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyVersion))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly version is missing.");
        }

        if (string.IsNullOrWhiteSpace(contract.DatadogTraceAssemblyMvid))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation("AOT contract Datadog.Trace assembly MVID is missing.");
        }

        if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyFullName))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation("AOT registry assembly full name is missing.");
        }

        if (string.IsNullOrWhiteSpace(metadata.RegistryAssemblyMvid))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation("AOT registry assembly MVID is missing.");
        }

        if (!string.Equals(CallTargetAotContract.CurrentSchemaVersion, contract.SchemaVersion, StringComparison.Ordinal))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation(
                $"AOT contract schema version mismatch. Expected '{CallTargetAotContract.CurrentSchemaVersion}', got '{contract.SchemaVersion}'.");
        }

        if (!string.Equals(CurrentDatadogTraceAssemblyVersion, contract.DatadogTraceAssemblyVersion, StringComparison.Ordinal) ||
            !string.Equals(CurrentDatadogTraceAssemblyMvid, contract.DatadogTraceAssemblyMvid, StringComparison.OrdinalIgnoreCase))
        {
            CallTargetAotRegistryContractValidationException.ThrowValidation(
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
                CallTargetAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _validatedRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }
    }

    /// <summary>
    /// Registers the generated begin and end handlers for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The optional target return type used by non-void end handlers.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="beginMethodName">The generated begin adapter method name.</param>
    /// <param name="endMethodName">The generated end adapter method name.</param>
    /// <param name="argumentTypes">The optional target method argument types used by the begin handler.</param>
    internal static void RegisterHandlerPair(Type integrationType, Type targetType, Type? returnType, Type declaringType, string beginMethodName, string endMethodName, params Type?[] argumentTypes)
    {
        Register(CreateBeginKey(integrationType, targetType, argumentTypes), new CallTargetAotHandlerRegistration(ResolveMethod(declaringType, beginMethodName)));
        Register(CreateEndKey(integrationType, targetType, returnType), new CallTargetAotHandlerRegistration(ResolveMethod(declaringType, endMethodName)));
    }

    /// <summary>
    /// Registers the generated slow-begin and end handlers for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The optional target return type used by non-void end handlers.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="beginMethodName">The generated slow-begin adapter method name.</param>
    /// <param name="endMethodName">The generated end adapter method name.</param>
    internal static void RegisterSlowHandlerPair(Type integrationType, Type targetType, Type? returnType, Type declaringType, string beginMethodName, string endMethodName)
    {
        Register(CallTargetAotHandlerKey.CreateBeginSlow(integrationType.TypeHandle, targetType.TypeHandle), new CallTargetAotHandlerRegistration(ResolveMethod(declaringType, beginMethodName)));
        Register(CreateEndKey(integrationType, targetType, returnType), new CallTargetAotHandlerRegistration(ResolveMethod(declaringType, endMethodName)));
    }

    /// <summary>
    /// Resolves a typed begin delegate for the supplied integration, target type, and optional argument types.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="argumentTypes">The optional target argument types.</param>
    /// <returns>The typed delegate.</returns>
    internal static TDelegate CreateBeginDelegate<TDelegate>(Type integrationType, Type targetType, params Type?[] argumentTypes)
        where TDelegate : Delegate
    {
        return CreateDelegate<TDelegate>(CreateBeginKey(integrationType, targetType, argumentTypes), integrationType, targetType);
    }

    /// <summary>
    /// Resolves a typed slow-begin delegate for the supplied integration and target type pair.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <returns>The typed delegate.</returns>
    internal static TDelegate CreateSlowBeginDelegate<TDelegate>(Type integrationType, Type targetType)
        where TDelegate : Delegate
    {
        return CreateDelegate<TDelegate>(CallTargetAotHandlerKey.CreateBeginSlow(integrationType.TypeHandle, targetType.TypeHandle), integrationType, targetType);
    }

    /// <summary>
    /// Resolves a typed void-end delegate for the supplied integration and target type pair.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <returns>The typed delegate.</returns>
    internal static TDelegate CreateEndDelegate<TDelegate>(Type integrationType, Type targetType)
        where TDelegate : Delegate
    {
        return CreateDelegate<TDelegate>(CallTargetAotHandlerKey.CreateEndVoid(integrationType.TypeHandle, targetType.TypeHandle), integrationType, targetType);
    }

    /// <summary>
    /// Resolves a typed value-end delegate for the supplied integration, target type, and return type.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="returnType">The target return type.</param>
    /// <returns>The typed delegate.</returns>
    internal static TDelegate CreateEndDelegate<TDelegate>(Type integrationType, Type targetType, Type returnType)
        where TDelegate : Delegate
    {
        return CreateDelegate<TDelegate>(CallTargetAotHandlerKey.CreateEndReturn(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle), integrationType, targetType);
    }

    /// <summary>
    /// Resolves the stored end-handler registration for a value-returning integration binding.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="returnType">The target return type.</param>
    /// <returns>The stored registration.</returns>
    internal static CallTargetAotHandlerRegistration GetEndRegistration(Type integrationType, Type targetType, Type returnType)
    {
        return GetRegistration(CallTargetAotHandlerKey.CreateEndReturn(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle), integrationType, targetType, returnType);
    }

    /// <summary>
    /// Registers a generated async-end continuation handler for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="resultType">The optional async result type used by Task{TResult} and ValueTask{TResult} continuations.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="methodName">The generated async adapter method name, when one exists.</param>
    /// <param name="preserveContext">Whether the callback must preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback itself returns a task.</param>
    internal static void RegisterAsyncHandler(Type integrationType, Type targetType, Type? resultType, Type declaringType, string? methodName, bool preserveContext, bool isAsyncCallback)
    {
        var key = resultType is null
                      ? CallTargetAotHandlerKey.CreateAsyncEndObject(integrationType.TypeHandle, targetType.TypeHandle)
                      : CallTargetAotHandlerKey.CreateAsyncEndResult(integrationType.TypeHandle, targetType.TypeHandle, resultType.TypeHandle);
        var method = string.IsNullOrWhiteSpace(methodName) ? null : ResolveMethod(declaringType, methodName!);
        if (method is null)
        {
            EnsureSingleRegistryAssemblyPerProcess(declaringType);
        }

        Register(key, new CallTargetAotHandlerRegistration(method, method is not null, preserveContext, isAsyncCallback));
    }

    /// <summary>
    /// Registers a generated typed task-return continuation wrapper for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="methodName">The generated continuation wrapper method name.</param>
    internal static void RegisterAsyncTaskResultContinuation(Type integrationType, Type targetType, Type returnType, Type declaringType, string methodName)
    {
        Register(
            CallTargetAotHandlerKey.CreateAsyncReturnTaskResult(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle),
            new CallTargetAotHandlerRegistration(ResolveMethod(declaringType, methodName)));
    }

    /// <summary>
    /// Registers a generated typed task-return continuation wrapper from a build-time method handle so the method is
    /// explicitly rooted in NativeAOT.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <param name="methodHandle">The generated continuation wrapper method handle.</param>
    internal static void RegisterAsyncTaskResultContinuation(Type integrationType, Type targetType, Type returnType, RuntimeMethodHandle methodHandle)
    {
        Register(
            CallTargetAotHandlerKey.CreateAsyncReturnTaskResult(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle),
            new CallTargetAotHandlerRegistration((MethodInfo?)MethodBase.GetMethodFromHandle(methodHandle)));
    }

    /// <summary>
    /// Resolves an async-end continuation registration for Task or ValueTask target methods without a typed result.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <returns>The resolved registration.</returns>
    internal static CallTargetAotHandlerRegistration GetAsyncObjectRegistration(Type integrationType, Type targetType)
    {
        return GetRegistration(CallTargetAotHandlerKey.CreateAsyncEndObject(integrationType.TypeHandle, targetType.TypeHandle), integrationType, targetType, null);
    }

    /// <summary>
    /// Resolves an async-end continuation registration for Task{TResult} or ValueTask{TResult} target methods.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="resultType">The async result type.</param>
    /// <returns>The resolved registration.</returns>
    internal static CallTargetAotHandlerRegistration GetAsyncResultRegistration(Type integrationType, Type targetType, Type resultType)
    {
        return GetRegistration(CallTargetAotHandlerKey.CreateAsyncEndResult(integrationType.TypeHandle, targetType.TypeHandle, resultType.TypeHandle), integrationType, targetType, resultType);
    }

    /// <summary>
    /// Resolves a typed task-return continuation delegate for Task{TResult} target methods in AOT mode.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <returns>The typed continuation delegate.</returns>
    internal static TDelegate CreateAsyncTaskResultContinuationDelegate<TDelegate>(Type integrationType, Type targetType, Type returnType)
        where TDelegate : Delegate
    {
        var key = CallTargetAotHandlerKey.CreateAsyncReturnTaskResult(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle);
        var registration = GetRegistration(key, integrationType, targetType, returnType);
        if (!registration.HasHandler || registration.Method is null)
        {
            throw new CallTargetAotMissingHandlerRegistrationException($"No CallTarget AOT registration was found for integration '{integrationType.FullName}' and target '{targetType.FullName}'.");
        }

        return (TDelegate)registration.Method.CreateDelegate(typeof(TDelegate));
    }

    /// <summary>
    /// Resolves the generated typed task-result continuation registration for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The instrumented target type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <returns>The stored registration.</returns>
    internal static CallTargetAotHandlerRegistration GetAsyncTaskResultContinuationRegistration(Type integrationType, Type targetType, Type returnType)
    {
        return GetRegistration(CallTargetAotHandlerKey.CreateAsyncReturnTaskResult(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle), integrationType, targetType, returnType);
    }

    /// <summary>
    /// Removes all registered AOT handlers for test isolation.
    /// </summary>
    internal static void ResetForTests()
    {
        Registrations.Clear();
        _registryType = null;
        _registeredRegistryAssemblyIdentity = null;
        _validatedRegistryAssemblyIdentity = null;
        Volatile.Write(ref _registryInitialized, 0);
    }

    /// <summary>
    /// Adds or validates a handler registration entry.
    /// </summary>
    /// <param name="key">The key that identifies the generated adapter.</param>
    /// <param name="registration">The generated adapter registration.</param>
    private static void Register(CallTargetAotHandlerKey key, CallTargetAotHandlerRegistration registration)
    {
        if (registration.Method is not null)
        {
            EnsureSingleRegistryAssemblyPerProcess(registration.Method);
        }

        if (Registrations.TryAdd(key, registration))
        {
            return;
        }

        var existing = Registrations[key];
        if (existing.HasHandler != registration.HasHandler ||
            existing.PreserveContext != registration.PreserveContext ||
            existing.IsAsyncCallback != registration.IsAsyncCallback ||
            !string.Equals(existing.Method?.Name, registration.Method?.Name, StringComparison.Ordinal) ||
            !string.Equals(existing.Method?.DeclaringType?.AssemblyQualifiedName, registration.Method?.DeclaringType?.AssemblyQualifiedName, StringComparison.Ordinal))
        {
            var existingMethodName = existing.Method?.DeclaringType is null ? existing.Method?.Name ?? "<none>" : $"{existing.Method.DeclaringType.FullName}.{existing.Method.Name}";
            var incomingMethodName = registration.Method?.DeclaringType is null ? registration.Method?.Name ?? "<none>" : $"{registration.Method.DeclaringType.FullName}.{registration.Method.Name}";
            CallTargetAotRegistrationConflictException.ThrowConflict(
                $"Binding '{key.Kind}' already points at '{existingMethodName}' and cannot be replaced with '{incomingMethodName}'.");
        }
    }

    /// <summary>
    /// Resolves a generated adapter method by declaring type and method name.
    /// </summary>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="methodName">The generated adapter method name.</param>
    /// <returns>The resolved method info.</returns>
    private static MethodInfo ResolveMethod(Type declaringType, string methodName)
    {
        return declaringType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"The generated CallTarget AOT method '{methodName}' could not be resolved on '{declaringType.AssemblyQualifiedName}'.");
    }

    /// <summary>
    /// Resolves a typed delegate from the stored registration and throws a clear missing-registration error when it is absent.
    /// </summary>
    /// <typeparam name="TDelegate">The typed delegate to materialize.</typeparam>
    /// <param name="key">The concrete handler key.</param>
    /// <param name="integrationType">The integration type used in the key.</param>
    /// <param name="targetType">The target type used in the key.</param>
    /// <returns>The typed delegate.</returns>
    private static TDelegate CreateDelegate<TDelegate>(CallTargetAotHandlerKey key, Type integrationType, Type targetType)
        where TDelegate : Delegate
    {
        var registration = GetRegistration(key, integrationType, targetType, null);
        if (!registration.HasHandler || registration.Method is null)
        {
            throw new CallTargetAotMissingHandlerRegistrationException($"No CallTarget AOT registration was found for integration '{integrationType.FullName}' and target '{targetType.FullName}'.");
        }

        return (TDelegate)registration.Method.CreateDelegate(typeof(TDelegate));
    }

    /// <summary>
    /// Resolves a stored handler registration and throws a clear missing-registration error when it is absent.
    /// </summary>
    /// <param name="key">The concrete handler key.</param>
    /// <param name="integrationType">The integration type used in the key.</param>
    /// <param name="targetType">The target type used in the key.</param>
    /// <param name="resultType">The optional async result type used for diagnostics.</param>
    /// <returns>The stored registration.</returns>
    private static CallTargetAotHandlerRegistration GetRegistration(CallTargetAotHandlerKey key, Type integrationType, Type targetType, Type? resultType)
    {
        if (Registrations.TryGetValue(key, out var registration))
        {
            return registration;
        }

        var resultSuffix = resultType is null ? string.Empty : $" and result '{resultType.FullName}'";
        throw new CallTargetAotMissingHandlerRegistrationException($"No CallTarget AOT registration was found for integration '{integrationType.FullName}', target '{targetType.FullName}'{resultSuffix}.");
    }

    /// <summary>
    /// Builds the begin-handler key for the supplied integration, target type, and optional argument types.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="argumentTypes">The optional target argument types.</param>
    /// <returns>The begin-handler key.</returns>
    private static CallTargetAotHandlerKey CreateBeginKey(Type integrationType, Type targetType, Type?[] argumentTypes)
    {
        var filteredArgumentTypes = argumentTypes.Where(static argumentType => argumentType is not null).Cast<Type>().ToArray();
        var beginKind = filteredArgumentTypes.Length switch
        {
            0 => CallTargetAotHandlerKind.Begin0,
            1 => CallTargetAotHandlerKind.Begin1,
            2 => CallTargetAotHandlerKind.Begin2,
            3 => CallTargetAotHandlerKind.Begin3,
            4 => CallTargetAotHandlerKind.Begin4,
            5 => CallTargetAotHandlerKind.Begin5,
            6 => CallTargetAotHandlerKind.Begin6,
            7 => CallTargetAotHandlerKind.Begin7,
            8 => CallTargetAotHandlerKind.Begin8,
            _ => throw new InvalidOperationException($"Unsupported CallTarget AOT begin arity '{filteredArgumentTypes.Length}'.")
        };

        return CallTargetAotHandlerKey.CreateBegin(
            beginKind,
            integrationType.TypeHandle,
            targetType.TypeHandle,
            filteredArgumentTypes.Select(static argumentType => argumentType.TypeHandle).ToArray());
    }

    /// <summary>
    /// Builds the end-handler key for the supplied integration, target type, and optional return type.
    /// </summary>
    /// <param name="integrationType">The integration type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="returnType">The optional target return type.</param>
    /// <returns>The end-handler key.</returns>
    private static CallTargetAotHandlerKey CreateEndKey(Type integrationType, Type targetType, Type? returnType)
    {
        return returnType is null
                   ? CallTargetAotHandlerKey.CreateEndVoid(integrationType.TypeHandle, targetType.TypeHandle)
                   : CallTargetAotHandlerKey.CreateEndReturn(integrationType.TypeHandle, targetType.TypeHandle, returnType.TypeHandle);
    }

    /// <summary>
    /// Enforces the single generated registry assembly rule using the supplied runtime type.
    /// </summary>
    /// <param name="registryType">A type defined in the generated registry assembly.</param>
    private static void EnsureSingleRegistryAssemblyPerProcess(Type registryType)
    {
        lock (RegistrationLock)
        {
            var incomingRegistryAssemblyIdentity = ResolveRegistryAssemblyIdentity(registryType);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
            {
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return;
            }

            if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                CallTargetAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }
    }

    /// <summary>
    /// Enforces the single generated registry assembly rule using the supplied generated method.
    /// </summary>
    /// <param name="method">A method emitted into the generated registry assembly.</param>
    private static void EnsureSingleRegistryAssemblyPerProcess(MethodInfo method)
    {
        lock (RegistrationLock)
        {
            var incomingRegistryAssemblyIdentity = ResolveRegistryAssemblyIdentity(method);
            var currentRegistryAssemblyIdentity = _registeredRegistryAssemblyIdentity ?? _validatedRegistryAssemblyIdentity;
            if (string.IsNullOrWhiteSpace(currentRegistryAssemblyIdentity))
            {
                _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
                return;
            }

            if (!string.Equals(currentRegistryAssemblyIdentity, incomingRegistryAssemblyIdentity, StringComparison.Ordinal))
            {
                CallTargetAotMultipleRegistryAssembliesException.Throw(currentRegistryAssemblyIdentity!, incomingRegistryAssemblyIdentity);
            }

            _registeredRegistryAssemblyIdentity = incomingRegistryAssemblyIdentity;
        }
    }

    /// <summary>
    /// Resolves the normalized identity of the registry assembly that owns the supplied runtime type.
    /// </summary>
    /// <param name="registryType">A type defined in the generated registry assembly.</param>
    /// <returns>The normalized registry assembly identity.</returns>
    private static string ResolveRegistryAssemblyIdentity(Type registryType)
    {
        var module = registryType.Module;
        var assembly = module.Assembly;
        var assemblyFullName = assembly.FullName;
        if (string.IsNullOrWhiteSpace(assemblyFullName))
        {
            var assemblyName = assembly.GetName();
            assemblyFullName = assemblyName.FullName ?? assemblyName.Name ?? "unknown";
        }

        return NormalizeRegistryAssemblyIdentity(assemblyFullName, module.ModuleVersionId.ToString("D"));
    }

    /// <summary>
    /// Resolves the normalized identity of the registry assembly that owns the supplied generated method.
    /// </summary>
    /// <param name="method">A method emitted into the generated registry assembly.</param>
    /// <returns>The normalized registry assembly identity.</returns>
    private static string ResolveRegistryAssemblyIdentity(MethodInfo method)
    {
        var module = method.Module;
        var assembly = module.Assembly;
        var assemblyFullName = assembly.FullName;
        if (string.IsNullOrWhiteSpace(assemblyFullName))
        {
            var assemblyName = assembly.GetName();
            assemblyFullName = assemblyName.FullName ?? assemblyName.Name ?? "unknown";
        }

        return NormalizeRegistryAssemblyIdentity(assemblyFullName, module.ModuleVersionId.ToString("D"));
    }

    /// <summary>
    /// Produces the deterministic assembly identity format used by CallTarget NativeAOT registry validation.
    /// </summary>
    /// <param name="assemblyNameOrFullName">The assembly full name or simple name.</param>
    /// <param name="moduleMvid">The module MVID associated with the registry assembly.</param>
    /// <returns>The normalized registry assembly identity string.</returns>
    private static string NormalizeRegistryAssemblyIdentity(string assemblyNameOrFullName, string moduleMvid)
    {
        var normalizedMvid = Guid.TryParse(moduleMvid, out var parsedMvid) ? parsedMvid.ToString("D") : moduleMvid;
        return $"{assemblyNameOrFullName}; MVID={normalizedMvid}";
    }
}
