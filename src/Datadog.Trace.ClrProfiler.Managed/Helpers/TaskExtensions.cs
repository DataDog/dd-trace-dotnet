using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class TaskExtensions
    {
        private static readonly ConcurrentDictionary<Type, Func<Task<object>, object>> Converters = new ConcurrentDictionary<Type, Func<Task<object>, object>>();

        public static object Cast(this Task<object> parent, Type taskResultType)
        {
            var converter = Converters.GetOrAdd(
                taskResultType,
                type =>
                {
                    var methodInfo = typeof(TaskExtensions)
                        .GetMethod(nameof(ConvertTaskImpl), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .MakeGenericMethod(type);

                    return (Func<Task<object>, object>)methodInfo.CreateDelegate(typeof(Func<Task<object>, object>));
                });

            return converter(parent);
        }

        private static async Task<T> ConvertTaskImpl<T>(Task<object> parent)
        {
            return (T)await parent.ConfigureAwait(false);
        }
    }
}
