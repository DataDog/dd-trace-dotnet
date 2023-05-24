// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;

namespace Datadog.Trace.Iast;

internal static class StringExtensions
{
    private const int GoldenRatio = 1618033987;

    internal static unsafe int GetStaticHashCode(this string? target)
    {
        if (target == null)
        {
            return -1;
        }

        fixed (char* charPtr = target)
        {
            var int32Length = target.Length / 2;
            var intPtr = (int*)charPtr;

            var result = 0;
            for (var i = 0; i < int32Length; i++)
            {
                result += intPtr[i] * GoldenRatio;
            }

            if (target.Length % 2 != 0)
            {
                result += ((int)charPtr[target.Length - 1]) * GoldenRatio;
            }

            return result;
        }
    }
}
