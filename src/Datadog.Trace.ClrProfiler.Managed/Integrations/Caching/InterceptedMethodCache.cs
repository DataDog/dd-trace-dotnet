using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Wrapper class for retrieving instrumented methods and caching them.
    /// Necessary because our profiling implementation treats all non <see cref="ValueType"/> types as equal for method signature purposes.
    /// Uses the actual types of arguments from the calling method to differentiate between signatures and caches them.
    /// </summary>
    /// <typeparam name="TDelegate">The type of delegate being intercepted.</typeparam>
    internal class InterceptedMethodCache<TDelegate>
        where TDelegate : Delegate
    {
        private readonly ConcurrentDictionary<string, TDelegate> _methodCache = new ConcurrentDictionary<string, TDelegate>();

        internal TDelegate GetInterceptedMethod(
            Assembly assembly,
            string owningType,
            string methodName,
            Type[] generics,
            Type[] parameters)
        {
            var methodKey = Interception.MethodKey(genericTypes: generics, parameterTypes: parameters);

            if (!_methodCache.TryGetValue(methodKey, out var method))
            {
                var type = assembly.GetType(owningType);

                method = Emit.DynamicMethodBuilder<TDelegate>.CreateMethodCallDelegate(
                    type,
                    methodName,
                    methodParameterTypes: parameters,
                    methodGenericArguments: generics);

                AddToCache(methodKey, method);
            }

            return method;
        }

        private void AddToCache(string key, TDelegate method)
        {
            _methodCache.AddOrUpdate(key, (k) => method, (k, m) => method);
        }
    }
}
