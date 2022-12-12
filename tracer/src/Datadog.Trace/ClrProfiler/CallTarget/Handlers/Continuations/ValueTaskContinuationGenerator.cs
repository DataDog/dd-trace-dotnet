// <copyright file="ValueTaskContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly ContinuationResolver Resolver;

        static ValueTaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
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

        internal delegate object ContinuationMethodDelegate(TTarget target, object returnValue, Exception exception, in CallTargetState state);

        internal delegate Task<object> AsyncContinuationMethodDelegate(TTarget target, object returnValue, Exception exception, in CallTargetState state);

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
                        IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
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
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
                }
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
                        IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
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
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
                }
            }
        }
    }
}
#endif
