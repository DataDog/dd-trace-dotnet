using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Helper class to instances of <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/>.
    /// </summary>
    /// <typeparam name="TDelegate">The type of delegate</typeparam>
    public static class DynamicMethodBuilder<TDelegate>
            where TDelegate : Delegate
    {
        private static ConcurrentDictionary<Key, TDelegate> _cached = new ConcurrentDictionary<Key, TDelegate>(new KeyComparer());

        /// <summary>
        /// Memoizes CreateMethodCallDelegate
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="methodParameterTypes">optional types for the method parameters</param>
        /// <param name="methodGenericArguments">optional generic type arguments for a generic method</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate GetOrCreateMethodCallDelegate(
            Type type,
            string methodName,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            return _cached.GetOrAdd(
                new Key(type, methodName, methodParameterTypes, methodGenericArguments),
                key =>
                {
                    return CreateMethodCallDelegate(key.Type, key.MethodName, key.MethodParameterTypes, key.MethodGenericArguments);
                });
        }

        /// <summary>
        /// Creates a simple <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/> that
        /// calls a method with the specified name and and parameter types.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="methodParameterTypes">optional types for the method parameters</param>
        /// <param name="methodGenericArguments">optional generic type arguments for a generic method</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate CreateMethodCallDelegate(
            Type type,
            string methodName,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            Type delegateType = typeof(TDelegate);
            Type[] genericTypeArguments = delegateType.GenericTypeArguments;

            Type returnType;
            Type[] parameterTypes;

            if (delegateType.Name.StartsWith("Func`"))
            {
                // last generic type argument is the return type
                returnType = genericTypeArguments.Last();
                parameterTypes = genericTypeArguments.Take(genericTypeArguments.Length - 1).ToArray();
            }
            else if (delegateType.Name.StartsWith("Action`"))
            {
                returnType = typeof(void);
                parameterTypes = genericTypeArguments;
            }
            else
            {
                throw new Exception($"Only Func<> or Action<> are supported in {nameof(CreateMethodCallDelegate)}.");
            }

            // find any method that matches by name and parameter types
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            methods = methods.Where(m => m.Name == methodName).ToArray();
            if (methodParameterTypes != null)
            {
                methods = methods.Where(m =>
                {
                    var ps = m.GetParameters();
                    if (ps.Length != methodParameterTypes.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < ps.Length; i++)
                    {
                        var t1 = ps[i].ParameterType;
                        var t2 = methodParameterTypes[i];

                        // generics can be tricky to compare for type equality
                        // so we will just check the namespace and name
                        if (t1.Namespace != t2.Namespace || t1.Name != t2.Name)
                        {
                            return false;
                        }
                    }

                    return true;
                }).ToArray();
            }

            if (methodGenericArguments != null)
            {
                methods = methods.Where(m => m.IsGenericMethodDefinition).ToArray();
            }

            MethodInfo methodInfo = methods.FirstOrDefault();
            if (methodInfo == null)
            {
                // method not found
                // TODO: logging
                return null;
            }

            if (methodGenericArguments != null)
            {
                methodInfo = methodInfo.MakeGenericMethod(methodGenericArguments);
            }

            var dynamicMethod = new DynamicMethod(methodName, returnType, parameterTypes, type);
            ILGenerator ilGenerator = dynamicMethod.GetILGenerator();

            for (int argumentIndex = 0; argumentIndex < parameterTypes.Length; argumentIndex++)
            {
                if (argumentIndex == 0)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                }
                else if (argumentIndex == 1)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_1);
                }
                else if (argumentIndex == 2)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_2);
                }
                else if (argumentIndex == 3)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_3);
                }
                else if (argumentIndex < 256)
                {
                    ilGenerator.Emit(OpCodes.Ldarg_S, (byte)argumentIndex);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldarg, argumentIndex);
                }
            }

            ilGenerator.Emit(methodInfo.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, methodInfo);
            ilGenerator.Emit(OpCodes.Ret);

            return (TDelegate)dynamicMethod.CreateDelegate(delegateType);
        }

        private struct Key
        {
            public Type Type;
            public string MethodName;
            public Type[] MethodParameterTypes;
            public Type[] MethodGenericArguments;

            public Key(Type type, string methodName, Type[] methodParameterTypes, Type[] methodGenericArguments)
            {
                Type = type;
                MethodName = methodName;
                MethodParameterTypes = methodParameterTypes;
                MethodGenericArguments = methodGenericArguments;
            }
        }

        private class KeyComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key x, Key y)
            {
                if (!object.Equals(x.Type, y.Type))
                {
                    return false;
                }

                if (!object.Equals(x.MethodName, y.MethodName))
                {
                    return false;
                }

                if (!(x.MethodParameterTypes == null && y.MethodParameterTypes == null))
                {
                    if (x.MethodParameterTypes == null || y.MethodParameterTypes == null)
                    {
                        return false;
                    }

                    if (x.MethodParameterTypes.Except(y.MethodParameterTypes).Any())
                    {
                        return false;
                    }
                }

                if (!(x.MethodGenericArguments == null && y.MethodGenericArguments == null))
                {
                    if (x.MethodGenericArguments == null || y.MethodGenericArguments == null)
                    {
                        return false;
                    }

                    if (x.MethodGenericArguments.Except(y.MethodGenericArguments).Any())
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(Key obj)
            {
                unchecked
                {
                    int hash = 17;

                    if (obj.Type != null)
                    {
                        hash = (hash * 23) + obj.Type.GetHashCode();
                    }

                    if (obj.MethodName != null)
                    {
                        hash = (hash * 23) + obj.MethodName.GetHashCode();
                    }

                    if (obj.MethodParameterTypes != null)
                    {
                        foreach (var t in obj.MethodParameterTypes)
                        {
                            if (t != null)
                            {
                                hash = (hash * 23) + t.GetHashCode();
                            }
                        }
                    }

                    if (obj.MethodGenericArguments != null)
                    {
                        foreach (var t in obj.MethodGenericArguments)
                        {
                            if (t != null)
                            {
                                hash = (hash * 23) + t.GetHashCode();
                            }
                        }
                    }

                    return hash;
                }
            }
        }
    }
}
