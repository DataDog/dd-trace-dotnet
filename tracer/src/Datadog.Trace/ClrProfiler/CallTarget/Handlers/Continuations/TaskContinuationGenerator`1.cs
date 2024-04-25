// <copyright file="TaskContinuationGenerator`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Serilog.Events;

#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn, TResult>
{
    private static readonly CallbackHandler Resolver;

    static TaskContinuationGenerator()
    {
        var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
        if (result.Method is not null)
        {
            if (result.Method.ReturnType == typeof(Task) ||
                (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType)))
            {
                var asyncContinuation = (AsyncContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncContinuationMethodDelegate));
                Resolver = new AsyncCallbackHandler(asyncContinuation, result.PreserveContext);
            }
            else
            {
                var continuation = (ContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ContinuationMethodDelegate));
                Resolver = new SyncCallbackHandler(continuation, result.PreserveContext);
            }
        }
        else
        {
            Resolver = new NoOpCallbackHandler();
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "== {TaskContinuationGenerator} using Resolver: {Resolver}",
                $"TaskContinuationGenerator<{typeof(TIntegration).FullName}, {typeof(TTarget).FullName}, {typeof(TReturn).FullName}, {typeof(TResult).FullName}>",
                Resolver.GetType().FullName);
        }
    }

    public override TReturn? SetContinuation(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        return Resolver.ExecuteCallback(instance, returnValue, exception, in state);
    }

    private class SyncCallbackHandler : CallbackHandler
    {
        private readonly ContinuationMethodDelegate _continuation;
        private readonly bool _preserveContext;

        public SyncCallbackHandler(ContinuationMethodDelegate continuation, bool preserveContext)
        {
            _continuation = continuation;
            _preserveContext = preserveContext;
        }

        public override TReturn? ExecuteCallback(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        {
            if (exception != null || returnValue == null)
            {
                _continuation(instance, default, exception, in state);
                return returnValue;
            }

            var previousTask = FromTReturn<Task<TResult>>(returnValue);
            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                var result = _continuation(instance, previousTask.Result, default, in state);
                if (EqualityComparer<TResult?>.Default.Equals(result, previousTask.Result))
                {
                    return returnValue;
                }

                return ToTReturn(Task.FromResult(result));
            }

            return ToTReturn(ContinuationAction(previousTask, instance, state));
        }

        private async Task<TResult?> ContinuationAction(Task<TResult> previousTask, TTarget? target, CallTargetState state)
        {
            if (!previousTask.IsCompleted)
            {
                await new NoThrowAwaiter(previousTask, _preserveContext);
            }

            TResult? taskResult = default;
            Exception? exception = null;
            TResult? continuationResult = default;

            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                taskResult = previousTask.Result;
            }
            else if (previousTask.Status == TaskStatus.Faulted)
            {
                exception = previousTask.Exception?.GetBaseException();
            }
            else if (previousTask.Status == TaskStatus.Canceled)
            {
                try
                {
                    // The only supported way to extract the cancellation exception is to await the task
                    await previousTask.ConfigureAwait(_preserveContext);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                continuationResult = _continuation(target, taskResult, exception, in state);
            }
            catch (Exception ex)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(ex);
            }

            // *
            // If the original task throws an exception we rethrow it here.
            // *
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return continuationResult;
        }
    }

    private class AsyncCallbackHandler : CallbackHandler
    {
        private readonly AsyncContinuationMethodDelegate _asyncContinuation;
        private readonly bool _preserveContext;

        public AsyncCallbackHandler(AsyncContinuationMethodDelegate asyncContinuation, bool preserveContext)
        {
            _asyncContinuation = asyncContinuation;
            _preserveContext = preserveContext;
        }

        public override TReturn ExecuteCallback(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        {
            var previousTask = returnValue == null ? null : FromTReturn<Task<TResult>>(returnValue);
            return ToTReturn(ContinuationAction(previousTask, instance, state, exception));
        }

        private async Task<TResult?> ContinuationAction(Task<TResult>? previousTask, TTarget? target, CallTargetState state, Exception? exception)
        {
            TResult? taskResult = default;
            if (previousTask is not null)
            {
                if (!previousTask.IsCompleted)
                {
                    await new NoThrowAwaiter(previousTask, _preserveContext);
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
                        // The only supported way to extract the cancellation exception is to await the task
                        await previousTask.ConfigureAwait(_preserveContext);
                    }
                    catch (Exception ex)
                    {
                        exception ??= ex;
                    }
                }
            }

            TResult? continuationResult = default;
            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                continuationResult = await _asyncContinuation(target, taskResult, exception, in state).ConfigureAwait(_preserveContext);
            }
            catch (Exception ex)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(ex);
            }

            // *
            // If the original task throws an exception we rethrow it here.
            // *
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return continuationResult;
        }
    }
}
