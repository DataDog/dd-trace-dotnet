using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Sigil;

namespace Datadog.Trace.ClrProfiler.Emit
{
    /// <summary>
    /// Helper class to instances of <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/>.
    /// </summary>
    /// <typeparam name="TDelegate">The type of delegate</typeparam>
    internal static class DynamicMethodBuilder<TDelegate>
        where TDelegate : Delegate
    {
        private static readonly ConcurrentDictionary<Key, TDelegate> _cached = new ConcurrentDictionary<Key, TDelegate>(new KeyComparer());

        /// <summary>
        /// Memoizes CreateMethodCallDelegate
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="returnType">The method's return type.</param>
        /// <param name="methodParameterTypes">optional types for the method parameters</param>
        /// <param name="methodGenericArguments">optional generic type arguments for a generic method</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate GetOrCreateMethodCallDelegate(
            Type type,
            string methodName,
            Type returnType = null,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            return _cached.GetOrAdd(
                new Key(type, methodName, returnType, methodParameterTypes, methodGenericArguments),
                key => CreateMethodCallDelegate(
                    key.Type,
                    key.MethodName,
                    key.MethodParameterTypes,
                    key.MethodGenericArguments));
        }

        /// <summary>
        /// Creates a simple <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/> that
        /// calls a method with the specified name and parameter types.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method to call when the returned delegate is executed..</param>
        /// <param name="methodName">The name of the method to call when the returned delegate is executed.</param>
        /// <param name="methodParameterTypes">If not null, use method overload that matches the specified parameters.</param>
        /// <param name="methodGenericArguments">If not null, use method overload that has the same number of generic arguments.</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate CreateMethodCallDelegate(
            Type type,
            string methodName,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            Type delegateType = typeof(TDelegate);
            Type[] genericTypeArguments = delegateType.GenericTypeArguments;

            Type[] parameterTypes;
            Type returnType;

            if (delegateType.Name.StartsWith("Func`"))
            {
                // last generic type argument is the return type
                int parameterCount = genericTypeArguments.Length - 1;
                parameterTypes = new Type[parameterCount];
                Array.Copy(genericTypeArguments, parameterTypes, parameterCount);

                returnType = genericTypeArguments[parameterCount];
            }
            else if (delegateType.Name.StartsWith("Action`"))
            {
                parameterTypes = genericTypeArguments;
                returnType = typeof(void);
            }
            else
            {
                throw new Exception($"Only Func<> or Action<> are supported in {nameof(CreateMethodCallDelegate)}.");
            }

            // find any method that matches by name and parameter types
            IEnumerable<MethodInfo> methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                                  .Where(m => m.Name == methodName);

            // if methodParameterTypes was specified, check for a method that matches
            if (methodParameterTypes != null)
            {
                methods = methods.Where(
                    m =>
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
                    });
            }

            if (methodGenericArguments != null)
            {
                methods = methods.Where(
                                      m => m.IsGenericMethodDefinition &&
                                           m.GetGenericArguments().Length == methodGenericArguments.Length)
                                 .ToArray();
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

            Type[] effectiveParameterTypes;

            IEnumerable<Type> reflectedParameterTypes = methodInfo.GetParameters()
                                                                  .Select(p => p.ParameterType);
            if (methodInfo.IsStatic)
            {
                effectiveParameterTypes = reflectedParameterTypes.ToArray();
            }
            else
            {
                // for instance methods, insert object's type as first element in array
                effectiveParameterTypes = new[] { type }
                                         .Concat(reflectedParameterTypes)
                                         .ToArray();
            }

            Emit<TDelegate> dynamicMethod = Emit<TDelegate>.NewDynamicMethod(methodInfo.Name);

            // load each argument and cast or unbox as necessary
            for (ushort argumentIndex = 0; argumentIndex < parameterTypes.Length; argumentIndex++)
            {
                Type delegateParameterType = parameterTypes[argumentIndex];
                Type underlyingParameterType = effectiveParameterTypes[argumentIndex];

                dynamicMethod.LoadArgument(argumentIndex);

                if (underlyingParameterType.IsValueType && delegateParameterType == typeof(object))
                {
                    dynamicMethod.UnboxAny(underlyingParameterType);
                }
                else if (underlyingParameterType != delegateParameterType)
                {
                    dynamicMethod.CastClass(underlyingParameterType);
                }
            }

            if (methodInfo.IsStatic)
            {
                dynamicMethod.Call(methodInfo);
            }
            else
            {
                // C# compiler always uses CALLVIRT for instance methods
                // to get the cheap null check, even if they are not virtual
                dynamicMethod.CallVirtual(methodInfo);
            }

            // Non-void return type?
            if (methodInfo.ReturnType.IsValueType && returnType == typeof(object))
            {
                dynamicMethod.Box(methodInfo.ReturnType);
            }
            else if (methodInfo.ReturnType != returnType)
            {
                dynamicMethod.CastClass(returnType);
            }

            dynamicMethod.Return();
            return dynamicMethod.CreateDelegate();
        }

        private struct Key
        {
            public readonly Type Type;
            public readonly string MethodName;
            public readonly Type ReturnType;
            public readonly Type[] MethodParameterTypes;
            public readonly Type[] MethodGenericArguments;

            public Key(Type type, string methodName, Type returnType, Type[] methodParameterTypes, Type[] methodGenericArguments)
            {
                Type = type;
                MethodName = methodName;
                ReturnType = returnType;
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

                if (!object.Equals(x.ReturnType, y.ReturnType))
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
