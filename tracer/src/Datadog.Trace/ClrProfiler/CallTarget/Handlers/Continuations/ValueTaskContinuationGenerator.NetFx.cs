// <copyright file="ValueTaskContinuationGenerator.NetFx.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETCOREAPP3_1_OR_GREATER
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal sealed class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
{
    private static readonly CallbackHandler Resolver;

    static ValueTaskContinuationGenerator()
    {
        var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
        if (result.Method is not null)
        {
            if (result.Method.ReturnType == typeof(Task) ||
                (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType)))
            {
                var asyncContinuation = (AsyncObjectContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncObjectContinuationMethodDelegate));
                Resolver = new AsyncCallbackHandler(asyncContinuation, result.PreserveContext);
            }
            else
            {
                var continuation = (ObjectContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ObjectContinuationMethodDelegate));
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
                $"TaskContinuationGenerator<{typeof(TIntegration).FullName}, {typeof(TTarget).FullName}, {typeof(TReturn).FullName}>",
                Resolver.GetType().FullName);
        }
    }

    public override TReturn? SetContinuation(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        return Resolver.ExecuteCallback(instance, returnValue, exception, in state);
    }

    private sealed class SyncCallbackHandler : CallbackHandler
    {
        private readonly ObjectContinuationMethodDelegate _continuation;
        private readonly bool _preserveContext;

        public SyncCallbackHandler(ObjectContinuationMethodDelegate continuation, bool preserveContext)
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

            var previousValueTask = returnValue.DuckCast<IValueTaskDuckType>();
            if (previousValueTask.IsCompletedSuccessfully)
            {
                // ok all good, just run synchronously
                _continuation(instance, returnValue, exception, in state);
                return returnValue;
            }

            // uh oh, need to extract the task, await it, run the continuation
            var task = previousValueTask.AsTask();
            var secondTask = ContinuationAction(task, instance, state);
            // need to wrap the secondTask in a ValueTask to return it
            return ValueTaskActivator<TReturn>.CreateInstance(secondTask);
        }

        private async Task ContinuationAction(Task previousTask, TTarget? target, CallTargetState state)
        {
            if (!previousTask.IsCompleted)
            {
                await new NoThrowAwaiter(previousTask, _preserveContext);
            }

            Exception? exception = null;

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
                _continuation(target, null, exception, in state);
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

    private sealed class AsyncCallbackHandler : CallbackHandler
    {
        private readonly AsyncObjectContinuationMethodDelegate _asyncContinuation;
        private readonly bool _preserveContext;

        public AsyncCallbackHandler(AsyncObjectContinuationMethodDelegate asyncContinuation, bool preserveContext)
        {
            _asyncContinuation = asyncContinuation;
            _preserveContext = preserveContext;
        }

        public override TReturn ExecuteCallback(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        {
            Task task;
            if (returnValue == null)
            {
                task = Task.CompletedTask;
            }
            else
            {
                var previousValueTask = returnValue.DuckCast<IValueTaskDuckType>();
                if (previousValueTask.IsCompletedSuccessfully)
                {
                    // ok all good, just run synchronously
                    task = Task.CompletedTask;
                }
                else
                {
                    task = previousValueTask.AsTask();
                }
            }

            var secondTask = ContinuationAction(task, instance, state, exception);
            // need to wrap the secondTask in a ValueTask to return it
            return ValueTaskActivator<TReturn>.CreateInstance(secondTask);
        }

        private async Task ContinuationAction(Task previousTask, TTarget? target, CallTargetState state, Exception? exception)
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

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                await _asyncContinuation(target, null, exception, in state).ConfigureAwait(_preserveContext);
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
#endif
