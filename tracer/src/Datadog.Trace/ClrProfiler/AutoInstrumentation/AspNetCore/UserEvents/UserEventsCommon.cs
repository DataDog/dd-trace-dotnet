// <copyright file="UserEventsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

internal static class UserEventsCommon
{
    internal static string? GetId(IIdentityUser? user)
    {
        return user?.Id?.ToString() ?? user?.Email ?? user?.UserName;
    }

    internal static unsafe string? GetAnonId(string id)
    {
        using var hash = SHA256.Create();
        var byteArray = hash.ComputeHash(Encoding.UTF8.GetBytes(id));

        // spec says take first half of the hash
        const int bytesToUse = 16;

        if (byteArray.Length >= bytesToUse)
        {
            // we want to end up with a string of the form anon_0c76692372ebf01a7da6e9570fb7d0a1
            // 37 is 5 prefix character plus 32 hexadecimal digits (presenting 16 bytes)
            // stackalloc to avoid intermediate heap allocation
            var stringChars = stackalloc char[37];
            stringChars[0] = 'a';
            stringChars[1] = 'n';
            stringChars[2] = 'o';
            stringChars[3] = 'n';
            stringChars[4] = '_';
            for (var iBytes = 0; iBytes < bytesToUse; iBytes++)
            {
                var b = byteArray[iBytes];
                var iChars = iBytes * 2;
                stringChars[iChars + 5] = ByteDigitToChar(b >> 4);
                stringChars[iChars + 6] = ByteDigitToChar(b & 0x0F);
            }

            return new string(stringChars, 0, 37);
        }

        return null;
    }

    // assumes byteDigit < 15
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ByteDigitToChar(int byteDigit)
    {
        if (byteDigit <= 9)
        {
            return (char)(byteDigit + 0x30);
        }

        return (char)(byteDigit - 0x0a + 0x61);
    }
}
