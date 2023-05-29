// <copyright file="NullableStringHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class NullableStringHelper<TMarkerType>
    {
        private static readonly Func<object> Activator;

        static NullableStringHelper()
        {
            var nullableStringType = typeof(TMarkerType).Assembly.GetType("NullableString")!;

            ConstructorInfo ctor = nullableStringType.GetConstructor(System.Type.EmptyTypes)!;

            DynamicMethod createHeadersMethod = new DynamicMethod(
                $"NullableStringHelper",
                nullableStringType,
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);

            ILGenerator il = createHeadersMethod.GetILGenerator();
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);

            Activator = (Func<object>)createHeadersMethod.CreateInstanceDelegate(typeof(Func<object>));
        }

        /// <summary>
        /// Creates a NullableString instance using the provided string
        /// </summary>
        public static object CreateNullableString(string value)
        {
            var nullableString = Activator();
            nullableString.DuckCast<INullableString>().Value = value;
            return nullableString;
        }
    }
}
#endif
