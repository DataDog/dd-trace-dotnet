using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    internal static class AsyncTool
    {
        private static readonly ConcurrentDictionary<Type, ITaskContinuations> ContinuationsCache = new ConcurrentDictionary<Type, ITaskContinuations>();

        /// <summary>
        /// Task continuations interface
        /// </summary>
        internal interface ITaskContinuations
        {
            /// <summary>
            /// Sets a task continuation to the method delegate
            /// </summary>
            /// <param name="previousTask">Previous task</param>
            /// <param name="continuation">Continuation</param>
            /// <returns>Task with continuation</returns>
            Task SetTaskContinuation(Task previousTask, Func<object, Exception, object> continuation);

            /// <summary>
            /// Sets a task continuation to the method async delegate
            /// </summary>
            /// <param name="previousTask">Previous task</param>
            /// <param name="continuation">Async continuation</param>
            /// <returns>Task with continuation</returns>
            Task SetTaskContinuationAsync(Task previousTask, Func<object, Exception, Task<object>> continuation);
        }

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

        private static ITaskContinuations GetContinuationFrom(Type type)
        {
            return ContinuationsCache.GetOrAdd(type, tType =>
            {
                Type[] gTypeArgs = tType.GenericTypeArguments;
                if (gTypeArgs.Length == 0)
                {
                    return new TaskContinuations();
                }

                if (gTypeArgs.Length > 1 && tType.BaseType != null && tType.BaseType.BaseType == typeof(Task) && tType.BaseType.GenericTypeArguments?.Length == 1)
                {
                    gTypeArgs = tType.BaseType.GenericTypeArguments;
                }

                return (ITaskContinuations)Activator.CreateInstance(typeof(TaskContinuations<>).MakeGenericType(gTypeArgs[0]));
            });
        }

        internal class TaskContinuations<TResult> : ITaskContinuations
        {
            public Task SetTaskContinuation(Task previousTask, Func<object, Exception, object> continuation)
                => SetTaskContinuation((Task<TResult>)previousTask, continuation);

            public Task SetTaskContinuationAsync(Task previousTask, Func<object, Exception, Task<object>> continuation)
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

        internal class TaskContinuations : ITaskContinuations
        {
            public async Task SetTaskContinuation(Task previousTask, Func<object, Exception, object> continuation)
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

            public async Task SetTaskContinuationAsync(Task previousTask, Func<object, Exception, Task<object>> continuation)
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
