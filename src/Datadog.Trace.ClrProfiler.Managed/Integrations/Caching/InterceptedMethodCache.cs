using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class InterceptedMethodCache<T>
        where T : class
    {
        private readonly ConcurrentDictionary<MethodBase, string> _keyCache = new ConcurrentDictionary<MethodBase, string>();
        private readonly ConcurrentDictionary<string, T> _methodCache = new ConcurrentDictionary<string, T>();

        internal InterceptedMethodCache()
        {
            bool isDelegateType = typeof(Delegate).IsAssignableFrom(typeof(T));
            if (!isDelegateType)
            {
                throw new ArgumentException($"{typeof(T).Name} is not a delegate type");
            }
        }

        internal bool TryGet(string key, out T method)
        {
            return _methodCache.TryGetValue(key, out method);
        }

        internal void Cache(string key, T method)
        {
            _methodCache.AddOrUpdate(key, (k) => method, (k, m) => method);
        }

        internal string GetMethodKey(params Type[] parameterTypes)
        {
            return GetMethodKeyInternal(parameterTypes);
        }

        private string GetMethodKeyInternal(Type[] parameterTypes)
        {
            var key = "m";

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                key = string.Concat(key, $"_{parameterTypes[i].FullName}");
            }

            return key;
        }
    }
}
