using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    internal static class AsyncTool
    {
        private static readonly ConcurrentDictionary<Type, TaskContinuationGenerator> ContinuationsGeneratorCache = new ConcurrentDictionary<Type, TaskContinuationGenerator>();

        /// <summary>
        /// Adds a continuation based on the current returnValue
        /// </summary>
        /// <typeparam name="TState">Type of the state</typeparam>
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <param name="state">State value</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation<TState>(object returnValue, Exception ex, Func<object, Exception, TState, object> continuation, TState state)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                return GetContinuationFrom(returnTask.GetType()).SetTaskContinuation(returnTask, continuation, state);
            }

            return continuation(returnValue, ex, state);
        }

        /// <summary>
        /// Adds a continuation based on the current returnValue
        /// </summary>
        /// <typeparam name="TState">Type of the state</typeparam>
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <param name="state">State value</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation<TState>(object returnValue, Exception ex, Func<object, Exception, TState, Task<object>> continuation, TState state)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                return GetContinuationFrom(returnTask.GetType()).SetTaskContinuationAsync(returnTask, continuation, state);
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
            public override Task SetTaskContinuation<TState>(Task previousTask, Func<object, Exception, TState, object> continuation, TState state)
                => SetTaskContinuation((Task<TResult>)previousTask, continuation, state);

            public override Task SetTaskContinuationAsync<TState>(Task previousTask, Func<object, Exception, TState, Task<object>> continuation, TState state)
                => SetTaskContinuationAsync((Task<TResult>)previousTask, continuation, state);

            private static async Task<TResult> SetTaskContinuation<TState>(Task<TResult> previousTask, Func<object, Exception, TState, object> continuation, TState state)
            {
                TResult result = default;
                try
                {
                    result = await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    continuation(result, ex, state);
                    throw;
                }

                return (TResult)continuation(result, null, state);
            }

            private static async Task<TResult> SetTaskContinuationAsync<TState>(Task<TResult> previousTask, Func<object, Exception, TState, Task<object>> continuation, TState state)
            {
                TResult result = default;
                try
                {
                    result = await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await continuation(result, ex, state).ConfigureAwait(false);
                    throw;
                }

                return (TResult)await continuation(result, null, state).ConfigureAwait(false);
            }
        }

        internal class TaskContinuationGenerator
        {
            public virtual async Task SetTaskContinuation<TState>(Task previousTask, Func<object, Exception, TState, object> continuation, TState state)
            {
                try
                {
                    await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    continuation(null, ex, state);
                    throw;
                }

                continuation(null, null, state);
            }

            public virtual async Task SetTaskContinuationAsync<TState>(Task previousTask, Func<object, Exception, TState, Task<object>> continuation, TState state)
            {
                try
                {
                    await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await continuation(null, ex, state).ConfigureAwait(false);
                    throw;
                }

                await continuation(null, null, state).ConfigureAwait(false);
            }
        }
    }
}
