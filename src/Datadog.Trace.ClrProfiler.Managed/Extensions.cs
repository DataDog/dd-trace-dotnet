using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Extension methods
    /// </summary>
    public static class Extensions
    {
        private static readonly ConcurrentDictionary<Type, Func<Task, Action<Exception>, Task>> TraceTaskMethods =
            new ConcurrentDictionary<Type, Func<Task, Action<Exception>, Task>>();

        /// <summary>
        /// Trace traces a function which returns an object with the given span. If the object is a task, the span will be finished when the task completes.
        /// </summary>
        /// <param name="span">The span to use for tracing. The scope for the span should not be set to finish on close.</param>
        /// <param name="func">The function to trace</param>
        /// <param name="onComplete">an optional function to call when the function/task completes. By default exceptions will be added to the span and the span will be finished.</param>
        /// <returns>The result of the function call</returns>
        public static object Trace(this Span span, Func<object> func, Action<Exception> onComplete = null)
        {
            if (onComplete == null)
            {
                onComplete = e =>
                {
                    if (e != null)
                    {
                        span.SetException(e);
                    }

                    span.Finish();
                };
            }

            try
            {
                var result = func();
                var resultType = result.GetType();

                if (result is Task task)
                {
                    if (resultType.IsGenericType)
                    {
                        Type[] genericArgs = resultType.GenericTypeArguments;

                        if (!TraceTaskMethods.TryGetValue(genericArgs[0], out var traceTaskAsync))
                        {
                            traceTaskAsync = DynamicMethodBuilder<Func<Task, Action<Exception>, Task>>
                               .CreateMethodCallDelegate(
                                    typeof(Extensions),
                                    "TraceTaskAsync",
                                    methodGenericArguments: genericArgs);

                            TraceTaskMethods[genericArgs[0]] = traceTaskAsync;
                        }

                        result = traceTaskAsync(task, onComplete);
                    }
                    else
                    {
                        result = TraceTaskAsync(task, onComplete);
                    }
                }
                else
                {
                    onComplete(null);
                }

                return result;
            }
            catch (Exception e)
            {
                onComplete(e);
                throw;
            }
        }

        private static async Task TraceTaskAsync(Task task, Action<Exception> onComplete)
        {
            try
            {
                await task.ConfigureAwait(false);
                onComplete(null);
            }
            catch (Exception ex)
            {
                onComplete(ex);
                throw;
            }
        }

        private static async Task<T> TraceTaskAsync<T>(Task<T> task, Action<Exception> onComplete)
        {
            try
            {
                T result = await task.ConfigureAwait(false);
                onComplete(null);
                return result;
            }
            catch (Exception ex)
            {
                onComplete(ex);
                throw;
            }
        }
    }
}
