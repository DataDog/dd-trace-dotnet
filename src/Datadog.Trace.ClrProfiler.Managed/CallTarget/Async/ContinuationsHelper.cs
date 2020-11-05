using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.CallTarget.Async
{
    internal static class ContinuationsHelper
    {
        public static TAsyncInstance Add<TAsyncInstance, TState>(TAsyncInstance returnValue, Exception exception, TState state, Action<Exception, TState> continuation)
        {
            if (exception != null)
            {
                continuation(exception, state);
                return returnValue;
            }

            if (returnValue is Task returnTask)
            {
                return (TAsyncInstance)(object)TaskContinuationGenerator.SetTaskContinuation(returnTask, state, continuation);
            }

#if NETCOREAPP3_1 || NET5_0
            if (returnValue is ValueTask valueTask)
            {
                ValueTask valueTaskResult = ValueTaskContinuationGenerator.SetValueTaskContinuation(valueTask, state, continuation);
                return Unsafe.As<ValueTask, TAsyncInstance>(ref valueTaskResult);
            }
#endif

            return returnValue;
        }

        public static TAsyncInstance Add<TAsyncInstance, TResult, TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<TResult, Exception, TState, TResult> continuation)
        {
            if (exception != null)
            {
                continuation(default, exception, state);
                return returnValue;
            }

            if (returnValue is Task<TResult> returnTask)
            {
                return (TAsyncInstance)(object)TaskContinuationGenerator<TResult>.SetTaskContinuation(returnTask, state, continuation);
            }

#if NETCOREAPP3_1 || NET5_0
            if (returnValue is ValueTask<TResult> valueTask)
            {
                ValueTask<TResult> valueTaskResult = ValueTaskContinuationGenerator<TResult>.SetValueTaskContinuation(valueTask, state, continuation);
                return Unsafe.As<ValueTask<TResult>, TAsyncInstance>(ref valueTaskResult);
            }
#endif
            return returnValue;
        }

        internal static class TaskContinuationGenerator
        {
            public static Task SetTaskContinuation<TState>(Task previousTask, TState state, Action<Exception, TState> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    continuation(null, state);
#if NET45
                    // "If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion state before it's returned to the caller."
                    // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?redirectedfrom=MSDN&view=netframework-4.5.2#System_Threading_Tasks_Task_WhenAll_System_Threading_Tasks_Task___
                    return Task.WhenAll();
#else
                    return Task.CompletedTask;
#endif
                }

                var continuationState = new TaskContinuationGeneratorState<TState>(state, continuation);
                return previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        ((TaskContinuationGeneratorState<TState>)oState).ExecuteContinuation(pTask);
                    },
                    continuationState,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current);
            }

            internal readonly struct TaskContinuationGeneratorState<TState>
            {
                private readonly TState _state;
                private readonly Action<Exception, TState> _continuation;

                public TaskContinuationGeneratorState(TState state, Action<Exception, TState> continuation)
                {
                    _state = state;
                    _continuation = continuation;
                }

                public void ExecuteContinuation(Task parentTask)
                {
                    _continuation(parentTask?.Exception, _state);
                }
            }
        }

        internal static class TaskContinuationGenerator<TResult>
        {
            public static Task<TResult> SetTaskContinuation<TState>(Task<TResult> previousTask, TState state, Func<TResult, Exception, TState, TResult> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return Task.FromResult(continuation(previousTask.Result, null, state));
                }

                var continuationState = new TaskContinuationGeneratorState<TState>(state, continuation);
                return previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        return ((TaskContinuationGeneratorState<TState>)oState).ExecuteContinuation(pTask);
                    },
                    continuationState,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current);
            }

            internal readonly struct TaskContinuationGeneratorState<TState>
            {
                private readonly TState _state;
                private readonly Func<TResult, Exception, TState, TResult> _continuation;

                public TaskContinuationGeneratorState(TState state, Func<TResult, Exception, TState, TResult> continuation)
                {
                    _state = state;
                    _continuation = continuation;
                }

                public TResult ExecuteContinuation(Task<TResult> parentTask)
                {
                    return _continuation(parentTask.Result, parentTask.Exception, _state);
                }
            }
        }

#if NETCOREAPP3_1 || NET5_0
        internal static class ValueTaskContinuationGenerator
        {
            public static async ValueTask SetValueTaskContinuation<TState>(ValueTask previousValueTask, TState state, Action<Exception, TState> continuation)
            {
                try
                {
                    await previousValueTask;
                }
                catch (Exception ex)
                {
                    continuation(ex, state);
                    throw;
                }

                continuation(null, state);
            }
        }

        internal static class ValueTaskContinuationGenerator<TResult>
        {
            public static async ValueTask<TResult> SetValueTaskContinuation<TState>(ValueTask<TResult> previousValueTask, TState state, Func<TResult, Exception, TState, TResult> continuation)
            {
                TResult result = default;
                try
                {
                    result = await previousValueTask;
                }
                catch (Exception ex)
                {
                    continuation(result, ex, state);
                    throw;
                }

                return continuation(result, null, state);
            }
        }
#endif
    }
}
