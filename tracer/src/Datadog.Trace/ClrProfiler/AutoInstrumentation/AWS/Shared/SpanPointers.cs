// <copyright file="SpanPointers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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
        // The pointer direction will always be down. The serverless agent handles cases where the
        // direction is up.
        private const string DownDirection = "d";
        private const string LinkKind = "span-pointer";
        private const int SpanPointerHashSizeBytes = 16;
        private const string S3PtrKind = "aws.s3.object";

        // S3 hashing rules: https://github.com/DataDog/dd-span-pointer-rules/blob/main/AWS/S3/Object/README.md
        public static void AddS3SpanPointer(Span span, string bucketName, string key, string eTag)
        {
            var hash = GeneratePointerHash(bucketName, key, StripQuotes(eTag));
            var spanLinkAttributes = new List<KeyValuePair<string, string>>
            {
                new("ptr.kind", S3PtrKind),
                new("ptr.dir", DownDirection),
                new("ptr.hash", hash),
                new("link.kind", LinkKind),
            };

            var spanLink = new SpanLink(SpanContext.ZeroContext, spanLinkAttributes);
            span.AddLink(spanLink);
        }

        // Hashing rules: https://github.com/DataDog/dd-span-pointer-rules/tree/main?tab=readme-ov-file#general-hashing-rules
        private static string GeneratePointerHash(params string[] components)
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

        /// <summary>
        /// Removes quotes wrapping a value, if they exist.
        /// S3's eTag is sometimes wrapped in quotes.
        /// </summary>
        /// <param name="value">Value to remove quotes from</param>
        /// <returns>Value with quotes removed</returns>
        private static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
            {
                return value;
            }

            if (value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}
