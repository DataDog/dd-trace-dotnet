using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class AsyncHelper
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> MethodCache = new ConcurrentDictionary<string, MethodInfo>();

        internal static object InvokeGenericTaskDelegate(
            Type owningType,
            Type taskResultType,
            string nameOfIntegrationMethod,
            Type integrationType,
            params object[] parametersToPass)
        {
            var methodKey =
                Interception.MethodKey(
                    owningType: owningType,
                    returnType: taskResultType,
                    genericTypes: Interception.NullTypeArray,
                    parameterTypes: Interception.ParamsToTypes(parametersToPass));

            var asyncDelegate =
                MethodCache.GetOrAdd(methodKey, _ => GetGenericAsyncMethodInfo(taskResultType, nameOfIntegrationMethod, integrationType));

            return asyncDelegate.Invoke(null, parametersToPass);
        }

        internal static object InvokeGenericTaskDelegateWithExplicitParameterTypes(
            Type owningType,
            Type taskResultType,
            string nameOfIntegrationMethod,
            Type integrationType,
            Type[] parameterTypes,
            params object[] parametersToPass)
        {
            var methodKey =
                Interception.MethodKey(
                    owningType: owningType,
                    returnType: taskResultType,
                    genericTypes: Interception.NullTypeArray,
                    parameterTypes: parameterTypes);

            var asyncDelegate =
                MethodCache.GetOrAdd(methodKey, _ => GetGenericAsyncMethodInfo(taskResultType, nameOfIntegrationMethod, integrationType));

            return asyncDelegate.Invoke(null, parametersToPass);
        }

        private static MethodInfo GetGenericAsyncMethodInfo(Type taskResultType, string nameOfIntegrationMethod, Type integrationType)
        {
            var method = integrationType.GetMethod(nameOfIntegrationMethod, BindingFlags.Static | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new ArgumentException($"Method {nameOfIntegrationMethod} not found on {integrationType.Name} for async delegate access. ");
            }

            if (method.IsStatic == false)
            {
                throw new ArgumentException($"Method {nameOfIntegrationMethod} on {integrationType.Name} must be static. ");
            }

            if (method.ReturnType.Name != typeof(Task<>).Name)
            {
                throw new ArgumentException($"Method {nameOfIntegrationMethod} on {integrationType.Name} must have a return type of Task<>. ");
            }

            return method.MakeGenericMethod(taskResultType);
        }
    }
}
