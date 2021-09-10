// <copyright file="AsyncTool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// This class is similar to AsyncHelper but removes the params array allocation, the string key allocation and the invoke by reflection.
    /// </summary>
    internal static class AsyncTool
    {
        private static readonly ConcurrentDictionary<Type, TaskContinuationGenerator> ContinuationsGeneratorCache = new ConcurrentDictionary<Type, TaskContinuationGenerator>();

        /// <summary>
        /// Adds a continuation based on the current returnValue
        /// </summary>
        /// <typeparam name="TState">Type of the state</typeparam>
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="state">State value</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <param name="generator">Continuation generator</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation<TState>(object returnValue, Exception ex, TState state, Func<object, Exception, TState, object> continuation, TaskContinuationGenerator generator = null)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                generator ??= GetContinuationFrom(returnTask.GetType());
                return generator.SetTaskContinuation(returnTask, state, continuation);
            }

            return continuation(returnValue, ex, state);
        }

        /// <summary>
        /// Adds a continuation based on the current returnValue
        /// </summary>
        /// <typeparam name="TState">Type of the state</typeparam>
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="state">State value</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <param name="generator">Continuation generator</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation<TState>(object returnValue, Exception ex, TState state, Func<object, Exception, TState, Task<object>> continuation, TaskContinuationGenerator generator = null)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                generator ??= GetContinuationFrom(returnTask.GetType());
                return generator.SetTaskContinuationAsync(returnTask, state, continuation);
            }

            SynchronizationContext currentContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                return continuation(returnValue, ex, state).GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(currentContext);
            }
        }

        /// <summary>
        /// Gets the task continuation generator
        /// </summary>
        /// <param name="type">Task type</param>
        /// <returns>Task continuation generator for this kind of task</returns>
        public static TaskContinuationGenerator GetTaskContinuationGenerator(Type type)
        {
            return GetContinuationFrom(type);
        }

        private static TaskContinuationGenerator GetContinuationFrom(Type type)
        {
            return ContinuationsGeneratorCache.GetOrAdd(type, tType =>
            {
                // We need to find the appropiate generic parameter for the task

                Type currentType = tType;
                while (currentType != null)
                {
                    Type[] typeArguments = currentType.GenericTypeArguments ?? Type.EmptyTypes;
                    switch (typeArguments.Length)
                    {
                        case 0:
                            return new TaskContinuationGenerator();
                        case 1:
                            return (TaskContinuationGenerator)Activator.CreateInstance(typeof(TaskContinuationGenerator<>).MakeGenericType(typeArguments[0]));
                        default:
                            currentType = currentType.BaseType;
                            break;
                    }
                }

                return new TaskContinuationGenerator();
            });
        }

        internal sealed class TaskContinuationGenerator<TResult> : TaskContinuationGenerator
        {
            public override Task SetTaskContinuation<TState>(Task previousTask, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return Task.FromResult((TResult)continuation(((Task<TResult>)previousTask).Result, null, state));
                }

                return InternalSetTaskContinuation((Task<TResult>)previousTask, state, continuation);
            }

            public override Task SetTaskContinuationAsync<TState>(Task previousTask, TState state, Func<object, Exception, TState, Task<object>> continuation)
                => SetTaskContinuationAsync((Task<TResult>)previousTask, state, continuation);

            private static async Task<TResult> InternalSetTaskContinuation<TState>(Task<TResult> previousTask, TState state, Func<object, Exception, TState, object> continuation)
            {
                TResult result = default;
                try
                {
                    result = await previousTask;
                }
                catch (Exception ex)
                {
                    continuation(result, ex, state);
                    throw;
                }

                return (TResult)continuation(result, null, state);
            }

            private static async Task<TResult> SetTaskContinuationAsync<TState>(Task<TResult> previousTask, TState state, Func<object, Exception, TState, Task<object>> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return (TResult)await continuation(previousTask.Result, null, state).ConfigureAwait(false);
                }

                TResult result = default;
                try
                {
                    result = await previousTask;
                }
                catch (Exception ex)
                {
                    await continuation(result, ex, state);
                    throw;
                }

                return (TResult)await continuation(result, null, state);
            }
        }

        internal class TaskContinuationGenerator
        {
            public virtual Task SetTaskContinuation<TState>(Task previousTask, TState state, Func<object, Exception, TState, object> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    continuation(null, null, state);
#if NET45
                    // "If the supplied array/enumerable contains no tasks, the returned task will immediately transition to a RanToCompletion state before it's returned to the caller."
                    // https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?redirectedfrom=MSDN&view=netframework-4.5.2#System_Threading_Tasks_Task_WhenAll_System_Threading_Tasks_Task___
                    return Task.WhenAll();
#else
                    return Task.CompletedTask;
#endif
                }

                return InternalSetTaskContinuation(previousTask, state, continuation);
            }

            private static async Task InternalSetTaskContinuation<TState>(Task previousTask, TState state, Func<object, Exception, TState, object> continuation)
            {
                try
                {
                    await previousTask;
                }
                catch (Exception ex)
                {
                    continuation(null, ex, state);
                    throw;
                }

                continuation(null, null, state);
            }

            public virtual Task SetTaskContinuationAsync<TState>(Task previousTask, TState state, Func<object, Exception, TState, Task<object>> continuation)
            {
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    return continuation(null, null, state);
                }

                return InternalSetTaskContinuationAsync(previousTask, state, continuation);
            }

            private static async Task InternalSetTaskContinuationAsync<TState>(Task previousTask, TState state, Func<object, Exception, TState, Task<object>> continuation)
            {
                try
                {
                    await previousTask;
                }
                catch (Exception ex)
                {
                    await continuation(null, ex, state);
                    throw;
                }

                await continuation(null, null, state);
            }
        }
    }
}
