using System;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly Func<TTarget, TResult, Exception, CallTargetState, TResult> _continuation;
        private static readonly Func<Task<TResult>, object, TResult> _continuationAction;

        static TaskContinuationGenerator()
        {
            DynamicMethod continuationMethod = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
            if (continuationMethod != null)
            {
                _continuation = (Func<TTarget, TResult, Exception, CallTargetState, TResult>)continuationMethod.CreateDelegate(typeof(Func<TTarget, TResult, Exception, CallTargetState, TResult>));
            }

            _continuationAction = new Func<Task<TResult>, object, TResult>(ContinuationAction);
        }

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (_continuation == null)
            {
                return returnValue;
            }

            if (exception != null || returnValue == null)
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
                _continuationAction,
                continuationState,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Current));
        }

        private static TResult ContinuationAction(Task<TResult> previousTask, object state)
        {
            ContinuationGeneratorState<TTarget> contState = (ContinuationGeneratorState<TTarget>)state;
            if (previousTask.Exception is null)
            {
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    return _continuation(contState.Target, previousTask.Result, null, contState.State);
                }
                catch (Exception ex)
                {
                    IntegrationOptions<TIntegration, TTarget>.LogException(ex, "Exception occurred when calling the CallTarget integration continuation.");
                }

                return previousTask.Result;
            }

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // In this case we don't need the return value of the continuation because we are going to throw the original
                // exception.
                // *
                _continuation(contState.Target, default, previousTask.Exception, contState.State);
            }
            catch (Exception ex)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(ex, "Exception occurred when calling the CallTarget integration continuation.");
            }

            // *
            // If the original task throws an exception we rethrow it here.
            // *
            ExceptionDispatchInfo.Capture(previousTask.Exception.GetBaseException()).Throw();
            return default;
        }
    }
}
