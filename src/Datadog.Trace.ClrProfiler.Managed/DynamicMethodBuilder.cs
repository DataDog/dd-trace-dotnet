using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.ClrProfiler
{
    public static class DynamicMethodBuilder
    {
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
