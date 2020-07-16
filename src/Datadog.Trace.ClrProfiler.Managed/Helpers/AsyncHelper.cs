using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class AsyncHelper
    {
        private static readonly ConcurrentDictionary<Key, MethodInfo> MethodCache = new ConcurrentDictionary<Key, MethodInfo>();

        internal static object InvokeGenericTaskDelegate(
            Type owningType,
            Type taskResultType,
            string nameOfIntegrationMethod,
            Type integrationType,
            params object[] parametersToPass)
        {
            var methodKey = new Key(
                owningType,
                taskResultType,
                Interception.NullTypeArray,
                Interception.ParamsToTypes(parametersToPass),
                nameOfIntegrationMethod,
                integrationType);

            var asyncDelegate =
                MethodCache.GetOrAdd(methodKey, key => GetGenericAsyncMethodInfo(key.ReturnType, key.IntegrationName, key.IntegrationType));

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

        private readonly struct Key : IEquatable<Key>
        {
            public readonly Type OwningType;
            public readonly Type ReturnType;

            public readonly Type[] GenericTypes;
            public readonly Type[] ParameterTypes;

            public readonly string IntegrationName;
            public readonly Type IntegrationType;

            public Key(Type owningType, Type returnType, Type[] genericTypes, Type[] parameterTypes, string integrationName, Type integrationType)
            {
                OwningType = owningType;
                ReturnType = returnType;
                GenericTypes = genericTypes;
                ParameterTypes = parameterTypes;
                IntegrationName = integrationName;
                IntegrationType = integrationType;
            }

            public bool Equals(Key other)
            {
                return Equals(OwningType, other.OwningType)
                    && Equals(ReturnType, other.ReturnType)
                    && ArrayEquals(GenericTypes, other.GenericTypes)
                    && ArrayEquals(ParameterTypes, other.ParameterTypes);
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (OwningType != null ? OwningType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (ReturnType != null ? ReturnType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ GetHashCode(GenericTypes);
                    hashCode = (hashCode * 397) ^ GetHashCode(ParameterTypes);
                    return hashCode;
                }
            }

            private static int GetHashCode(Type[] array)
            {
                if (array == null)
                {
                    return 0;
                }

                int value = array.Length;

                for (int i = 0; i < array.Length; i++)
                {
                    value = unchecked((value * 31) + array[i]?.GetHashCode() ?? 0);
                }

                return value;
            }

            private static bool ArrayEquals(Type[] array1, Type[] array2)
            {
                if (array1 == null)
                {
                    return array2 == null;
                }

                if (array2 == null)
                {
                    return false;
                }

                if (array1.Length != array2.Length)
                {
                    return false;
                }

                for (int i = 0; i < array1.Length; i++)
                {
                    if (array1[i] != array2[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
