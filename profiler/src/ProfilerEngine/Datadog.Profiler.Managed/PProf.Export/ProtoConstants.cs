// <copyright file="ProtoConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.PProf.Export
{
    internal static class ProtoConstants
    {
        public static class StringTableIndex
        {
            public const long Unresolved = -1;
            public const long Unset = 0;

            public static bool IsSet(long stringTableIndex)
            {
                return (stringTableIndex != Unresolved) && (stringTableIndex != Unset);
            }

            public static long GetUnresolvedIfSet(long stringTableIndex)
            {
                return IsSet(stringTableIndex) ? Unresolved : stringTableIndex;
            }
        }

        public static class NumericValue
        {
            public const long UnsetInt64 = 0;
            public const ulong UnsetUInt64 = 0;
        }
    }
}
