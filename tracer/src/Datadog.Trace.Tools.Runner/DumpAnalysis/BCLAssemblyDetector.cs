// <copyright file="BCLAssemblyDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Datadog.Trace.Tools.Runner.DumpAnalysis
{
    internal static class BCLAssemblyDetector
    {
        // Created by using this tool: https://gist.github.com/OmerRaviv/81c870f0d521e1122e88be4499b74b8c
        private static readonly string[] MicrosoftPublicKeyTokens =
        {
            "b03f5f7f11d50a3a",
            "b77a5c561934e089",
            "cc7b13ffcd2ddd51",
            "31bf3856ad364e35",
            "7cec85d7bea7798e",
            "adb9793829ddae60",
            "30ad4fe6b2a6aeed",
            "50cebf1cceb9d05e",
            "979442b78dfc278e",
            "9dff12846e04bfbd"
        };

        internal static bool IsBCLAssembly(string? fileFullPath)
        {
            if (fileFullPath == null || !File.Exists(fileFullPath))
            {
                return false;
            }

            try
            {
                var publicKeyToken = ReadAssemblyPublicKeyToken(fileFullPath);
                return MicrosoftPublicKeyTokens.Contains(publicKeyToken);
            }
            catch
            {
                return false;
            }
        }

        private static string? ReadAssemblyPublicKeyToken(string fileFullPath)
        {
            using (var stream = File.OpenRead(fileFullPath))
            using (var reader = new PEReader(stream))
            {
                var metadataReader = reader.GetMetadataReader();
                var publicKey = metadataReader.GetAssemblyDefinition().PublicKey;
                return FormatPublicKeyToken(publicKey, metadataReader);
            }
        }

        private static string? FormatPublicKeyToken(this BlobHandle handle, MetadataReader metadataReader)
        {
            if (handle.IsNil)
            {
                return null;
            }

            var bytes = metadataReader.GetBlobBytes(handle);
            if (bytes == null || bytes.Length <= 0)
            {
                return null;
            }

            // Strong named assembly
            if (bytes.Length > 8)
            {
                // Get the public key token, which is the last 8 bytes of the SHA-1 hash of the public key
                using (var sha1 = SHA1.Create())
                {
                    var token = sha1.ComputeHash(bytes);
                    bytes = new byte[8];
                    var count = 0;
                    for (var i = token.Length - 1; i >= token.Length - 8; i--)
                    {
                        bytes[count] = token[i];
                        count++;
                    }
                }
            }

            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
