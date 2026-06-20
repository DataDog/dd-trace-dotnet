// <copyright file="CallTargetAotInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Provides cached reflection helpers used by generated CallTarget AOT adapters for the small set of callback shapes
/// that are difficult to encode directly in emitted IL across runtime identities.
/// </summary>
internal static class CallTargetAotInvoker
{
    private static readonly ConcurrentDictionary<AsyncEndInvokerKey, MethodInfo?> AsyncEndMethodCache = new();

    private delegate TResult? SyncTaskResultContinuationMethod<TTarget, TResult>(TTarget? target, TResult? returnValue, Exception? exception, in CallTargetState state);

    private delegate Task<TResult?> AsyncTaskResultContinuationMethod<TTarget, TResult>(TTarget? target, TResult? returnValue, Exception? exception, in CallTargetState state);

    private delegate CallTargetReturn<Task<TResult?>> EndTaskResultMethod<TTarget, TResult>(TTarget? target, Task<TResult?>? returnValue, Exception? exception, in CallTargetState state);

    /// <summary>
    /// Extracts the raw return value from a <see cref="CallTargetReturn{T}"/> wrapper through a normal helper method
    /// so the generated AOT registry does not depend on direct generic member calls on the ref struct.
    /// </summary>
    /// <typeparam name="T">The wrapped return type.</typeparam>
    /// <param name="returnValue">The wrapped calltarget return value.</param>
    /// <returns>The extracted raw return value.</returns>
    internal static T? GetCallTargetReturnValue<T>(CallTargetReturn<T> returnValue)
    {
        return returnValue.GetReturnValue();
    }

    /// <summary>
    /// Extracts the real target return value from a <see cref="CallTargetReturn{T}"/> that wraps a generated DuckType
    /// proxy returned by an AOT-generated end callback.
    /// </summary>
    /// <typeparam name="TFrom">The generated DuckType proxy type.</typeparam>
    /// <typeparam name="TTo">The original target method return type.</typeparam>
    /// <param name="returnValue">The wrapped duck-typed calltarget return value.</param>
    /// <returns>The unwrapped target method return value.</returns>
    internal static TTo? GetDuckTypeCallTargetReturnValue<TFrom, TTo>(CallTargetReturn<TFrom> returnValue)
        where TFrom : IDuckType
    {
        return UnwrapReturnValue<TFrom, TTo>(returnValue.GetReturnValue());
    }

    /// <summary>
    /// Unwraps a generated DuckType proxy into the original target runtime type.
    /// </summary>
    /// <typeparam name="TFrom">The generated DuckType proxy type.</typeparam>
    /// <typeparam name="TTo">The original target runtime type.</typeparam>
    /// <param name="returnValue">The generated DuckType proxy value.</param>
    /// <returns>The unwrapped target runtime value.</returns>
    internal static TTo? UnwrapReturnValue<TFrom, TTo>(TFrom? returnValue)
        where TFrom : IDuckType
    {
#pragma warning disable DDDUCK001 // Checking IDuckType for null
        if (returnValue is not null)
        {
            return returnValue.GetInternalDuckTypedInstance<TTo>();
        }
#pragma warning restore DDDUCK001 // Checking IDuckType for null

        return default;
    }

    /// <summary>
    /// Awaits a task that returns a generated DuckType proxy and unwraps the completed value into the original target
    /// runtime type.
    /// </summary>
    /// <typeparam name="TFrom">The generated DuckType proxy type.</typeparam>
    /// <typeparam name="TTo">The original target runtime type.</typeparam>
    /// <param name="returnValue">The task that returns the generated DuckType proxy.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <returns>The unwrapped target runtime value.</returns>
    internal static async Task<TTo?> UnwrapTaskReturnValue<TFrom, TTo>(Task<TFrom?>? returnValue, bool preserveContext)
        where TFrom : IDuckType
    {
        if (returnValue is null)
        {
            return default;
        }

        return UnwrapReturnValue<TFrom, TTo>(await returnValue.ConfigureAwait(preserveContext));
    }

