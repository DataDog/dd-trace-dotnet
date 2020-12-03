using System;
using System.Reflection.Emit;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
#if NETCOREAPP3_1 || NET5_0
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly Func<TTarget, TResult, Exception, CallTargetState, TResult> _continuation;

        static ValueTaskContinuationGenerator()
        {
            DynamicMethod continuationMethod = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
            if (continuationMethod != null)
            {
                _continuation = (Func<TTarget, TResult, Exception, CallTargetState, TResult>)continuationMethod.CreateDelegate(typeof(Func<TTarget, TResult, Exception, CallTargetState, TResult>));
            }
        }

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (_continuation is null)
            {
                return returnValue;
            }

            if (exception != null)
            {
                _continuation(instance, default, exception, state);
                return returnValue;
            }

            ValueTask<TResult> previousValueTask = FromTReturn<ValueTask<TResult>>(returnValue);
            return ToTReturn(InnerSetValueTaskContinuation(instance, previousValueTask, state));

            static async ValueTask<TResult> InnerSetValueTaskContinuation(TTarget instance, ValueTask<TResult> previousValueTask, CallTargetState state)
            {
                TResult result = default;
                try
                {
                    result = await previousValueTask;
                }
                catch (Exception ex)
                {
                    _continuation(instance, result, ex, state);
                    throw;
                }

                return _continuation(instance, result, null, state);
            }
        }
    }
#endif
}
