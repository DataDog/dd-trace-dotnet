// <copyright file="ValueTaskContinuationGenerator`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Serilog.Events;

#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn, TResult>
{
    private static readonly CallbackHandler Resolver;

    static ValueTaskContinuationGenerator()
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
                $"TaskContinuationGenerator<{typeof(TIntegration).FullName}, {typeof(TTarget).FullName}, {typeof(TReturn).FullName}>",
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

            var previousValueTask = FromTReturn<ValueTask<TResult>>(returnValue);
            return ToTReturn(ContinuationAction(previousValueTask, instance, state));
        }

        private async ValueTask<TResult?> ContinuationAction(ValueTask<TResult> previousValueTask, TTarget? target, CallTargetState state)
        {
            TResult? result = default;
            try
            {
                result = await previousValueTask.ConfigureAwait(_preserveContext);
            }
            catch (Exception ex)
            {
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    _continuation(target, result, ex, in state);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
                }

                throw;
            }

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                return _continuation(target, result, null, in state);
            }
            catch (Exception contEx)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
            }

            return result;
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
            var previousValueTask = returnValue == null ? new ValueTask<TResult>() : FromTReturn<ValueTask<TResult>>(returnValue);
            return ToTReturn(ContinuationAction(previousValueTask, instance, state, exception));
        }

        private async ValueTask<TResult?> ContinuationAction(ValueTask<TResult> previousValueTask, TTarget? target, CallTargetState state, Exception? exception)
        {
            if (exception != null)
            {
                return await _asyncContinuation(target, default, exception, in state).ConfigureAwait(_preserveContext);
            }

            TResult? result = default;
            try
            {
                result = await previousValueTask.ConfigureAwait(_preserveContext);
            }
            catch (Exception ex)
            {
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    await _asyncContinuation(target, result, ex, in state).ConfigureAwait(_preserveContext);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
                }

                throw;
            }

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                return await _asyncContinuation(target, result, null, in state).ConfigureAwait(_preserveContext);
            }
            catch (Exception contEx)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
            }

            return result;
        }
    }
}
#endif