    /// <summary>
    /// Invokes a typed async <c>OnAsyncMethodEnd</c> integration callback using a cached closed generic method lookup.
    /// </summary>
    /// <param name="integrationType">The integration type that declares the callback.</param>
    /// <param name="targetType">The concrete instrumented target type.</param>
    /// <param name="resultType">The async continuation result type.</param>
    /// <param name="instance">The target instance passed to the callback.</param>
    /// <param name="returnValue">The completed async result value.</param>
    /// <param name="exception">The exception thrown by the target task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <returns>The callback result boxed as an object.</returns>
    internal static object? InvokeAsyncMethodEnd(Type integrationType, Type targetType, Type resultType, object? instance, object? returnValue, Exception? exception, CallTargetState state)
    {
        var method = AsyncEndMethodCache.GetOrAdd(new AsyncEndInvokerKey(integrationType, targetType, resultType), static key => ResolveAsyncEndMethod(key.IntegrationType, key.TargetType, key.ResultType));
        if (method is null)
        {
            throw new InvalidOperationException($"The async CallTarget callback '{integrationType.FullName}.OnAsyncMethodEnd<{targetType.FullName}, {resultType.FullName}>' could not be resolved.");
        }

        return method.Invoke(null, [instance, returnValue, exception, state]);
    }

