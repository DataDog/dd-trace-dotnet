// <copyright file="GlobalCoverageReconciliation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal static class GlobalCoverageReconciliation
{
    private const int ClaimMaximumBytes = 1_024;
    private const long MaximumProtocolMetadataBytes = 16L * 1024 * 1024;
    private const int MaximumParticipants = 4_096;
    private const int MaximumProtocolFiles = 65_536;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly StringComparer PathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool TryAcquire(
        string inputDirectory,
        GlobalCoverageReconciliationAuthority? authority,
        out GlobalCoverageReconciliationLease? lease,
        out bool protocolPresent)
    {
        lease = null;
        protocolPresent = false;
        FileStream? lockStream = null;
        GlobalCoverageReconciliationAuthority? reconciliationAuthority = authority;
        var authorityTransferred = false;
        var takeoverAuthority = false;
        try
        {
            var canonicalInput = CanonicalizeDirectory(inputDirectory);
            var pendingInInput = GetFilesBounded(canonicalInput, GlobalCoverageProtocol.PendingMarkerPattern);
            var readyInInput = GetFilesBounded(canonicalInput, GlobalCoverageProtocol.ReadyMarkerPattern);
            var claimsInInput = GetFilesBounded(canonicalInput, GlobalCoverageProtocol.CommandOwnerClaimPattern);
            protocolPresent = pendingInInput.Length > 0 || readyInInput.Length > 0 || claimsInInput.Length > 0;
            if (!protocolPresent)
            {
                return true;
            }

            if (pendingInInput.Length > MaximumParticipants ||
                pendingInInput.Length != readyInInput.Length ||
                claimsInInput.Length != 1)
            {
                return false;
            }

            lockStream = new FileStream(
                Path.Combine(canonicalInput, GlobalCoverageProtocol.ReconciliationLockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            var claimPath = claimsInInput[0];
            var claimFileName = Path.GetFileName(claimPath);
            var claimRunToken = claimFileName.Substring(
                GlobalCoverageProtocol.CommandOwnerClaimPrefix.Length,
                claimFileName.Length - GlobalCoverageProtocol.CommandOwnerClaimPrefix.Length - GlobalCoverageProtocol.ClaimExtension.Length);
            if (!IsLowerHex(claimRunToken, 64))
            {
                return false;
            }

            if (reconciliationAuthority is null)
            {
                var claimStream = new FileStream(claimPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                reconciliationAuthority = new GlobalCoverageReconciliationAuthority(claimPath, claimStream);
                takeoverAuthority = true;
            }
            else if (!PathComparer.Equals(reconciliationAuthority.ClaimPath, claimPath))
            {
                return false;
            }

            ValidateClaim(reconciliationAuthority.ClaimStream, claimRunToken);
            var metadataBudget = new ProtocolMetadataBudget();
            metadataBudget.AddFile(claimPath);

            if (pendingInInput.Length == 0)
            {
                if (GetFilesBounded(canonicalInput, GlobalCoverageProtocol.CoverageFilePattern).Length != 0)
                {
                    return false;
                }

                lease = new GlobalCoverageReconciliationLease(
                    lockStream,
                    reconciliationAuthority,
                    claimRunToken,
                    [],
                    [],
                    [],
                    [],
                    []);
                lockStream = null;
                authorityTransferred = true;
                return true;
            }

            var participants = new List<Participant>(pendingInInput.Length);
            foreach (var pendingPath in pendingInInput)
            {
                var suffix = Path.GetFileName(pendingPath).Substring(GlobalCoverageProtocol.PendingMarkerPrefix.Length);
                var readyPath = Path.Combine(canonicalInput, GlobalCoverageProtocol.GetReadyMarkerFileName(suffix));
                if (!File.Exists(readyPath))
                {
                    return false;
                }

                metadataBudget.AddFile(pendingPath);
                metadataBudget.AddFile(readyPath);
                var pending = ReadMarker(pendingPath, expectReady: false);
                var inputReady = ReadMarker(readyPath, expectReady: true);
                ValidatePair(pending, inputReady, suffix);
                if (!inputReady.Coordinator || !PathComparer.Equals(pending.Directory, canonicalInput))
                {
                    return false;
                }

                if (!string.Equals(claimRunToken, pending.RunToken, StringComparison.Ordinal))
                {
                    return false;
                }

                participants.Add(LoadParticipant(inputReady, suffix, metadataBudget));
            }

            var allRawFiles = new HashSet<string>(PathComparer);
            var allRawInputs = new List<GlobalCoverageCertifiedInput>();
            var selectedInputs = new List<GlobalCoverageCertifiedInput>();
            var allPending = new HashSet<string>(PathComparer);
            var allReady = new HashSet<string>(PathComparer);
            var allDirectories = new HashSet<string>(PathComparer);
            foreach (var participant in participants)
            {
                GlobalCoverageMarkerRecord? coordinator = null;
                Dictionary<long, FileFingerprint>? expectedCopies = null;
                foreach (var ready in participant.ReadyRecords)
                {
                    var directory = ready.Directory!;
                    allDirectories.Add(directory);
                    var identity = GetIdentitySuffix(ready);
                    var pendingPath = Path.Combine(directory, GlobalCoverageProtocol.GetPendingMarkerFileName(identity));
                    var readyPath = Path.Combine(directory, GlobalCoverageProtocol.GetReadyMarkerFileName(identity));
                    allPending.Add(pendingPath);
                    allReady.Add(readyPath);

                    var prefix = GlobalCoverageProtocol.GetCoverageGenerationPrefix(identity);
                    var rawFiles = GetFilesBounded(directory, prefix + "*" + GlobalCoverageProtocol.JsonExtension);
                    if (rawFiles.Length != ready.CommittedGenerations)
                    {
                        return false;
                    }

                    var generations = new HashSet<long>();
                    var copies = new Dictionary<long, FileFingerprint>();
                    foreach (var rawFile in rawFiles)
                    {
                        if (!TryParseGeneration(rawFile, prefix, out var generation) || !generations.Add(generation))
                        {
                            return false;
                        }

                        var newRawFile = allRawFiles.Add(rawFile);
                        if (newRawFile && allRawFiles.Count > MaximumProtocolFiles)
                        {
                            return false;
                        }

                        metadataBudget.AddPath(rawFile);
                        var fingerprint = GetFileFingerprint(rawFile);
                        copies.Add(generation, fingerprint);
                        if (newRawFile)
                        {
                            allRawInputs.Add(new GlobalCoverageCertifiedInput(rawFile, fingerprint.Length, fingerprint.Hash));
                        }
                    }

                    for (long generation = 1; generation <= ready.CommittedGenerations; generation++)
                    {
                        if (!generations.Contains(generation))
                        {
                            return false;
                        }
                    }

                    if (expectedCopies is null)
                    {
                        expectedCopies = copies;
                    }
                    else if (!HaveEquivalentCopies(expectedCopies, copies))
                    {
                        return false;
                    }

                    if (ready.Coordinator)
                    {
                        if (coordinator is not null)
                        {
                            return false;
                        }

                        coordinator = ready;
                        foreach (var rawFile in rawFiles)
                        {
                            TryParseGeneration(rawFile, prefix, out var generation);
                            var fingerprint = copies[generation];
                            selectedInputs.Add(new GlobalCoverageCertifiedInput(rawFile, fingerprint.Length, fingerprint.Hash));
                        }
                    }
                }

                if (coordinator is null)
                {
                    return false;
                }
            }

            foreach (var directory in allDirectories)
            {
                var claims = GetFilesBounded(directory, GlobalCoverageProtocol.CommandOwnerClaimPattern);
                if (PathComparer.Equals(directory, canonicalInput))
                {
                    var expectedClaim = Path.Combine(directory, GlobalCoverageProtocol.GetCommandOwnerClaimFileName(claimRunToken));
                    if (claims.Length != 1 || claims.Any(claim => !PathComparer.Equals(claim, expectedClaim)))
                    {
                        return false;
                    }
                }
                else if (claims.Length != 0)
                {
                    return false;
                }

                foreach (var pendingMarker in GetFilesBounded(directory, GlobalCoverageProtocol.PendingMarkerPattern))
                {
                    if (!allPending.Contains(pendingMarker))
                    {
                        return false;
                    }
                }

                foreach (var readyMarker in GetFilesBounded(directory, GlobalCoverageProtocol.ReadyMarkerPattern))
                {
                    if (!allReady.Contains(readyMarker))
                    {
                        return false;
                    }
                }

                foreach (var coverageFile in GetFilesBounded(directory, GlobalCoverageProtocol.CoverageFilePattern))
                {
                    if (!allRawFiles.Contains(coverageFile))
                    {
                        return false;
                    }
                }
            }

            lease = new GlobalCoverageReconciliationLease(
                lockStream,
                reconciliationAuthority,
                claimRunToken,
                selectedInputs,
                allRawInputs,
                allReady.ToArray(),
                allPending.ToArray(),
                allDirectories.ToArray());
            lockStream = null;
            authorityTransferred = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or OverflowException or OutOfMemoryException or ArgumentException or NotSupportedException or SecurityException or ObjectDisposedException)
        {
            return false;
        }
        finally
        {
            if (takeoverAuthority && !authorityTransferred)
            {
                reconciliationAuthority?.Dispose();
            }

            lockStream?.Dispose();
        }
    }

    private static string CanonicalizeDirectory(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        while (fullPath.Length > root.Length &&
               (fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar || fullPath[fullPath.Length - 1] == Path.AltDirectorySeparatorChar))
        {
            fullPath = fullPath.Substring(0, fullPath.Length - 1);
        }

        return fullPath;
    }

    private static string[] GetFilesBounded(string directory, string pattern)
    {
        var files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                             .Take(MaximumProtocolFiles + 1)
                             .ToArray();
        if (files.Length > MaximumProtocolFiles)
        {
            throw new InvalidDataException("The global coverage protocol file-count limit was exceeded.");
        }

        return files;
    }

    private static FileFingerprint GetFileFingerprint(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > GlobalCoverageArtifactLimits.Default.MaximumSerializedBytes)
        {
            throw new InvalidDataException("A certified global coverage artifact has an invalid size.");
        }

        using var sha256 = SHA256.Create();
        return new FileFingerprint(stream.Length, sha256.ComputeHash(stream));
    }

    private static bool HaveEquivalentCopies(Dictionary<long, FileFingerprint> expected, Dictionary<long, FileFingerprint> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var fingerprint) ||
                pair.Value.Length != fingerprint.Length ||
                !pair.Value.Hash.SequenceEqual(fingerprint.Hash))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowerHex(string value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private static GlobalCoverageMarkerRecord ReadMarker(string path, bool expectReady)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > GlobalCoverageProtocol.MarkerMaximumBytes)
        {
            throw new InvalidDataException("A global coverage marker has an invalid size.");
        }

        if (stream.Length >= 3)
        {
            var first = stream.ReadByte();
            var second = stream.ReadByte();
            var third = stream.ReadByte();
            stream.Position = 0;
            if (first == 0xef && second == 0xbb && third == 0xbf)
            {
                throw new InvalidDataException("A global coverage marker must not contain a UTF-8 byte-order mark.");
            }
        }

        using var streamReader = new StreamReader(stream, StrictUtf8, false, 4096, false);
        using var reader = new JsonTextReader(streamReader) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
        {
            throw new InvalidDataException("A global coverage marker must be a JSON object.");
        }

        var record = new GlobalCoverageMarkerRecord();
        var properties = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName || reader.Value is not string propertyName || !properties.Add(propertyName) || !reader.Read())
            {
                throw new InvalidDataException("A global coverage marker contains an invalid or duplicate property.");
            }

            switch (propertyName)
            {
                case "version":
                    record.Version = ReadInt32(reader);
                    break;
                case "status":
                    record.Status = ReadString(reader);
                    break;
                case "runToken":
                    record.RunToken = ReadString(reader);
                    break;
                case "pid":
                    record.ProcessId = ReadInt32(reader);
                    break;
                case "nonce":
                    record.Nonce = ReadString(reader);
                    break;
                case "directory":
                    record.Directory = ReadString(reader);
                    break;
                case "requiredMask" when expectReady:
                    record.RequiredMask = ReadInt32(reader);
                    break;
                case "committedGenerations" when expectReady:
                    record.CommittedGenerations = ReadInt64(reader);
                    break;
                case "started" when expectReady:
                    record.Started = ReadInt64(reader);
                    break;
                case "closed" when expectReady:
                    record.Closed = ReadInt64(reader);
                    break;
                case "disposed" when expectReady:
                    record.Disposed = ReadInt64(reader);
                    break;
                case "coordinator" when expectReady:
                    record.Coordinator = ReadBoolean(reader);
                    break;
                case "directories" when expectReady:
                    ReadDirectories(reader, record.Directories);
                    break;
                default:
                    throw new InvalidDataException("A global coverage marker contains an unknown property.");
            }
        }

        if (reader.TokenType != JsonToken.EndObject || reader.Read())
        {
            throw new InvalidDataException("A global coverage marker contains trailing or incomplete JSON.");
        }

        var expectedCount = expectReady ? 13 : 6;
        if (properties.Count != expectedCount ||
            record.Version != 1 ||
            !string.Equals(record.Status, expectReady ? "ready" : "pending", StringComparison.Ordinal) ||
            record.RunToken is null || !IsLowerHex(record.RunToken, 64) ||
            record.ProcessId < 0 ||
            record.Nonce is null || !IsLowerHex(record.Nonce, 32) ||
            record.Directory is null || !string.Equals(record.Directory, CanonicalizeDirectory(record.Directory), PathComparer == StringComparer.OrdinalIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("A global coverage marker failed validation.");
        }

        if (expectReady &&
            (record.RequiredMask is < 1 or > 3 ||
             record.CommittedGenerations <= 0 ||
             record.Started < 0 ||
             record.Started != record.Closed ||
             record.Closed != record.Disposed ||
             record.Directories.Count != (record.RequiredMask == 3 ? 2 : 1) ||
             !record.Directories.Any(directory => PathComparer.Equals(directory, record.Directory))))
        {
            throw new InvalidDataException("A global coverage ready marker failed its balance or topology audit.");
        }

        return record;
    }

    private static void ValidateClaim(FileStream stream, string expectedRunToken)
    {
        if (stream.Length <= 0 || stream.Length > ClaimMaximumBytes)
        {
            throw new InvalidDataException("A global coverage command claim has an invalid size.");
        }

        stream.Position = 0;
        if (stream.Length >= 3)
        {
            var first = stream.ReadByte();
            var second = stream.ReadByte();
            var third = stream.ReadByte();
            stream.Position = 0;
            if (first == 0xef && second == 0xbb && third == 0xbf)
            {
                throw new InvalidDataException("A global coverage command claim must not contain a UTF-8 byte-order mark.");
            }
        }

        using var streamReader = new StreamReader(stream, StrictUtf8, false, 1024, true);
        using var reader = new JsonTextReader(streamReader) { MaxDepth = 4, DateParseHandling = DateParseHandling.None };
        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
        {
            throw new InvalidDataException("A global coverage command claim must be a JSON object.");
        }

        var properties = new HashSet<string>(StringComparer.Ordinal);
        var version = 0;
        string? runToken = null;
        var processId = 0;
        string? kind = null;
        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName || reader.Value is not string propertyName || !properties.Add(propertyName) || !reader.Read())
            {
                throw new InvalidDataException("A global coverage command claim contains an invalid or duplicate property.");
            }

            switch (propertyName)
            {
                case "version":
                    version = ReadInt32(reader);
                    break;
                case "runToken":
                    runToken = ReadString(reader);
                    break;
                case "pid":
                    processId = ReadInt32(reader);
                    break;
                case "kind":
                    kind = ReadString(reader);
                    break;
                default:
                    throw new InvalidDataException("A global coverage command claim contains an unknown property.");
            }
        }

        if (reader.TokenType != JsonToken.EndObject || reader.Read() ||
            properties.Count != 4 ||
            version != 1 ||
            !string.Equals(runToken, expectedRunToken, StringComparison.Ordinal) ||
            processId <= 0 ||
            (kind != "dotnet-test" && kind != "vstest-executor"))
        {
            throw new InvalidDataException("A global coverage command claim failed validation.");
        }

        stream.Position = 0;
    }

    private static Participant LoadParticipant(GlobalCoverageMarkerRecord inputReady, string suffix, ProtocolMetadataBudget metadataBudget)
    {
        var records = new List<GlobalCoverageMarkerRecord>(inputReady.Directories.Count);
        foreach (var directory in inputReady.Directories)
        {
            var canonicalDirectory = CanonicalizeDirectory(directory);
            var pendingPath = Path.Combine(canonicalDirectory, GlobalCoverageProtocol.GetPendingMarkerFileName(suffix));
            var readyPath = Path.Combine(canonicalDirectory, GlobalCoverageProtocol.GetReadyMarkerFileName(suffix));
            metadataBudget.AddFile(pendingPath);
            metadataBudget.AddFile(readyPath);
            var pending = ReadMarker(pendingPath, expectReady: false);
            var ready = ReadMarker(readyPath, expectReady: true);
            ValidatePair(pending, ready, suffix);
            if (!PathComparer.Equals(pending.Directory, canonicalDirectory))
            {
                throw new InvalidDataException("A global coverage marker is not located in its declared directory.");
            }

            ValidateReadyEquivalent(inputReady, ready);
            records.Add(ready);
        }

        return new Participant(records);
    }

    private static void ValidatePair(GlobalCoverageMarkerRecord pending, GlobalCoverageMarkerRecord ready, string suffix)
    {
        if (!string.Equals(GetIdentitySuffix(pending), suffix, StringComparison.Ordinal) ||
            !string.Equals(GetIdentitySuffix(ready), suffix, StringComparison.Ordinal) ||
            !string.Equals(pending.RunToken, ready.RunToken, StringComparison.Ordinal) ||
            pending.ProcessId != ready.ProcessId ||
            !string.Equals(pending.Nonce, ready.Nonce, StringComparison.Ordinal) ||
            !PathComparer.Equals(pending.Directory, ready.Directory))
        {
            throw new InvalidDataException("A global coverage pending/ready marker pair does not match.");
        }
    }

    private static void ValidateReadyEquivalent(GlobalCoverageMarkerRecord expected, GlobalCoverageMarkerRecord actual)
    {
        if (!string.Equals(expected.RunToken, actual.RunToken, StringComparison.Ordinal) ||
            expected.ProcessId != actual.ProcessId ||
            !string.Equals(expected.Nonce, actual.Nonce, StringComparison.Ordinal) ||
            expected.RequiredMask != actual.RequiredMask ||
            expected.CommittedGenerations != actual.CommittedGenerations ||
            expected.Started != actual.Started ||
            expected.Closed != actual.Closed ||
            expected.Disposed != actual.Disposed ||
            expected.Directories.Count != actual.Directories.Count)
        {
            throw new InvalidDataException("Cross-linked global coverage ready markers disagree.");
        }

        for (var i = 0; i < expected.Directories.Count; i++)
        {
            if (!PathComparer.Equals(expected.Directories[i], actual.Directories[i]))
            {
                throw new InvalidDataException("Cross-linked global coverage ready-marker directories disagree.");
            }
        }
    }

    private static string GetIdentitySuffix(GlobalCoverageMarkerRecord record)
        => GlobalCoverageProtocol.GetProcessIdentity(record.RunToken!, record.ProcessId, record.Nonce!);

    private static bool TryParseGeneration(string path, string prefix, out long generation)
    {
        var fileName = Path.GetFileName(path);
        var suffix = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - GlobalCoverageProtocol.JsonExtension.Length);
        return long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out generation) && generation > 0;
    }

    private static int ReadInt32(JsonTextReader reader)
    {
        var value = ReadInt64(reader);
        return checked((int)value);
    }

    private static long ReadInt64(JsonTextReader reader)
        => reader.TokenType == JsonToken.Integer && reader.Value is not null
               ? Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture)
               : throw new InvalidDataException("A global coverage marker integer has an invalid type.");

    private static string ReadString(JsonTextReader reader)
        => reader.TokenType == JsonToken.String && reader.Value is string value
               ? value
               : throw new InvalidDataException("A global coverage marker string has an invalid type.");

    private static bool ReadBoolean(JsonTextReader reader)
        => reader.TokenType == JsonToken.Boolean && reader.Value is bool value
               ? value
               : throw new InvalidDataException("A global coverage marker boolean has an invalid type.");

    private static void ReadDirectories(JsonTextReader reader, List<string> directories)
    {
        if (reader.TokenType != JsonToken.StartArray)
        {
            throw new InvalidDataException("The global coverage marker directories value is not an array.");
        }

        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            if (directories.Count >= 2)
            {
                throw new InvalidDataException("A global coverage marker contains too many output directories.");
            }

            var directory = ReadString(reader);
            if (!string.Equals(directory, CanonicalizeDirectory(directory), PathComparer == StringComparer.OrdinalIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                directories.Any(existing => PathComparer.Equals(existing, directory)))
            {
                throw new InvalidDataException("A global coverage marker contains an invalid output directory.");
            }

            directories.Add(directory);
        }

        if (reader.TokenType != JsonToken.EndArray)
        {
            throw new InvalidDataException("A global coverage marker contains an incomplete output-directory array.");
        }
    }

    private sealed class Participant
    {
        public Participant(IReadOnlyList<GlobalCoverageMarkerRecord> readyRecords)
        {
            ReadyRecords = readyRecords;
        }

        public IReadOnlyList<GlobalCoverageMarkerRecord> ReadyRecords { get; }
    }

    private sealed class FileFingerprint
    {
        public FileFingerprint(long length, byte[] hash)
        {
            Length = length;
            Hash = hash;
        }

        public long Length { get; }

        public byte[] Hash { get; }
    }

    private sealed class ProtocolMetadataBudget
    {
        private readonly HashSet<string> _paths = new(PathComparer);
        private long _bytes;

        public void AddFile(string path)
        {
            if (!_paths.Add(path))
            {
                return;
            }

            var file = new FileInfo(path);
            AddBytes(path, file.Length);
        }

        public void AddPath(string path)
        {
            if (_paths.Add(path))
            {
                AddBytes(path, 0);
            }
        }

        private void AddBytes(string path, long fileLength)
        {
            _bytes = checked(_bytes + fileLength + StrictUtf8.GetByteCount(path));
            if (_bytes > MaximumProtocolMetadataBytes)
            {
                throw new InvalidDataException("The global coverage protocol metadata budget was exceeded.");
            }
        }
    }
}
