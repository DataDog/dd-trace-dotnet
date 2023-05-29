// <copyright file="NullableStringHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class NullableStringHelper<TMarkerType>
    {
        private static readonly Type NullableStringType;

        static NullableStringHelper()
        {
            NullableStringType = typeof(TMarkerType).Assembly.GetType("NullableString")!;
        }

        /// <summary>
        /// Creates a NullableString instance using the provided string
        /// </summary>
        public static object CreateNullableString(string value)
        {
            var nullableString = Activator.CreateInstance(NullableStringType)!;
            nullableString.DuckCast<INullableString>().Value = value;
            return nullableString;
        }
    }
}
#endif
