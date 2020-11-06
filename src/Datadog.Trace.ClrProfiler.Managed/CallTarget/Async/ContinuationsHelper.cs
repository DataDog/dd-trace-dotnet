using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.ClrProfiler.CallTarget.Async
{
    internal static class ContinuationsHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Type GetResultType(Type parentType)
        {
            Type currentType = parentType;
            while (currentType != null)
            {
                Type[] typeArguments = currentType.GenericTypeArguments ?? Type.EmptyTypes;
                switch (typeArguments.Length)
                {
                    case 0:
                        return typeof(object);
                    case 1:
                        return typeArguments[0];
                    default:
                        currentType = currentType.BaseType;
                        break;
                }
            }

            return typeof(object);
        }

#if NETCOREAPP3_1 || NET5_0
#else
        internal static TTo Convert<TFrom, TTo>(TFrom value)
        {
            return Converter<TFrom, TTo>.Convert(value);
        }

        private static class Converter<TFrom, TTo>
        {
            private static readonly ConvertDelegate _converter;

            static Converter()
            {
                DynamicMethod dMethod = new DynamicMethod($"Converter<{typeof(TFrom).Name},{typeof(TTo).Name}>", typeof(TTo), new[] { typeof(TFrom) }, typeof(ConvertDelegate).Module, true);
                ILGenerator il = dMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ret);
                _converter = (ConvertDelegate)dMethod.CreateDelegate(typeof(ConvertDelegate));
            }

            private delegate TTo ConvertDelegate(TFrom value);

            public static TTo Convert(TFrom value)
            {
                return _converter(value);
            }
        }
