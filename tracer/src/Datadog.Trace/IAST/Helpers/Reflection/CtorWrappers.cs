// <copyright file="CtorWrappers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.Iast.Helpers.Reflection;

internal static class CtorWrappers
{
    private const string DynMethodSuffix = "_dd_dyn";

    public class CtorWrapper<T1, TRes>(string methodSignature) : MethodWrapper(methodSignature)
    {
        private Func<T1, TRes?>? _func;

        public TRes? Invoke(T1 arg1)
        {
            _func ??= Func(ResolveCtor());
            return _func.Invoke(arg1);
        }

        private static Func<T1, TRes?> Func(ConstructorInfo ctor)
        {
            var dynMethod = new DynamicMethod(ctor.Name + DynMethodSuffix, typeof(TRes), new[] { typeof(T1) }, ctor.DeclaringType!, true);
            var il = dynMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);
            return (Func<T1, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, TRes>));
        }
    }
}
