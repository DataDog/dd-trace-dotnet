// <copyright file="GlobalCoverageProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Ci.Coverage;

// Keep the on-disk vocabulary and identity hashing shared by producers and consumers. Directory
// resolution stays with each caller because producers resolve against a captured base directory,
// while reconciliation intentionally resolves the directory supplied at consumption time.
internal static class GlobalCoverageProtocol
{
    internal const int MarkerMaximumBytes = 128 * 1024;
    internal const string PendingMarkerPrefix = ".dd-coverage-process-incomplete-";
    internal const string ReadyMarkerPrefix = ".dd-coverage-process-ready-";
    internal const string CommandOwnerClaimPrefix = ".dd-coverage-command-owner-";
    internal const string ClaimExtension = ".claim";
    internal const string ReconciliationLockFileName = ".dd-coverage-process-reconcile.lock";
    internal const string CoverageFilePrefix = "coverage-";
    internal const string JsonExtension = ".json";
    internal const string PendingMarkerPattern = PendingMarkerPrefix + "*";
    internal const string ReadyMarkerPattern = ReadyMarkerPrefix + "*";
    internal const string CommandOwnerClaimPattern = CommandOwnerClaimPrefix + "*" + ClaimExtension;
    internal const string CoverageFilePattern = CoverageFilePrefix + "*" + JsonExtension;

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

    internal static string GetRunToken(string runId)
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

    internal static string GetProcessIdentity(string runToken, int processId, string nonce)
        => $"{runToken}-{processId.ToString(CultureInfo.InvariantCulture)}-{nonce}";

    internal static string GetPendingMarkerFileName(string processIdentity)
        => PendingMarkerPrefix + processIdentity;

    internal static string GetReadyMarkerFileName(string processIdentity)
        => ReadyMarkerPrefix + processIdentity;

    internal static string GetCommandOwnerClaimFileName(string runToken)
        => CommandOwnerClaimPrefix + runToken + ClaimExtension;

    internal static string GetCoverageGenerationPrefix(string processIdentity)
        => $"{CoverageFilePrefix}{processIdentity}-";

    internal static string GetCoverageFileName(string processIdentity, long generationId)
        => $"{GetCoverageGenerationPrefix(processIdentity)}{generationId.ToString(CultureInfo.InvariantCulture)}{JsonExtension}";
}
