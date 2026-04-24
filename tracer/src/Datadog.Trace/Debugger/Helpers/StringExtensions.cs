// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class StringExtensions
    {
        private static readonly char[] Hex = "0123456789abcdef".ToCharArray();

        // https://stackoverflow.com/questions/18021808/uuid-interop-with-c-sharp-code
        public static string ToUUID(this string input)
        {
            // 1. To MD5
#if NETCOREAPP
            // MD5 always produces a 16 byte hash
            Span<byte> bytes = stackalloc byte[16];
            Md5Helper.ComputeMd5Hash(input, bytes);
#else
            var bytes = Md5Helper.ComputeMd5Hash(input);
#endif

            // version (3) and variant (RFC 4122)
            bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

            // Format the return bytes as a guid
            // note that we _could_ get a UUID from these bytes by trivially doing
            // new Guid(bytes).ToString("d") but that uses a slightly different
            // layout of the bytes, which would cause a different UUID to be generated
            Span<char> chars = stackalloc char[36];
            var targetIndex = 0;

            for (var sourceIndex = 0; sourceIndex < 16; sourceIndex++)
            {
                if (sourceIndex == 4 || sourceIndex == 6 || sourceIndex == 8 || sourceIndex == 10)
                {
                    chars[targetIndex++] = '-';
                }

                var sourceByte = bytes[sourceIndex];
                chars[targetIndex++] = Hex[sourceByte >> 4];
                chars[targetIndex++] = Hex[sourceByte & 0xF];
            }

#if NETCOREAPP
            return new string(chars);
#else
            return chars.ToString();
#endif
        }
    }
}
