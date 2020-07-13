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
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation(object returnValue, Exception ex, Func<object, Exception, object> continuation)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                return GetContinuationFrom(returnTask.GetType()).SetTaskContinuation(returnTask, continuation);
            }

            return continuation(returnValue, ex);
        }

        /// <summary>
        /// Adds a continuation based on the current returnValue
        /// </summary>
        /// <param name="returnValue">Return value</param>
        /// <param name="ex">Exception</param>
        /// <param name="continuation">Continuation delegate</param>
        /// <returns>Return value after the continuation</returns>
        public static object AddContinuation(object returnValue, Exception ex, Func<object, Exception, Task<object>> continuation)
        {
            if (returnValue is Task returnTask && ex is null)
            {
                return GetContinuationFrom(returnTask.GetType()).SetTaskContinuationAsync(returnTask, continuation);
            }

            SynchronizationContext currentContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                return continuation(returnValue, ex).GetAwaiter().GetResult();
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
            public override Task SetTaskContinuation(Task previousTask, Func<object, Exception, object> continuation)
                => SetTaskContinuation((Task<TResult>)previousTask, continuation);

            public override Task SetTaskContinuationAsync(Task previousTask, Func<object, Exception, Task<object>> continuation)
                => SetTaskContinuationAsync((Task<TResult>)previousTask, continuation);

            private static async Task<TResult> SetTaskContinuation(Task<TResult> previousTask, Func<object, Exception, object> continuation)
            {
                TResult result = default;
                try
                {
                    result = await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    continuation(result, ex);
                    throw;
                }

                return (TResult)continuation(result, null);
            }

            private static async Task<TResult> SetTaskContinuationAsync(Task<TResult> previousTask, Func<object, Exception, Task<object>> continuation)
            {
                TResult result = default;
                try
                {
                    result = await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await continuation(result, ex).ConfigureAwait(false);
                    throw;
                }

                return (TResult)await continuation(result, null).ConfigureAwait(false);
            }
        }

        internal class TaskContinuationGenerator
        {
            public virtual async Task SetTaskContinuation(Task previousTask, Func<object, Exception, object> continuation)
            {
                try
                {
                    await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    continuation(null, ex);
                    throw;
                }

                continuation(null, null);
            }

            public virtual async Task SetTaskContinuationAsync(Task previousTask, Func<object, Exception, Task<object>> continuation)
            {
                try
                {
                    await previousTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await continuation(null, ex).ConfigureAwait(false);
                    throw;
                }

                await continuation(null, null).ConfigureAwait(false);
            }
        }
    }
}
