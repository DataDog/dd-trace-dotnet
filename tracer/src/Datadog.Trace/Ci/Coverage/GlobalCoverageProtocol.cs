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
    public const int MarkerMaximumBytes = 128 * 1024;
    public const string PendingMarkerPrefix = ".dd-coverage-process-incomplete-";
    public const string ReadyMarkerPrefix = ".dd-coverage-process-ready-";
    public const string CommandOwnerClaimPrefix = ".dd-coverage-command-owner-";
    public const string ClaimExtension = ".claim";
    public const string ReconciliationLockFileName = ".dd-coverage-process-reconcile.lock";
    public const string CoverageFilePrefix = "coverage-";
    public const string JsonExtension = ".json";
    public const string PendingMarkerPattern = PendingMarkerPrefix + "*";
    public const string ReadyMarkerPattern = ReadyMarkerPrefix + "*";
    public const string CommandOwnerClaimPattern = CommandOwnerClaimPrefix + "*" + ClaimExtension;
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

    public static string GetReadyMarkerFileName(string processIdentity)
        => ReadyMarkerPrefix + processIdentity;

    public static string GetCommandOwnerClaimFileName(string runToken)
        => CommandOwnerClaimPrefix + runToken + ClaimExtension;

    public static string GetCoverageGenerationPrefix(string processIdentity)
        => $"{CoverageFilePrefix}{processIdentity}-";

    public static string GetCoverageFileName(string processIdentity, long generationId)
        => $"{GetCoverageGenerationPrefix(processIdentity)}{generationId.ToString(CultureInfo.InvariantCulture)}{JsonExtension}";
}
