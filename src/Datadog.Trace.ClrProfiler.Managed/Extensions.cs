using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Extension methods
    /// </summary>
    public static class Extensions
    {
        private static ConcurrentDictionary<Type, Func<Span, Task, Action<Exception>, Task>> _traceTaskMethods = new ConcurrentDictionary<Type, Func<Span, Task, Action<Exception>, Task>>();

        /// <summary>
        /// Trace traces a function which returns an object with the given span. If the object is a task, the span will be finished when the task completes.
        /// </summary>
        /// <param name="span">The span to use for tracing. The scope for the span should not be set to finish on close.</param>
        /// <param name="func">The function to trace</param>
        /// <param name="onComplete">an optional function to call when the function/task completes. By default exceptions will be added to the span and the span will be finished.</param>
        /// <returns>The result of the function call</returns>
        public static object Trace(this Span span, Func<object> func, Action<Exception> onComplete = null)
        {
            onComplete = onComplete ?? new Action<Exception>(e =>
            {
                if (e != null)
                {
                    span.SetException(e);
                }
                span.Finish();
            });

            try
            {
                var result = func();

                if (result is Task task)
                {
                    var typ = task.GetType();
                    var genericArgs = GetGenericTypeArguments(typeof(Task<>), typ);
                    if (genericArgs.Length == 1)
                    {
                        if (!_traceTaskMethods.TryGetValue(genericArgs[0], out var traceTaskAsync))
                        {
                            traceTaskAsync = DynamicMethodBuilder<Func<Span, Task, Action<Exception>, Task>>.CreateMethodCallDelegate(typeof(Extensions), "TraceTaskAsync", methodGenericArguments: genericArgs);
                            _traceTaskMethods[genericArgs[0]] = traceTaskAsync;
                        }

                        result = traceTaskAsync(span, task, onComplete);
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

        private static Type[] GetGenericTypeArguments(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return toCheck.GetGenericArguments();
                }

                toCheck = toCheck.BaseType;
            }

            return null;
        }
    }
}
