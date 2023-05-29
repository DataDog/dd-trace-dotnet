// <copyright file="TaskContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly CallbackHandler Resolver;
        private static readonly DynamicMethod Method;

        static TaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
            if (result.Method is not null)
            {
                Method = result.Method;
                if (result.Method.ReturnType == typeof(Task) ||
                    (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType)))
                {
                    Resolver = new AsyncCallbackHandler(result.Method.GetFunctionPointer(), result.PreserveContext);
                }
                else
                {
                    Resolver = new SyncCallbackHandler(result.Method.GetFunctionPointer(), result.PreserveContext);
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
                    $"TaskContinuationGenerator<{typeof(TIntegration).FullName}, {typeof(TTarget).FullName}, {typeof(TReturn).FullName}>",
                    Resolver.GetType().FullName);
            }
        }

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return Resolver.ExecuteCallback(instance, returnValue, exception, in state);
        }

        private class SyncCallbackHandler : CallbackHandler
        {
            private readonly IntPtr _continuation;
            private readonly bool _preserveContext;

            public SyncCallbackHandler(IntPtr continuation, bool preserveContext)
            {
                _continuation = continuation;
                _preserveContext = preserveContext;
            }

            public override unsafe TReturn ExecuteCallback(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                if (exception != null || returnValue == null)
                {
                    ((delegate*<TTarget, object, Exception, in CallTargetState, object>)_continuation)(instance, default, exception, in state);
                    return returnValue;
                }

                Task previousTask = FromTReturn<Task>(returnValue);
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    ((delegate*<TTarget, object, Exception, in CallTargetState, object>)_continuation)(instance, default, null, in state);
                    return returnValue;
                }

                return ToTReturn(ContinuationAction(previousTask, instance, state));
            }

            private async Task ContinuationAction(Task previousTask, TTarget target, CallTargetState state)
            {
                if (!previousTask.IsCompleted)
                {
                    await new NoThrowAwaiter(previousTask, _preserveContext);
                }

                Exception exception = null;

                if (previousTask.Status == TaskStatus.Faulted)
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
                    unsafe
                    {
                        ((delegate*<TTarget, object, Exception, in CallTargetState, object>)_continuation)(target, null, exception, in state);
                    }
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
            }
        }

        private class AsyncCallbackHandler : CallbackHandler
        {
            private readonly IntPtr _asyncContinuation;
            private readonly bool _preserveContext;

            public AsyncCallbackHandler(IntPtr asyncContinuation, bool preserveContext)
            {
                _asyncContinuation = asyncContinuation;
                _preserveContext = preserveContext;
            }

            public override TReturn ExecuteCallback(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                var previousTask = returnValue == null ? null : FromTReturn<Task>(returnValue);
                return ToTReturn(ContinuationAction(previousTask, instance, state, exception));
            }

            private async Task ContinuationAction(Task previousTask, TTarget target, CallTargetState state, Exception exception)
            {
                if (previousTask is not null)
                {
                    if (!previousTask.IsCompleted)
                    {
                        await new NoThrowAwaiter(previousTask, _preserveContext);
                    }

                    if (previousTask.Status == TaskStatus.Faulted)
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

                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    Task<object> task;
                    unsafe
                    {
                        task = ((delegate*<TTarget, object, Exception, in CallTargetState, Task<object>>)_asyncContinuation)(target, null, exception, in state);
                    }

                    await task.ConfigureAwait(_preserveContext);
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
            }
        }
    }
}
