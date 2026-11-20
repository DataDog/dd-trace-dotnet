// <copyright file="GlobalCoverageProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Ci.Coverage;

// Keep the small on-disk vocabulary and run identity hashing shared by producers and consumers.
internal static class GlobalCoverageProtocol
{
    public const string PendingMarkerPrefix = ".dd-coverage-process-incomplete-";
    public const string CoverageFilePrefix = "coverage-";
    public const string JsonExtension = ".json";
    public const string PendingMarkerPattern = PendingMarkerPrefix + "*";
    public const string CoverageFilePattern = CoverageFilePrefix + "*" + JsonExtension;

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

    public static string GetRunToken(string runId)
    {
        var bytes = Utf8WithoutBom.GetBytes(runId);
#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(bytes);
#else
        byte[] hash;
        using (var sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(bytes);
        }
#endif
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string GetProcessIdentity(string runToken, int processId, string nonce)
        => $"{runToken}-{processId.ToString(CultureInfo.InvariantCulture)}-{nonce}";

    public static string GetPendingMarkerFileName(string processIdentity)
        => PendingMarkerPrefix + processIdentity;

    public static string GetCoverageFileName(string processIdentity)
        => $"{CoverageFilePrefix}{processIdentity}{JsonExtension}";
}
