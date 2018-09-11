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
        /// <param name="isVirtual"><c>true</c> if the dynamic method should use a virtual method call, <c>false</c> otherwise.</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate CreateMethodCallDelegate<TDelegate>(
            Type type,
            string methodName,
            bool isVirtual)
            where TDelegate : Delegate
        {
            Type delegateType = typeof(TDelegate);
            Type[] genericTypeArguments = delegateType.GenericTypeArguments;
            Type returnType;
            Type[] parameterTypes;

            if (delegateType.Name.StartsWith("Func`"))
            {
                // last generic parameter
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

            MethodInfo methodInfo = type.GetMethod(methodName, parameterTypes);

            if (methodInfo == null)
            {
                // method not found
                // TODO: logging
                return default;
            }

            var dynamicMethod = new DynamicMethod(methodName, returnType, parameterTypes);
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

            ilGenerator.Emit(isVirtual ? OpCodes.Callvirt : OpCodes.Call, methodInfo);
            ilGenerator.Emit(OpCodes.Ret);

            return (TDelegate)dynamicMethod.CreateDelegate(delegateType);
        }
    }
}
