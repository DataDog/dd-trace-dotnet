using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal static class ContinuationsHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Type GetResultType(Type parentType)
        {
            Type currentType = parentType;
            while (currentType != null)
            {
                Type[] typeArguments = currentType.GenericTypeArguments ?? Type.EmptyTypes;
                switch (typeArguments.Length)
                {
                    case 0:
                        return typeof(object);
                    case 1:
                        return typeArguments[0];
                    default:
                        currentType = currentType.BaseType;
                        break;
                }
            }

            return typeof(object);
        }

#if NETCOREAPP3_1 || NET5_0
#else
        internal static TTo Convert<TFrom, TTo>(TFrom value)
        {
            return Converter<TFrom, TTo>.Convert(value);
        }

        private static class Converter<TFrom, TTo>
        {
            private static readonly ConvertDelegate _converter;

            static Converter()
            {
                DynamicMethod dMethod = new DynamicMethod($"Converter<{typeof(TFrom).Name},{typeof(TTo).Name}>", typeof(TTo), new[] { typeof(TFrom) }, typeof(ConvertDelegate).Module, true);
                ILGenerator il = dMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ret);
                _converter = (ConvertDelegate)dMethod.CreateDelegate(typeof(ConvertDelegate));
            }

            private delegate TTo ConvertDelegate(TFrom value);

            public static TTo Convert(TFrom value)
            {
                return _converter(value);
            }
        }
#endif
    }
}
