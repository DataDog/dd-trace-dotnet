// <copyright file="ValueTaskContinuationGenerator`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly ContinuationMethodDelegate _continuation;
        private static readonly bool _preserveContext;

        static ValueTaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
            if (result.Method != null)
            {
                _continuation = (ContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ContinuationMethodDelegate));
                _preserveContext = result.PreserveContext;
            }
        }

        internal delegate TResult ContinuationMethodDelegate(TTarget target, TResult returnValue, Exception exception, in CallTargetState state);

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (_continuation is null)
            {
                return returnValue;
            }

            if (exception != null)
            {
                _continuation(instance, default, exception, in state);
                return returnValue;
            }

            ValueTask<TResult> previousValueTask = FromTReturn<ValueTask<TResult>>(returnValue);
            return ToTReturn(InnerSetValueTaskContinuation(instance, previousValueTask, state));

            static async ValueTask<TResult> InnerSetValueTaskContinuation(TTarget instance, ValueTask<TResult> previousValueTask, CallTargetState state)
            {
                TResult result = default;
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
                        _continuation(instance, result, ex, in state);
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
                    return _continuation(instance, result, null, in state);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
                }

                return result;
            }
        }
    }
}
#endif
