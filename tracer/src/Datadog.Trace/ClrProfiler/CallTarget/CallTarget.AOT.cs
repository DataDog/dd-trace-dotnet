// <copyright file="CallTarget.AOT.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Exposes the NativeAOT CallTarget runtime mode and generated handler registration surface.
/// </summary>
#pragma warning disable SA1649
public static class CallTargetAot
{
    private const string ManualRegistrationObsoleteMessage = "Reserved for generated NativeAOT registry bootstrap. Manual registration and registry mixing are unsupported.";
    private const int RuntimeModeStateUninitialized = 0;
    private const int RuntimeModeStateAot = 2;

    private static int _runtimeModeState;

    /// <summary>
    /// Enables the NativeAOT CallTarget runtime path.
    /// </summary>
    public static void EnableAotMode()
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
    }

    /// <summary>
    /// Ensures the generated registry is initialized exactly once for the process and enables AOT mode.
    /// </summary>
    /// <param name="registryType">The generated registry bootstrap type.</param>
    /// <returns><see langword="true"/> when the caller should perform registration; otherwise <see langword="false"/>.</returns>
    public static bool TryInitializeGeneratedRegistry(Type registryType)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        return CallTargetAotEngine.TryInitializeGeneratedRegistry(registryType);
    }

    /// <summary>
    /// Validates an AOT-generated CallTarget registry contract against the currently loaded Datadog.Trace runtime.
    /// </summary>
    /// <param name="schemaVersion">The generated contract schema version.</param>
    /// <param name="datadogTraceAssemblyVersion">The Datadog.Trace assembly version used during generation.</param>
    /// <param name="datadogTraceAssemblyMvid">The Datadog.Trace module MVID used during generation.</param>
    /// <param name="registryAssemblyFullName">The generated registry assembly full name.</param>
    /// <param name="registryAssemblyMvid">The generated registry module MVID.</param>
    public static void ValidateAotRegistryContract(
        string schemaVersion,
        string datadogTraceAssemblyVersion,
        string datadogTraceAssemblyMvid,
        string registryAssemblyFullName,
        string registryAssemblyMvid)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.ValidateContract(
            new CallTargetAotContract(schemaVersion, datadogTraceAssemblyVersion, datadogTraceAssemblyMvid),
            new CallTargetAotAssemblyMetadata(registryAssemblyFullName, registryAssemblyMvid));
    }

    /// <summary>
    /// Registers the generated begin and end handler adapters for a concrete integration and target type pair.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The optional target return type used by non-void end handlers.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="beginMethodName">The generated begin adapter method name.</param>
    /// <param name="endMethodName">The generated end adapter method name.</param>
    /// <param name="arg1">The optional first target method argument type.</param>
    /// <param name="arg2">The optional second target method argument type.</param>
    /// <param name="arg3">The optional third target method argument type.</param>
    /// <param name="arg4">The optional fourth target method argument type.</param>
    /// <param name="arg5">The optional fifth target method argument type.</param>
    /// <param name="arg6">The optional sixth target method argument type.</param>
    /// <param name="arg7">The optional seventh target method argument type.</param>
    /// <param name="arg8">The optional eighth target method argument type.</param>
    [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
    public static void RegisterAotHandlerPair(
        Type integrationType,
        Type targetType,
        Type? returnType,
        Type declaringType,
        string beginMethodName,
        string endMethodName,
        Type? arg1 = null,
        Type? arg2 = null,
        Type? arg3 = null,
        Type? arg4 = null,
        Type? arg5 = null,
        Type? arg6 = null,
        Type? arg7 = null,
        Type? arg8 = null)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.RegisterHandlerPair(integrationType, targetType, returnType, declaringType, beginMethodName, endMethodName, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }

    /// <summary>
    /// Registers the generated slow-begin and end handler adapters for a concrete integration and target type pair.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The optional target return type used by non-void end handlers.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="beginMethodName">The generated slow-begin adapter method name.</param>
    /// <param name="endMethodName">The generated end adapter method name.</param>
    [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
    public static void RegisterAotSlowHandlerPair(
        Type integrationType,
        Type targetType,
        Type? returnType,
        Type declaringType,
        string beginMethodName,
        string endMethodName)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.RegisterSlowHandlerPair(integrationType, targetType, returnType, declaringType, beginMethodName, endMethodName);
    }

    /// <summary>
    /// Registers the generated async-end continuation adapter for a concrete integration binding.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="resultType">The optional async result type used by Task{TResult} and ValueTask{TResult} continuations.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="methodName">The generated async adapter method name, when one exists.</param>
    /// <param name="preserveContext">Whether the callback must preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback returns a task.</param>
    [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
    public static void RegisterAotAsyncHandler(
        Type integrationType,
        Type targetType,
        Type? resultType,
        Type declaringType,
        string? methodName,
        bool preserveContext,
        bool isAsyncCallback)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.RegisterAsyncHandler(integrationType, targetType, resultType, declaringType, methodName, preserveContext, isAsyncCallback);
    }

    /// <summary>
    /// Registers a generated typed task-return continuation wrapper for Task{TResult} target methods.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <param name="declaringType">The generated bootstrap declaring type.</param>
    /// <param name="methodName">The generated continuation wrapper method name.</param>
    [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
    public static void RegisterAotAsyncTaskResultContinuation(
        Type integrationType,
        Type targetType,
        Type returnType,
        Type declaringType,
        string methodName)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.RegisterAsyncTaskResultContinuation(integrationType, targetType, returnType, declaringType, methodName);
    }

    /// <summary>
    /// Registers a generated typed task-return continuation wrapper for Task{TResult} target methods using a rooted
    /// method handle emitted by the generated bootstrap.
    /// </summary>
    /// <param name="integrationType">The integration runtime type.</param>
    /// <param name="targetType">The instrumented target runtime type.</param>
    /// <param name="returnType">The concrete Task{TResult} return type.</param>
    /// <param name="methodHandle">The generated continuation wrapper method handle.</param>
    [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
    public static void RegisterAotAsyncTaskResultContinuation(
        Type integrationType,
        Type targetType,
        Type returnType,
        RuntimeMethodHandle methodHandle)
    {
        EnsureRuntimeModeIsInitialized(RuntimeModeStateAot);
        CallTargetAotEngine.RegisterAsyncTaskResultContinuation(integrationType, targetType, returnType, methodHandle);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the CallTarget runtime is using the AOT registration path.
    /// </summary>
    /// <returns><see langword="true"/> when AOT mode is active; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAotMode()
    {
        var currentState = Volatile.Read(ref _runtimeModeState);
        return currentState == RuntimeModeStateAot;
    }

    /// <summary>
    /// Resets the runtime mode and handler registrations for tests.
    /// </summary>
    internal static void ResetForTests()
    {
        CallTargetAotEngine.ResetForTests();
        Volatile.Write(ref _runtimeModeState, RuntimeModeStateUninitialized);
    }

    /// <summary>
    /// Initializes the runtime mode or validates that the requested mode matches the current mode.
    /// </summary>
    /// <param name="requestedState">The requested runtime mode constant.</param>
    private static void EnsureRuntimeModeIsInitialized(int requestedState)
    {
        var currentState = Interlocked.CompareExchange(ref _runtimeModeState, requestedState, RuntimeModeStateUninitialized);
        if (currentState == RuntimeModeStateUninitialized || currentState == requestedState)
        {
            return;
        }

        throw new InvalidOperationException("CallTarget runtime mode cannot be switched after it has been initialized.");
    }
}
#pragma warning restore SA1649
