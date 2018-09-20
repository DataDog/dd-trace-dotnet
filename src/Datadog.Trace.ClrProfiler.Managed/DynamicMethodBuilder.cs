using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Helper class to instances of <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/>.
    /// </summary>
    public static class DynamicMethodBuilder
    {
        /// <summary>
        /// Creates a simple <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/> that
        /// calls a method with the specified name and and parameter types.
        /// </summary>
        /// <typeparam name="TDelegate">A <see cref="Delegate"/> type with the signature of the method to call.</typeparam>
        /// <param name="type">The <see cref="Type"/> that contains the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="methodParameterTypes">optional types for the method parameters</param>
        /// <param name="methodGenericArguments">optional generic type arguments for a generic method</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate CreateMethodCallDelegate<TDelegate>(
            Type type,
            string methodName,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
            where TDelegate : Delegate
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
    }
}
