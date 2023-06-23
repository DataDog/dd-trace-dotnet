// <copyright file="UnsafeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal static class UnsafeHelper
{
    private static readonly object Instance = new();

#if NETCOREAPP3_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo As<TFrom, TTo>(ref TFrom value)
    {
        return ref Unsafe.As<TFrom, TTo>(ref value);
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo As<TFrom, TTo>(ref TFrom value)
    {
        return Converter<TFrom, TTo>.Convert(ref value);
    }

    private static class Converter<TFrom, TTo>
    {
        private static readonly ConvertDelegate ConverterInstance;

        static Converter()
        {
            var dMethod = new DynamicMethod($"Converter<{typeof(TFrom).Name},{typeof(TTo).Name}>", typeof(TTo), new[] { typeof(object), typeof(TFrom) }, typeof(ConvertDelegate).Module, true);
            var il = dMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ret);
            ConverterInstance = (ConvertDelegate)dMethod.CreateDelegate(typeof(ConvertDelegate), Instance);
        }

        private delegate TTo ConvertDelegate(TFrom value);

        public static TTo Convert(ref TFrom value)
        {
            return ConverterInstance(value);
        }
    }
#endif
}
