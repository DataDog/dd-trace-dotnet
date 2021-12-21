// <copyright file="ValueTaskContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
#if NETCOREAPP3_1 || NET5_0
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly ContinuationMethodDelegate _continuation;
        private static readonly bool _preserveContext;

        static ValueTaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
            if (result.Method != null)
            {
                _continuation = (ContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ContinuationMethodDelegate));
                _preserveContext = result.PreserveContext;
            }
        }

        internal delegate object ContinuationMethodDelegate(TTarget target, object returnValue, Exception exception, in CallTargetState state);

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

            ValueTask previousValueTask = FromTReturn<ValueTask>(returnValue);

            return ToTReturn(InnerSetValueTaskContinuation(instance, previousValueTask, state));

            static async ValueTask InnerSetValueTaskContinuation(TTarget instance, ValueTask previousValueTask, CallTargetState state)
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
                        _continuation(instance, default, ex, in state);
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
                    _continuation(instance, default, default, in state);
                }
                catch (Exception contEx)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(contEx, "Exception occurred when calling the CallTarget integration continuation.");
                }
            }
        }
    }
#endif
}
