// <copyright file="TaskContinuationGenerator`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly ContinuationResolver Resolver;

        static TaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
            if (result.Method is not null)
            {
                if (result.Method.ReturnType == typeof(Task) ||
                    (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType)))
                {
                    var asyncContinuation = (AsyncContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncContinuationMethodDelegate));
                    Resolver = new AsyncContinuationResolver(asyncContinuation, result.PreserveContext);
                }
                else
                {
                    var continuation = (ContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ContinuationMethodDelegate));
                    Resolver = new SyncContinuationResolver(continuation, result.PreserveContext);
                }
            }

            Resolver ??= new ContinuationResolver();
        }

        internal delegate TResult ContinuationMethodDelegate(TTarget target, TResult returnValue, Exception exception, in CallTargetState state);

        internal delegate Task<TResult> AsyncContinuationMethodDelegate(TTarget target, TResult returnValue, Exception exception, in CallTargetState state);

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return Resolver.SetContinuation(instance, returnValue, exception, in state);
        }

        private class ContinuationResolver
        {
            public virtual TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                return returnValue;
            }
        }

        private class SyncContinuationResolver : ContinuationResolver
        {
            private readonly ContinuationMethodDelegate _continuation;
            private readonly bool _preserveContext;

            public SyncContinuationResolver(ContinuationMethodDelegate continuation, bool preserveContext)
            {
                _continuation = continuation;
                _preserveContext = preserveContext;
            }

            public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                if (exception != null || returnValue == null)
                {
                    _continuation(instance, default, exception, in state);
                    return returnValue;
                }

                var previousTask = FromTReturn<Task<TResult>>(returnValue);
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return ToTReturn(Task.FromResult(_continuation(instance, previousTask.Result, default, in state)));
                }

                return ToTReturn(ContinuationAction(previousTask, instance, state));
            }

            private async Task<TResult> ContinuationAction(Task<TResult> previousTask, TTarget target, CallTargetState state)
            {
                if (!previousTask.IsCompleted)
                {
                    await new NoThrowAwaiter(previousTask, _preserveContext);
                }

                TResult taskResult = default;
                Exception exception = null;
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

                TResult continuationResult = default;
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    continuationResult = _continuation(target, taskResult, exception, in state);
                }
                catch (Exception ex)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(ex, "Exception occurred when calling the CallTarget integration continuation.");
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

        private class AsyncContinuationResolver : ContinuationResolver
        {
            private readonly AsyncContinuationMethodDelegate _asyncContinuation;
            private readonly bool _preserveContext;

            public AsyncContinuationResolver(AsyncContinuationMethodDelegate asyncContinuation, bool preserveContext)
            {
                _asyncContinuation = asyncContinuation;
                _preserveContext = preserveContext;
            }

            public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                var previousTask = returnValue == null ? null : FromTReturn<Task<TResult>>(returnValue);
                return ToTReturn(ContinuationAction(previousTask, instance, state, exception));
            }

            private async Task<TResult> ContinuationAction(Task<TResult> previousTask, TTarget target, CallTargetState state, Exception exception)
            {
                TResult taskResult = default;
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

                TResult continuationResult = default;
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    continuationResult = await _asyncContinuation(target, taskResult, exception, in state).ConfigureAwait(_preserveContext);
                }
                catch (Exception ex)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(ex, "Exception occurred when calling the CallTarget integration continuation.");
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
}
