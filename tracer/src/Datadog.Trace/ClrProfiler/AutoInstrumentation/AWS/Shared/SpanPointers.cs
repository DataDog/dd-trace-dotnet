// <copyright file="SpanPointers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared
{
    /// <summary>
    ///  SpanPointer helper methods
    /// </summary>
    internal static class SpanPointers
    {
        private const int SpanPointerHashSizeBytes = 16;

        public static string GeneratePointerHash(string[] components)
        {
            using var stream = new MemoryStream();

            var first = true;
            foreach (var component in components)
            {
                if (!first)
                {
                    stream.WriteByte((byte)'|');
                }
                else
                {
                    first = false;
                }

                var componentBytes = Encoding.UTF8.GetBytes(component);
                stream.Write(componentBytes, 0, componentBytes.Length);
            }

            using var sha256 = SHA256.Create();
            var fullHash = sha256.ComputeHash(stream.ToArray());
            var truncatedHash = new byte[SpanPointerHashSizeBytes];
            Array.Copy(fullHash, truncatedHash, SpanPointerHashSizeBytes);

            return BitConverter.ToString(truncatedHash).Replace("-", string.Empty).ToLower();
        }
    }
}
