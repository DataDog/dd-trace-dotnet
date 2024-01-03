// <copyright file="FuncWrappers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Helpers.Reflection;

internal static class FuncWrappers
{
    private const string DynMethodSuffix = "_dd_dyn";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FuncWrappers));

    public class FuncWrapper<T1, TRes>(string methodSignature) : MethodWrapper(methodSignature)
    {
        private Func<T1, TRes?>? _func;

        public TRes? Invoke(T1 arg1)
        {
            _func ??= GetFunc(ResolveMethod(arg1));
            return _func.Invoke(arg1);
        }

        private static Func<T1, TRes?> GetFunc(MethodInfo method)
        {
            Func<T1, TRes?>? res = null;
            try
            {
                var dynMethod = new DynamicMethod(method.Name + DynMethodSuffix, typeof(TRes), new Type[] { typeof(T1) }, method.DeclaringType! /* weird */, skipVisibility: true);
                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.EmitCall(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method, optionalParameterTypes: null);
                il.Emit(OpCodes.Ret);
                res = (Func<T1, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, TRes>));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to create dynamic method for {0}", method);
            }

            if (res == null)
            {
                res = (arg1) =>
                {
                    if (method.IsStatic)
                    {
                        // Check if he method is generic
                        if (method.IsGenericMethod)
                        {
                            method = method.MakeGenericMethod(typeof(T1));
                        }

                        return (TRes?)method.Invoke(obj: null, new object[] { arg1! });
                    }

                    return (TRes?)method.Invoke(arg1, Array.Empty<object>())!;
                };
            }

            return res;
        }
    }

    public class FuncWrapper<T1, T2, TRes>(string methodSignature) : MethodWrapper(methodSignature)
    {
        private Func<T1, T2, TRes?>? _func;

        public TRes? Invoke(T1 arg1, T2 arg2)
        {
            _func ??= GetFunc(ResolveMethod(arg1));
            return _func.Invoke(arg1, arg2);
        }

        private static Func<T1, T2, TRes?> GetFunc(MethodInfo method)
        {
            Func<T1, T2, TRes?>? res = null;
            try
            {
                var dynMethod = new DynamicMethod(method.Name + DynMethodSuffix, typeof(TRes), new Type[] { typeof(T1), typeof(T2) }, method.DeclaringType! /* weird */, skipVisibility: true);
                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.EmitCall(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method, optionalParameterTypes: null);
                il.Emit(OpCodes.Ret);
                res = (Func<T1, T2, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, T2, TRes>));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to create dynamic method for {0}", method);
            }

            if (res == null)
            {
                res = (arg1, arg2) =>
                {
                    if (method.IsStatic)
                    {
                        // Check if he method is generic
                        if (method.IsGenericMethod)
                        {
                            method = method.MakeGenericMethod(typeof(T1));
                        }

                        return (TRes?)method.Invoke(obj: null, new object[] { arg1!, arg2! });
                    }

                    return (TRes?)method.Invoke(arg1, new object[] { arg2! })!;
                };
            }

            return res;
        }
    }
}
