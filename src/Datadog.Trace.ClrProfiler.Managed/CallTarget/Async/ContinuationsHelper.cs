using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Async
{
    internal static class ContinuationsHelper<TAsyncInstance>
    {
        private static readonly ContinuationGenerator _continuationGenerator;

        static ContinuationsHelper()
        {
            _continuationGenerator = new ContinuationGenerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
        {
            return _continuationGenerator.SetContinuation(returnValue, exception, state, continuation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TAsyncInstance ToTAsyncInstance<TFrom>(TFrom returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TFrom, TAsyncInstance>(ref returnValue);
#else
            return (TAsyncInstance)(object)returnValue;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TTo FromTAsyncInstance<TTo>(TAsyncInstance returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TAsyncInstance, TTo>(ref returnValue);
#else
            return (TTo)(object)returnValue;
#endif
        }

        private readonly struct TaskContinuationGeneratorState<TState>
        {
            private readonly TState _state;
            private readonly Func<object, Exception, TState, object> _continuation;

            public TaskContinuationGeneratorState(TState state, Func<object, Exception, TState, object> continuation)
            {
                _state = state;
                _continuation = continuation;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteContinuation(Task parentTask)
            {
                _continuation(null, parentTask?.Exception, _state);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TResult ExecuteContinuation<TResult>(Task<TResult> parentTask)
            {
                return (TResult)_continuation(parentTask.Result, parentTask.Exception, _state);
            }
        }

        private class ContinuationGenerator
        {
            public virtual TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
            {
                return returnValue;
            }
        }

        private class TaskContinuationGenerator : ContinuationGenerator
        {
            public override TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (exception != null)
                {
                    continuation(default, exception, state);
                    return returnValue;
                }

                Task previousTask = FromTAsyncInstance<Task>(returnValue);
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    continuation(default, null, state);
#if NET45
                    // "If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion state before it's returned to the caller."
                    // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?redirectedfrom=MSDN&view=netframework-4.5.2#System_Threading_Tasks_Task_WhenAll_System_Threading_Tasks_Task___
                    return ToTAsyncInstance(Task.WhenAll());
#else
                    return ToTAsyncInstance(Task.CompletedTask);
#endif
                }

                var continuationState = new TaskContinuationGeneratorState<TState>(state, continuation);
                return ToTAsyncInstance(previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        ((TaskContinuationGeneratorState<TState>)oState).ExecuteContinuation(pTask);
                    },
                    continuationState,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current));
            }
        }

        private class TaskContinuationGenerator<TResult> : ContinuationGenerator
        {
            public override TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (exception != null)
                {
                    continuation(default, exception, state);
                    return returnValue;
                }

                Task<TResult> previousTask = FromTAsyncInstance<Task<TResult>>(returnValue);

                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return ToTAsyncInstance(Task.FromResult((TResult)continuation(previousTask.Result, default, state)));
                }

                var continuationState = new TaskContinuationGeneratorState<TState>(state, continuation);
                return ToTAsyncInstance(previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        return ((TaskContinuationGeneratorState<TState>)oState).ExecuteContinuation(pTask);
                    },
                    continuationState,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current));
            }
        }

#if NETCOREAPP3_1 || NET5_0
        private class ValueTaskContinuationGenerator : ContinuationGenerator
        {
            public override TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (exception != null)
                {
                    continuation(default, exception, state);
                    return returnValue;
                }

                ValueTask previousValueTask = FromTAsyncInstance<ValueTask>(returnValue);

                return ToTAsyncInstance(InnerSetValueTaskContinuation(previousValueTask, state, continuation));

                static async ValueTask InnerSetValueTaskContinuation(ValueTask previousValueTask, TState state, Func<object, Exception, TState, object> continuation)
                {
                    try
                    {
                        await previousValueTask;
                    }
                    catch (Exception ex)
                    {
                        continuation(default, ex, state);
                        throw;
                    }

                    continuation(default, default, state);
                }
            }
        }

        private class ValueTaskContinuationGenerator<TResult> : ContinuationGenerator
        {
            public override TAsyncInstance SetContinuation<TState>(TAsyncInstance returnValue, Exception exception, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (exception != null)
                {
                    continuation(default, exception, state);
                    return returnValue;
                }

                ValueTask<TResult> previousValueTask = FromTAsyncInstance<ValueTask<TResult>>(returnValue);
                return ToTAsyncInstance(InnerSetValueTaskContinuation(previousValueTask, state, continuation));

                static async ValueTask<TResult> InnerSetValueTaskContinuation(ValueTask<TResult> previousValueTask, TState state, Func<object, Exception, TState, object> continuation)
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

                    return (TResult)continuation(result, null, state);
                }
            }
        }
#endif
    }
}
