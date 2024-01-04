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
            _func ??= IsCtor ? GetFunc(ResolveCtor()) : GetFunc(ResolveMethod(arg1));
            return _func.Invoke(arg1);
        }

        private static Func<T1, TRes?> GetFunc(MethodInfo method)
        {
            Func<T1, TRes?>? res = null;
            try
            {
                var dynMethod = new DynamicMethod(method.Name + DynMethodSuffix, typeof(TRes), new[] { typeof(T1) }, method.DeclaringType!, true);
                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
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
                res = arg1 =>
                {
                    if (!method.IsStatic)
                    {
                        return (TRes?)method.Invoke(arg1, Array.Empty<object>())!;
                    }

                    // Check if the method is generic
                    if (method.IsGenericMethodDefinition)
                    {
                        method = method.MakeGenericMethod(typeof(T1));
                    }

                    return (TRes?)method.Invoke(obj: null, new object[] { arg1! });
                };
            }

            return res;
        }

        private static Func<T1, TRes?> GetFunc(ConstructorInfo ctor)
        {
            Func<T1, TRes?>? res = null;
            try
            {
                var dynMethod = new DynamicMethod(ctor.Name + DynMethodSuffix, typeof(TRes), new Type[] { typeof(T1) }, ctor.DeclaringType!, true);
                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
                res = (Func<T1, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, TRes>));
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }

            if (res == null)
            {
                res = (arg1) =>
                {
                    return (TRes)ctor.Invoke(new object[] { arg1! });
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
                    if (!method.IsStatic)
                    {
                        return (TRes)method.Invoke(arg1, new object[] { arg2! })!;
                    }

                    // Check if the method is generic
                    if (method.IsGenericMethodDefinition)
                    {
                        method = method.MakeGenericMethod(typeof(T1));
                    }

                    return (TRes?)method.Invoke(obj: null, new object[] { arg1!, arg2! });
                };
            }

            return res;
        }
    }
}