    /// <summary>
    /// Wraps a typed Task{TResult} return value with the generated AOT async-end callback without constructing any
    /// runtime generic helper types inside the published NativeAOT application.
    /// </summary>
    /// <typeparam name="TIntegration">The integration type used for exception logging.</typeparam>
    /// <typeparam name="TTarget">The instrumented target type.</typeparam>
    /// <typeparam name="TResult">The completed task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="previousTask">The original task returned by the instrumented method.</param>
    /// <param name="exception">The exception thrown by the target method before returning the task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <param name="callbackMethod">The generated AOT async-end callback method.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback itself returns a task.</param>
    /// <returns>The wrapped task returned to the instrumented application.</returns>
    internal static Task<TResult?> ContinueTaskResult<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        MethodInfo callbackMethod,
        bool preserveContext,
        bool isAsyncCallback)
    {
        return isAsyncCallback
                   ? ContinueTaskResultAsync<TIntegration, TTarget, TResult>(
                       instance,
                       previousTask,
                       exception,
                       state,
                       (AsyncTaskResultContinuationMethod<TTarget, TResult>)callbackMethod.CreateDelegate(typeof(AsyncTaskResultContinuationMethod<TTarget, TResult>)),
                       preserveContext)
                   : ContinueTaskResultSync<TIntegration, TTarget, TResult>(
                       instance,
                       previousTask,
                       exception,
                       state,
                       (SyncTaskResultContinuationMethod<TTarget, TResult>)callbackMethod.CreateDelegate(typeof(SyncTaskResultContinuationMethod<TTarget, TResult>)),
                       preserveContext);
    }

    /// <summary>
    /// Wraps a typed Task{TResult} return value by resolving a generated async-end callback from a method handle
    /// produced at build time, avoiding any runtime generic method construction in the NativeAOT application.
    /// </summary>
    /// <typeparam name="TIntegration">The integration type used for exception logging.</typeparam>
    /// <typeparam name="TTarget">The instrumented target type.</typeparam>
    /// <typeparam name="TResult">The completed task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="previousTask">The original task returned by the instrumented method.</param>
    /// <param name="exception">The exception thrown by the target method before returning the task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <param name="callbackMethodHandle">The build-time generated callback method handle.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback itself returns a task.</param>
    /// <returns>The wrapped task returned to the instrumented application.</returns>
    internal static Task<TResult?> ContinueTaskResultFromMethodHandle<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        RuntimeMethodHandle callbackMethodHandle,
        bool preserveContext,
        bool isAsyncCallback)
    {
        var callbackMethod = (MethodInfo?)MethodBase.GetMethodFromHandle(callbackMethodHandle);
        if (callbackMethod is null)
        {
            throw new InvalidOperationException($"The generated CallTarget AOT callback handle for integration '{typeof(TIntegration).FullName}' could not be resolved.");
        }

        return ContinueTaskResult<TIntegration, TTarget, TResult>(instance, previousTask, exception, state, callbackMethod, preserveContext, isAsyncCallback);
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Wraps a typed ValueTask{TResult} return value by resolving a generated async-end callback from a method handle
    /// produced at build time, avoiding any runtime generic method construction in the NativeAOT application.
    /// </summary>
    /// <typeparam name="TIntegration">The integration type used for exception logging.</typeparam>
    /// <typeparam name="TTarget">The instrumented target type.</typeparam>
    /// <typeparam name="TResult">The completed value-task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="previousValueTask">The original value task returned by the instrumented method.</param>
    /// <param name="exception">The exception thrown by the target method before returning the value task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <param name="callbackMethodHandle">The build-time generated callback method handle.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback itself returns a task.</param>
    /// <returns>The wrapped value task returned to the instrumented application.</returns>
    internal static ValueTask<TResult?> ContinueValueTaskResultFromMethodHandle<TIntegration, TTarget, TResult>(
        TTarget? instance,
        ValueTask<TResult> previousValueTask,
        Exception? exception,
        CallTargetState state,
        RuntimeMethodHandle callbackMethodHandle,
        bool preserveContext,
        bool isAsyncCallback)
    {
        var callbackMethod = (MethodInfo?)MethodBase.GetMethodFromHandle(callbackMethodHandle);
        if (callbackMethod is null)
        {
            throw new InvalidOperationException($"The generated CallTarget AOT callback handle for integration '{typeof(TIntegration).FullName}' could not be resolved.");
        }

        return isAsyncCallback
                   ? ContinueValueTaskResultAsync<TIntegration, TTarget, TResult>(
                       instance,
                       previousValueTask,
                       exception,
                       state,
                       (AsyncTaskResultContinuationMethod<TTarget, TResult>)callbackMethod.CreateDelegate(typeof(AsyncTaskResultContinuationMethod<TTarget, TResult>)),
                       preserveContext)
                   : ContinueValueTaskResultSync<TIntegration, TTarget, TResult>(
                       instance,
                       previousValueTask,
                       exception,
                       state,
                       (SyncTaskResultContinuationMethod<TTarget, TResult>)callbackMethod.CreateDelegate(typeof(SyncTaskResultContinuationMethod<TTarget, TResult>)),
                       preserveContext);
    }
#endif

    /// <summary>
    /// Invokes a closed <c>OnMethodEnd</c> method handle that returns <see cref="CallTargetReturn{T}"/> for
    /// <see cref="Task{TResult}"/>, unwraps the updated task inside <c>Datadog.Trace</c>, and then chains the
    /// generated async-end callback handle without requiring the generated registry to manipulate the byref-like
    /// wrapper directly.
    /// </summary>
    /// <typeparam name="TIntegration">The integration type used for diagnostics.</typeparam>
    /// <typeparam name="TTarget">The instrumented target type.</typeparam>
    /// <typeparam name="TResult">The task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="previousTask">The task returned by the target method.</param>
    /// <param name="exception">The exception thrown by the target method before returning the task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <param name="endMethodHandle">The closed generated or integration end-method handle.</param>
    /// <param name="callbackMethodHandle">The generated async-end callback handle.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback itself returns a task.</param>
    /// <returns>The wrapped task returned to the instrumented application.</returns>
    internal static Task<TResult?> InvokeEndTaskResultAndContinueFromMethodHandles<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        RuntimeMethodHandle endMethodHandle,
        RuntimeMethodHandle callbackMethodHandle,
        bool preserveContext,
        bool isAsyncCallback)
    {
        var endMethod = (MethodInfo?)MethodBase.GetMethodFromHandle(endMethodHandle);
        if (endMethod is null)
        {
            throw new InvalidOperationException($"The generated CallTarget AOT end callback handle for integration '{typeof(TIntegration).FullName}' could not be resolved.");
        }

        var endDelegate = (EndTaskResultMethod<TTarget, TResult>)endMethod.CreateDelegate(typeof(EndTaskResultMethod<TTarget, TResult>));
        var updatedTask = endDelegate(instance, previousTask, exception, in state).GetReturnValue();
        return ContinueTaskResultFromMethodHandle<TIntegration, TTarget, TResult>(instance, updatedTask, exception, state, callbackMethodHandle, preserveContext, isAsyncCallback);
    }

    /// <summary>
    /// Resolves the typed <c>OnMethodEnd</c> and <c>OnAsyncMethodEnd</c> callbacks through reflection inside
    /// <c>Datadog.Trace</c>, avoiding build-time method-handle metadata for the typed task-result path.
    /// </summary>
    internal static Task<TResult?> InvokeEndTaskResultAndContinueReflected<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        bool preserveContext,
        bool isAsyncCallback)
    {
        var endMethod = typeof(TIntegration)
                       .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                       .SingleOrDefault(method =>
                            string.Equals(method.Name, "OnMethodEnd", StringComparison.Ordinal) &&
                            method.GetGenericArguments().Length == 2 &&
                            method.GetParameters().Length == 4)
                       ?.MakeGenericMethod(typeof(TTarget), typeof(Task<TResult>));
        if (endMethod is null)
        {
            throw new InvalidOperationException($"The AOT end callback '{typeof(TIntegration).FullName}.OnMethodEnd<{typeof(TTarget).FullName}, {typeof(Task<TResult>).FullName}>' could not be resolved.");
        }

        var endDelegate = (EndTaskResultMethod<TTarget, TResult>)endMethod.CreateDelegate(typeof(EndTaskResultMethod<TTarget, TResult>));
        var updatedTask = endDelegate(instance, previousTask, exception, in state).GetReturnValue();
        return ContinueTaskResultReflected<TIntegration, TTarget, TResult>(instance, updatedTask, exception, state, preserveContext, isAsyncCallback);
    }

    /// <summary>
    /// Wraps a typed Task{TResult} return value by invoking a closed integration async-end method resolved from a
    /// build-time method handle, avoiding both runtime generic construction and generated delegate wrappers.
    /// </summary>
    /// <typeparam name="TIntegration">The integration type used for exception logging.</typeparam>
    /// <typeparam name="TTarget">The instrumented target type.</typeparam>
    /// <typeparam name="TResult">The completed task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="previousTask">The original task returned by the instrumented method.</param>
    /// <param name="exception">The exception thrown by the target method before returning the task, if any.</param>
    /// <param name="state">The captured CallTarget state.</param>
    /// <param name="callbackMethodHandle">The build-time generated closed integration method handle.</param>
    /// <param name="preserveContext">Whether the continuation should preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the integration callback itself returns a task.</param>
    /// <returns>The wrapped task returned to the instrumented application.</returns>
    internal static Task<TResult?> ContinueTaskResultFromIntegrationMethodHandle<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        RuntimeMethodHandle callbackMethodHandle,
        bool preserveContext,
        bool isAsyncCallback)
    {
        var callbackMethod = (MethodInfo?)MethodBase.GetMethodFromHandle(callbackMethodHandle);
        if (callbackMethod is null)
        {
            throw new InvalidOperationException($"The generated CallTarget AOT integration callback handle for integration '{typeof(TIntegration).FullName}' could not be resolved.");
        }

        return isAsyncCallback
                   ? ContinueTaskResultAsyncReflectedMethod<TIntegration, TTarget, TResult>(instance, previousTask, exception, state, callbackMethod, preserveContext)
                   : ContinueTaskResultSyncReflectedMethod<TIntegration, TTarget, TResult>(instance, previousTask, exception, state, callbackMethod, preserveContext);
    }

    /// <summary>
    /// Wraps a typed Task{TResult} return value by invoking the integration callback through the reflection-based
    /// async-end helper directly, avoiding an extra delegate hop into the generated registry assembly.
    /// </summary>
    internal static Task<TResult?> ContinueTaskResultReflected<TIntegration, TTarget, TResult>(
        TTarget? instance,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        bool preserveContext,
        bool isAsyncCallback)
    {
        return isAsyncCallback
                   ? ContinueTaskResultAsyncReflected<TIntegration, TTarget, TResult>(instance, previousTask, exception, state, preserveContext)
                   : ContinueTaskResultSyncReflected<TIntegration, TTarget, TResult>(instance, previousTask, exception, state, preserveContext);
    }

    /// <summary>
    /// Resolves the closed generic async callback method for the supplied integration, target, and result types.
    /// </summary>
    /// <param name="integrationType">The integration type that declares the callback.</param>
    /// <param name="targetType">The concrete instrumented target type.</param>
    /// <param name="resultType">The async continuation result type.</param>
    /// <returns>The closed generic method when one exists; otherwise <see langword="null"/>.</returns>
    private static MethodInfo? ResolveAsyncEndMethod(Type integrationType, Type targetType, Type resultType)
    {
        var candidate = integrationType
                       .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                       .SingleOrDefault(method =>
                            string.Equals(method.Name, "OnAsyncMethodEnd", StringComparison.Ordinal) &&
                            method.GetGenericArguments().Length == 2 &&
                            method.GetParameters().Length == 4);
        return candidate?.MakeGenericMethod(targetType, resultType);
    }

    /// <summary>
    /// Executes a synchronous typed async-end callback through the reflection helper after the original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultSyncReflected<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        bool preserveContext)
    {
        if (exception != null || previousTask is null)
        {
            return (TResult?)InvokeAsyncMethodEnd(typeof(TIntegration), typeof(TTarget), typeof(TResult), target, default(TResult), exception, state);
        }

        if (!previousTask.IsCompleted)
        {
            await new NoThrowAwaiter(previousTask, preserveContext);
        }

        TResult? taskResult = default;
        Exception? taskException = exception;
        if (previousTask.Status == TaskStatus.RanToCompletion)
        {
            taskResult = previousTask.Result;
        }
        else if (previousTask.Status == TaskStatus.Faulted)
        {
            taskException ??= previousTask.Exception?.GetBaseException();
        }
        else if (previousTask.Status == TaskStatus.Canceled)
        {
            try
            {
                await previousTask.ConfigureAwait(preserveContext);
            }
            catch (Exception ex)
            {
                taskException ??= ex;
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            continuationResult = (TResult?)InvokeAsyncMethodEnd(typeof(TIntegration), typeof(TTarget), typeof(TResult), target, taskResult, taskException, state);
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (taskException != null)
        {
            ExceptionDispatchInfo.Capture(taskException).Throw();
        }

        return continuationResult;
    }

    /// <summary>
    /// Executes a synchronous typed async-end callback through a pre-resolved closed integration method after the
    /// original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultSyncReflectedMethod<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        MethodInfo callbackMethod,
        bool preserveContext)
    {
        if (exception != null || previousTask is null)
        {
            return (TResult?)callbackMethod.Invoke(null, [target, default(TResult), exception, state]);
        }

        if (!previousTask.IsCompleted)
        {
            await new NoThrowAwaiter(previousTask, preserveContext);
        }

        TResult? taskResult = default;
        Exception? taskException = exception;
        if (previousTask.Status == TaskStatus.RanToCompletion)
        {
            taskResult = previousTask.Result;
        }
        else if (previousTask.Status == TaskStatus.Faulted)
        {
            taskException ??= previousTask.Exception?.GetBaseException();
        }
        else if (previousTask.Status == TaskStatus.Canceled)
        {
            try
            {
                await previousTask.ConfigureAwait(preserveContext);
            }
            catch (Exception ex)
            {
                taskException ??= ex;
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            continuationResult = (TResult?)callbackMethod.Invoke(null, [target, taskResult, taskException, state]);
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (taskException != null)
        {
            ExceptionDispatchInfo.Capture(taskException).Throw();
        }

        return continuationResult;
    }

    /// <summary>
    /// Executes an asynchronous typed async-end callback through the reflection helper after the original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultAsyncReflected<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        bool preserveContext)
    {
        TResult? taskResult = default;
        if (previousTask is not null)
        {
            if (!previousTask.IsCompleted)
            {
                await new NoThrowAwaiter(previousTask, preserveContext);
            }

            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                taskResult = previousTask.Result;
            }
            else if (previousTask.Status == TaskStatus.Faulted)
            {
                exception ??= previousTask.Exception?.GetBaseException();
            }
            else if (previousTask.Status == TaskStatus.Canceled)
            {
                try
                {
                    await previousTask.ConfigureAwait(preserveContext);
                }
                catch (Exception ex)
                {
                    exception ??= ex;
                }
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            var reflectedTask = (Task<TResult?>?)InvokeAsyncMethodEnd(typeof(TIntegration), typeof(TTarget), typeof(TResult), target, taskResult, exception, state);
            if (reflectedTask is not null)
            {
                continuationResult = await reflectedTask.ConfigureAwait(preserveContext);
            }
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return continuationResult;
    }

    /// <summary>
    /// Executes an asynchronous typed async-end callback through a pre-resolved closed integration method after the
    /// original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultAsyncReflectedMethod<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        MethodInfo callbackMethod,
        bool preserveContext)
    {
        TResult? taskResult = default;
        if (previousTask is not null)
        {
            if (!previousTask.IsCompleted)
            {
                await new NoThrowAwaiter(previousTask, preserveContext);
            }

            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                taskResult = previousTask.Result;
            }
            else if (previousTask.Status == TaskStatus.Faulted)
            {
                exception ??= previousTask.Exception?.GetBaseException();
            }
            else if (previousTask.Status == TaskStatus.Canceled)
            {
                try
                {
                    await previousTask.ConfigureAwait(preserveContext);
                }
                catch (Exception ex)
                {
                    exception ??= ex;
                }
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            if (callbackMethod.Invoke(null, [target, taskResult, exception, state]) is Task<TResult?> reflectedTask)
            {
                continuationResult = await reflectedTask.ConfigureAwait(preserveContext);
            }
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return continuationResult;
    }

    /// <summary>
    /// Executes a synchronous generated typed task-result callback after the original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultSync<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        SyncTaskResultContinuationMethod<TTarget, TResult> continuation,
        bool preserveContext)
    {
        if (exception != null || previousTask is null)
        {
            return continuation(target, default, exception, in state);
        }

        if (!previousTask.IsCompleted)
        {
            await new NoThrowAwaiter(previousTask, preserveContext);
        }

        TResult? taskResult = default;
        Exception? taskException = exception;
        if (previousTask.Status == TaskStatus.RanToCompletion)
        {
            taskResult = previousTask.Result;
        }
        else if (previousTask.Status == TaskStatus.Faulted)
        {
            taskException ??= previousTask.Exception?.GetBaseException();
        }
        else if (previousTask.Status == TaskStatus.Canceled)
        {
            try
            {
                await previousTask.ConfigureAwait(preserveContext);
            }
            catch (Exception ex)
            {
                taskException ??= ex;
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            continuationResult = continuation(target, taskResult, taskException, in state);
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (taskException != null)
        {
            ExceptionDispatchInfo.Capture(taskException).Throw();
        }

        return continuationResult;
    }

    /// <summary>
    /// Executes an asynchronous generated typed task-result callback after the original task completes.
    /// </summary>
    private static async Task<TResult?> ContinueTaskResultAsync<TIntegration, TTarget, TResult>(
        TTarget? target,
        Task<TResult?>? previousTask,
        Exception? exception,
        CallTargetState state,
        AsyncTaskResultContinuationMethod<TTarget, TResult> continuation,
        bool preserveContext)
    {
        TResult? taskResult = default;
        if (previousTask is not null)
        {
            if (!previousTask.IsCompleted)
            {
                await new NoThrowAwaiter(previousTask, preserveContext);
            }

            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                taskResult = previousTask.Result;
            }
            else if (previousTask.Status == TaskStatus.Faulted)
            {
                exception ??= previousTask.Exception?.GetBaseException();
            }
            else if (previousTask.Status == TaskStatus.Canceled)
            {
                try
                {
                    await previousTask.ConfigureAwait(preserveContext);
                }
                catch (Exception ex)
                {
                    exception ??= ex;
                }
            }
        }

        TResult? continuationResult = taskResult;
        try
        {
            continuationResult = await continuation(target, taskResult, exception, in state).ConfigureAwait(preserveContext);
        }
        catch (Exception ex)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(ex);
        }

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return continuationResult;
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Executes a synchronous generated typed value-task-result callback after the original value task completes.
    /// </summary>
    private static async ValueTask<TResult?> ContinueValueTaskResultSync<TIntegration, TTarget, TResult>(
        TTarget? target,
        ValueTask<TResult> previousValueTask,
        Exception? exception,
        CallTargetState state,
        SyncTaskResultContinuationMethod<TTarget, TResult> continuation,
        bool preserveContext)
    {
        if (exception != null)
        {
            return continuation(target, default, exception, in state);
        }

        TResult? taskResult = default;
        try
        {
            taskResult = await previousValueTask.ConfigureAwait(preserveContext);
        }
        catch (Exception ex)
        {
            try
            {
                _ = continuation(target, default, ex, in state);
            }
            catch (Exception continuationException)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(continuationException);
            }

            throw;
        }

        try
        {
            return continuation(target, taskResult, null, in state);
        }
        catch (Exception continuationException)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(continuationException);
        }

        return taskResult;
    }
#endif

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Executes an asynchronous generated typed value-task-result callback after the original value task completes.
    /// </summary>
    private static async ValueTask<TResult?> ContinueValueTaskResultAsync<TIntegration, TTarget, TResult>(
        TTarget? target,
        ValueTask<TResult> previousValueTask,
        Exception? exception,
        CallTargetState state,
        AsyncTaskResultContinuationMethod<TTarget, TResult> continuation,
        bool preserveContext)
    {
        if (exception != null)
        {
            return await continuation(target, default, exception, in state).ConfigureAwait(preserveContext);
        }

        TResult? taskResult = default;
        try
        {
            taskResult = await previousValueTask.ConfigureAwait(preserveContext);
        }
        catch (Exception ex)
        {
            try
            {
                await continuation(target, default, ex, in state).ConfigureAwait(preserveContext);
            }
            catch (Exception continuationException)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(continuationException);
            }

            throw;
        }

        try
        {
            return await continuation(target, taskResult, null, in state).ConfigureAwait(preserveContext);
        }
        catch (Exception continuationException)
        {
            IntegrationOptions<TIntegration, TTarget>.LogException(continuationException);
        }

        return taskResult;
    }
#endif

    /// <summary>
    /// Defines the cache key used to reuse the closed async callback method lookup for a concrete binding.
    /// </summary>
    /// <param name="IntegrationType">The integration type.</param>
    /// <param name="TargetType">The concrete target type.</param>
    /// <param name="ResultType">The async continuation result type.</param>
    private readonly record struct AsyncEndInvokerKey(Type IntegrationType, Type TargetType, Type ResultType);
}
