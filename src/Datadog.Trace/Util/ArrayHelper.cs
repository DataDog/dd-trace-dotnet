// <copyright file="ArrayHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Util
{
    internal static class ArrayHelper
    {
        public static T[] Empty<T>()
        {
#if NET45
            return EmptyArray<T>.Value;
#else
            return System.Array.Empty<T>();
#endif
        }

#if NET45
        private static class EmptyArray<T>
        {
            internal static readonly T[] Value = new T[0];
        }
#endif
    }
}
