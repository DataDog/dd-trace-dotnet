using System;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TIntegration, TTarget, TReturn>
    {
        private static readonly Func<TTarget, TResult, Exception, CallTargetState, TResult> _continuation;

        static TaskContinuationGenerator()
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

            Task<TResult> previousTask = FromTReturn<Task<TResult>>(returnValue);

            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                return ToTReturn(Task.FromResult(_continuation(instance, previousTask.Result, default, state)));
            }

            var continuationState = new ContinuationGeneratorState<TTarget>(instance, state);
            return ToTReturn(previousTask.ContinueWith(
                (pTask, oState) =>
                {
                    var contState = (ContinuationGeneratorState<TTarget>)oState;
                    if (pTask.Exception is null)
                    {
                        return _continuation(contState.Target, pTask.Result, null, contState.State);
                    }
                    return _continuation(contState.Target, default, pTask.Exception, contState.State);
                },
                continuationState,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Current));
        }
    }
}
