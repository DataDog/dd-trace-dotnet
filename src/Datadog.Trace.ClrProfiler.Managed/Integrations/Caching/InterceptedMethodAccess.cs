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
    internal class InterceptedMethodAccess<TDelegate>
        where TDelegate : Delegate
    {
        private readonly ConcurrentDictionary<string, TDelegate> _methodCache = new ConcurrentDictionary<string, TDelegate>();

        /// <summary>
        /// Attempts to retrieve a method from cache, otherwise creates the delegate reference, adds it to the cache, and then returns it.
        /// </summary>
        /// <param name="assembly">Assembly containing the method.</param>
        /// <param name="owningType">Type which owns the method.</param>
        /// <param name="methodName">Name of the method being instrumented.</param>
        /// <param name="returnType">The return type of the instrumented method.</param>
        /// <param name="generics">The ordered types of the method's generics.</param>
        /// <param name="parameters">The ordered types of the method's parameters.</param>
        /// <returns>Delegate representing instrumented method.</returns>
        internal TDelegate GetInterceptedMethod(
            Assembly assembly,
            string owningType,
            string methodName,
            Type returnType,
            Type[] generics,
            Type[] parameters)
        {
            var methodKey = Interception.MethodKey(returnType: returnType, genericTypes: generics, parameterTypes: parameters);

            return
                _methodCache.GetOrAdd(
                    methodKey,
                    key =>
                    {
                        var type = assembly.GetType(owningType);

                        return Emit.DynamicMethodBuilder<TDelegate>.CreateInstrumentedMethodDelegate(
                            owningType: type,
                            methodName: methodName,
                            returnType: returnType,
                            parameterTypes: parameters,
                            genericTypes: generics);
                    });
        }

        /// <summary>
        /// Attempts to retrieve a method from cache, otherwise creates the delegate reference, adds it to the cache, and then returns it.
        /// </summary>
        /// <param name="owningType">Type which owns the method.</param>
        /// <param name="methodName">Name of the method being instrumented.</param>
        /// <param name="returnType">The return type of the instrumented method.</param>
        /// <param name="generics">The ordered types of the method's generics.</param>
        /// <param name="parameters">The ordered types of the method's parameters.</param>
        /// <returns>Delegate representing instrumented method.</returns>
        internal TDelegate GetInterceptedMethod(
            Type owningType,
            string methodName,
            Type returnType,
            Type[] generics,
            Type[] parameters)
        {
            var methodKey = Interception.MethodKey(returnType: returnType, genericTypes: generics, parameterTypes: parameters);

            return
                _methodCache.GetOrAdd(
                    methodKey,
                    key => Emit.DynamicMethodBuilder<TDelegate>.CreateInstrumentedMethodDelegate(
                        owningType: owningType,
                        methodName: methodName,
                        returnType: returnType,
                        parameterTypes: parameters,
                        genericTypes: generics));
        }
    }
}
