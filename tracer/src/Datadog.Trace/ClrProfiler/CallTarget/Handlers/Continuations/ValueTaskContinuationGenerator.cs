// <copyright file="ValueTaskContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static CallbackHandler _resolver;

        static ValueTaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
            if (result.Method is not null)
            {
                if (result.Method.ReturnType == typeof(Task) ||
                    (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType)))
                {
                    var asyncContinuation = (AsyncObjectContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncObjectContinuationMethodDelegate));
                    _resolver = new AsyncCallbackHandler(asyncContinuation, result.PreserveContext);
                }
                else
                {
                    var continuation = (ObjectContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ObjectContinuationMethodDelegate));
                    _resolver = new SyncCallbackHandler(continuation, result.PreserveContext);
                }
            }
            else
            {
                _resolver = new NoOpCallbackHandler();
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "== {TaskContinuationGenerator} using Resolver: {Resolver}",
                    $"TaskContinuationGenerator<{typeof(TIntegration).FullName}, {typeof(TTarget).FullName}, {typeof(TReturn).FullName}>",
                    _resolver.GetType().FullName);
            }
        }

#if NET6_0_OR_GREATER
        internal static void EnsureInitializedForNativeAot(IntPtr callback, bool isAsyncCallback, bool preserveContext)
        {
            if (_resolver is null)
            {
                if (callback == IntPtr.Zero)
                {
                    _resolver = new NoOpCallbackHandler();
                }
                else if (isAsyncCallback)
                {
                    _resolver = new AsyncCallbackHandler(callback, preserveContext);
                }
                else
                {
                    _resolver = new SyncCallbackHandler(callback, preserveContext);
                }
            }
        }
#endif

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return _resolver.ExecuteCallback(instance, returnValue, exception, in state);
        }

        private class SyncCallbackHandler : CallbackHandler
        {
            private readonly ObjectContinuationMethodDelegate _continuation;
            private readonly bool _preserveContext;

            public SyncCallbackHandler(ObjectContinuationMethodDelegate continuation, bool preserveContext)
            {
                _continuation = continuation;
                _preserveContext = preserveContext;
            }

            public unsafe SyncCallbackHandler(IntPtr continuation, bool preserveContext)
            {
                var callback = (delegate*<TTarget, object, Exception, in CallTargetState, object>)continuation;
                _continuation = (TTarget instance, object returnValue, Exception exception, in CallTargetState state) => callback(instance, returnValue, exception, in state);
                _preserveContext = preserveContext;
            }

            public override TReturn ExecuteCallback(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                if (exception != null)
                {
                    _continuation(instance, default, exception, in state);
                    return returnValue;
                }

                var previousValueTask = FromTReturn<ValueTask>(returnValue);
                return ToTReturn(ContinuationAction(previousValueTask, instance, state));
            }

            private async ValueTask ContinuationAction(ValueTask previousValueTask, TTarget target, CallTargetState state)
            {
                try
                {
                    await previousValueTask.ConfigureAwait(_preserveContext);
                }
                catch (Exception ex)
                {
                    try
                    {
                        // *
                        // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                        // *
                        _continuation(target, default, ex, in state);
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
                    _continuation(target, default, default, in state);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
                }
            }
        }

        private class AsyncCallbackHandler : CallbackHandler
        {
            private readonly AsyncObjectContinuationMethodDelegate _asyncContinuation;
            private readonly bool _preserveContext;

            public AsyncCallbackHandler(AsyncObjectContinuationMethodDelegate asyncContinuation, bool preserveContext)
            {
                _asyncContinuation = asyncContinuation;
                _preserveContext = preserveContext;
            }

            public unsafe AsyncCallbackHandler(IntPtr asyncContinuation, bool preserveContext)
            {
                var callback = (delegate*<TTarget, object, Exception, in CallTargetState, Task<object>>)asyncContinuation;
                _asyncContinuation = (TTarget instance, object returnValue, Exception exception, in CallTargetState state) => callback(instance, returnValue, exception, in state);
                _preserveContext = preserveContext;
            }

            public override TReturn ExecuteCallback(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            {
                var previousValueTask = returnValue == null ? new ValueTask() : FromTReturn<ValueTask>(returnValue);
                return ToTReturn(ContinuationAction(previousValueTask, instance, state, exception));
            }

            private async ValueTask ContinuationAction(ValueTask previousValueTask, TTarget target, CallTargetState state, Exception exception)
            {
                if (exception != null)
                {
                    await _asyncContinuation(target, default, exception, in state).ConfigureAwait(_preserveContext);
                }

                try
                {
                    await previousValueTask.ConfigureAwait(_preserveContext);
                }
                catch (Exception ex)
                {
                    try
                    {
                        // *
                        // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                        // *
                        await _asyncContinuation(target, default, ex, in state).ConfigureAwait(_preserveContext);
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
                    await _asyncContinuation(target, default, default, in state).ConfigureAwait(_preserveContext);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx);
                }
            }
        }
    }
}
#endif
