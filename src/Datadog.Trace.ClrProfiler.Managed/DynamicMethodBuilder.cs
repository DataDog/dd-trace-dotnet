using System;
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
        /// <param name="returnType">The return <see cref="Type"/> of the method.</param>
        /// <param name="parameterTypes">An array with the <see cref="Type"/> of each of the method's parameters, in order.</param>
        /// <param name="isVirtual"><c>true</c> if the dyanmic method should use a virtual method call, <c>false</c> otherwise.</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static Delegate CreateMethodCallDelegate<TDelegate>(
            Type type,
            string methodName,
            Type returnType,
            Type[] parameterTypes,
            bool isVirtual)
        {
            MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);

            if (methodInfo == null)
            {
                // method not found
                // TODO: logging
                return null;
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

            return dynamicMethod.CreateDelegate(typeof(TDelegate));
        }
    }
}
