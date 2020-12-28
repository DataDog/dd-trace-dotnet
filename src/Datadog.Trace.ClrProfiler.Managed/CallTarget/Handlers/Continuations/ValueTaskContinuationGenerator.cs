using System;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
#if NETCOREAPP3_1 || NET5_0
    internal class ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly Func<TTarget, object, Exception, CallTargetState, object> _continuation;

        static ValueTaskContinuationGenerator()
        {
            DynamicMethod continuationMethod = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
            if (continuationMethod != null)
            {
                _continuation = (Func<TTarget, object, Exception, CallTargetState, object>)continuationMethod.CreateDelegate(typeof(Func<TTarget, object, Exception, CallTargetState, object>));
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

            ValueTask previousValueTask = FromTReturn<ValueTask>(returnValue);

            return ToTReturn(InnerSetValueTaskContinuation(instance, previousValueTask, state));

            static async ValueTask InnerSetValueTaskContinuation(TTarget instance, ValueTask previousValueTask, CallTargetState state)
            {
                try
                {
                    await previousValueTask;
                }
                catch (Exception ex)
                {
                    _continuation(instance, default, ex, state);
                    throw;
                }

                _continuation(instance, default, default, state);
            }
        }
    }
#endif
}