#endif
    }

    internal static class ContinuationsHelper<TIntegration, TTarget, TAsyncInstance>
    {
        private static readonly ContinuationGenerator _continuationGenerator;

        static ContinuationsHelper()
        {
            Type returnType = typeof(TAsyncInstance);
            Type resultType = typeof(void);

            if (returnType.IsGenericType)
            {
                resultType = ContinuationsHelper.GetResultType(returnType);

                Type genericReturnType = returnType.GetGenericTypeDefinition();
                if (typeof(Task).IsAssignableFrom(returnType))
                {
                    // The type is a Task<>
                    _continuationGenerator = (ContinuationGenerator)Activator.CreateInstance(typeof(TaskContinuationGenerator<>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, resultType));
                }
#if NETCOREAPP3_1 || NET5_0
                else if (genericReturnType == typeof(ValueTask<>))
                {
                    // The type is a ValueTask<>
                    _continuationGenerator = (ContinuationGenerator)Activator.CreateInstance(typeof(ValueTaskContinuationGenerator<>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, resultType));
                }
#endif
            }
            else
            {
                if (returnType == typeof(Task))
                {
                    // The type is a Task
                    _continuationGenerator = new TaskContinuationGenerator();
                }
#if NETCOREAPP3_1 || NET5_0
                else if (returnType == typeof(ValueTask))
                {
                    // The type is a ValueTask
                    _continuationGenerator = new ValueTaskContinuationGenerator();
                }
#endif
            }

            _continuationGenerator ??= new ContinuationGenerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
        {
            if (returnValue is null)
            {
                return returnValue;
            }

            return _continuationGenerator.SetContinuation(instance, returnValue, exception, state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TAsyncInstance ToTAsyncInstance<TFrom>(TFrom returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TFrom, TAsyncInstance>(ref returnValue);
#else
            return ContinuationsHelper.Convert<TFrom, TAsyncInstance>(returnValue);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TTo FromTAsyncInstance<TTo>(TAsyncInstance returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TAsyncInstance, TTo>(ref returnValue);
#else
            return ContinuationsHelper.Convert<TAsyncInstance, TTo>(returnValue);
#endif
        }

        private readonly struct TaskContinuationGeneratorState
        {
            public readonly TTarget Target;
            public readonly CallTargetState State;

            public TaskContinuationGeneratorState(TTarget target, CallTargetState state)
            {
                Target = target;
                State = state;
            }
        }

        private class ContinuationGenerator
        {
            public virtual TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
            {
                return returnValue;
            }
        }

        private class TaskContinuationGenerator : ContinuationGenerator
        {
            private static readonly Func<TTarget, object, Exception, CallTargetState, object> _continuation;

            static TaskContinuationGenerator()
            {
                DynamicMethod continuationMethod = CallTargetInvokerHandler.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
                if (continuationMethod != null)
                {
                    _continuation = (Func<TTarget, object, Exception, CallTargetState, object>)continuationMethod.CreateDelegate(typeof(Func<TTarget, object, Exception, CallTargetState, object>));
                }
            }

            public override TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
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

                Task previousTask = FromTAsyncInstance<Task>(returnValue);
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    _continuation(instance, default, null, state);
#if NET45
                    // "If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion state before it's returned to the caller."
                    // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?redirectedfrom=MSDN&view=netframework-4.5.2#System_Threading_Tasks_Task_WhenAll_System_Threading_Tasks_Task___
                    return ToTAsyncInstance(Task.WhenAll());
#else
                    return ToTAsyncInstance(Task.CompletedTask);
#endif
                }

                var continuationState = new TaskContinuationGeneratorState(instance, state);
                return ToTAsyncInstance(previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        var contState = (TaskContinuationGeneratorState)oState;
                        _continuation(contState.Target, null, pTask?.Exception, contState.State);
                    },
                    continuationState,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current));
            }
        }

        private class TaskContinuationGenerator<TResult> : ContinuationGenerator
        {
            private static readonly Func<TTarget, TResult, Exception, CallTargetState, TResult> _continuation;

            static TaskContinuationGenerator()
            {
                DynamicMethod continuationMethod = CallTargetInvokerHandler.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
                if (continuationMethod != null)
                {
                    _continuation = (Func<TTarget, TResult, Exception, CallTargetState, TResult>)continuationMethod.CreateDelegate(typeof(Func<TTarget, TResult, Exception, CallTargetState, TResult>));
                }
            }

            public override TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
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

                Task<TResult> previousTask = FromTAsyncInstance<Task<TResult>>(returnValue);

                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return ToTAsyncInstance(Task.FromResult((TResult)_continuation(instance, previousTask.Result, default, state)));
                }

                var continuationState = new TaskContinuationGeneratorState(instance, state);
                return ToTAsyncInstance(previousTask.ContinueWith(
                    (pTask, oState) =>
                    {
                        var contState = (TaskContinuationGeneratorState)oState;
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

#if NETCOREAPP3_1 || NET5_0
        private class ValueTaskContinuationGenerator : ContinuationGenerator
        {
            private static readonly Func<TTarget, object, Exception, CallTargetState, object> _continuation;

            static ValueTaskContinuationGenerator()
            {
                DynamicMethod continuationMethod = CallTargetInvokerHandler.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
                if (continuationMethod != null)
                {
                    _continuation = (Func<TTarget, object, Exception, CallTargetState, object>)continuationMethod.CreateDelegate(typeof(Func<TTarget, object, Exception, CallTargetState, object>));
                }
            }

            public override TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
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

                ValueTask previousValueTask = FromTAsyncInstance<ValueTask>(returnValue);

                return ToTAsyncInstance(InnerSetValueTaskContinuation(instance, previousValueTask, state));

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

        private class ValueTaskContinuationGenerator<TResult> : ContinuationGenerator
        {
            private static readonly Func<TTarget, TResult, Exception, CallTargetState, TResult> _continuation;

            static ValueTaskContinuationGenerator()
            {
                DynamicMethod continuationMethod = CallTargetInvokerHandler.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TResult));
                if (continuationMethod != null)
                {
                    _continuation = (Func<TTarget, TResult, Exception, CallTargetState, TResult>)continuationMethod.CreateDelegate(typeof(Func<TTarget, TResult, Exception, CallTargetState, TResult>));
                }
            }

            public override TAsyncInstance SetContinuation(TTarget instance, TAsyncInstance returnValue, Exception exception, CallTargetState state)
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

                ValueTask<TResult> previousValueTask = FromTAsyncInstance<ValueTask<TResult>>(returnValue);
                return ToTAsyncInstance(InnerSetValueTaskContinuation(instance, previousValueTask, state));

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
}
