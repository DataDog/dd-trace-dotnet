using System;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TIntegration, TTarget, TReturn>
    {
        private static readonly Func<TTarget, object, Exception, CallTargetState, object> _continuation;

        static TaskContinuationGenerator()
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

            Task previousTask = FromTReturn<Task>(returnValue);
            if (previousTask.Status == TaskStatus.RanToCompletion)
            {
                _continuation(instance, default, null, state);
#if NET45
                    // "If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion state before it's returned to the caller."
                    // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?redirectedfrom=MSDN&view=netframework-4.5.2#System_Threading_Tasks_Task_WhenAll_System_Threading_Tasks_Task___
                    return ToTReturn(Task.WhenAll());
#else
                return ToTReturn(Task.CompletedTask);
#endif
            }

            var continuationState = new ContinuationGeneratorState<TTarget>(instance, state);
            return ToTReturn(previousTask.ContinueWith(
                (pTask, oState) =>
                {
                    var contState = (ContinuationGeneratorState<TTarget>)oState;
                    _continuation(contState.Target, null, pTask?.Exception, contState.State);
                },
                continuationState,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Current));
        }
    }
}
