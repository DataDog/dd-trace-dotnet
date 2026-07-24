// <copyright file="GlobalCoverageCertifiedInput.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageCertifiedInput
{
    public GlobalCoverageCertifiedInput(string path, long length, byte[] hash)
    {
        Path = path;
        Length = length;
        Hash = hash;
    }

    public string Path { get; }

    public long Length { get; }

    public byte[] Hash { get; }

    public bool Matches(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        if (stream.Length != Length)
        {
            return false;
        }

        using var sha256 = SHA256.Create();
        return Hash.SequenceEqual(sha256.ComputeHash(stream));
    }
}
