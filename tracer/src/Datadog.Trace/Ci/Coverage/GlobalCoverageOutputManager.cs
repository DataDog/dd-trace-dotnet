// <copyright file="GlobalCoverageOutputManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageOutputManager
{
    private const int MarkerMaximumBytes = 128 * 1024;
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);
    private static readonly StringComparer DirectoryComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly object _gate = new();
    private readonly string? _configuredDirectory;
    private readonly string _baseDirectory;
    private readonly Func<string> _runIdProvider;
    private readonly List<GlobalCoverageOutputRegistration> _registrations = new(2);
    private string? _runToken;
    private string? _nonce;
    private int _processId;
    private bool _frozen;
    private bool _failed;
    private long _committedGenerationCount;

    internal GlobalCoverageOutputManager(string? configuredDirectory, string baseDirectory, Func<string> runIdProvider)
    {
        _configuredDirectory = configuredDirectory;
        _baseDirectory = baseDirectory;
        _runIdProvider = runIdProvider;
    }

    internal byte FrozenMask
    {
        get
        {
            lock (_gate)
            {
                return GetMaskUnderLock();
            }
        }
    }

    internal bool IsFailed
    {
        get
        {
            lock (_gate)
            {
                return _failed;
            }
        }
    }

    internal IReadOnlyList<GlobalCoverageOutputRegistration> GetRegistrations()
    {
        lock (_gate)
        {
            return _registrations.ToArray();
        }
    }

    internal bool EnsureConfiguredAndFreeze()
    {
        lock (_gate)
        {
            if (_frozen)
            {
                return !_failed;
            }

            if (!string.IsNullOrWhiteSpace(_configuredDirectory))
            {
                TryRegisterUnderLock(_configuredDirectory!, coordinator: true);
            }

            _frozen = true;
            return !_failed;
        }
    }

    internal bool RegisterCollectorAndFreeze(string directory)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(_configuredDirectory))
            {
                TryRegisterUnderLock(_configuredDirectory!, coordinator: true);
            }

            TryRegisterUnderLock(directory, coordinator: _registrations.Count == 0);
            _frozen = true;
            return !_failed;
        }
    }

    internal string GetCoveragePath(GlobalCoverageOutputRegistration registration, long generationId)
    {
        lock (_gate)
        {
            EnsureIdentityUnderLock();
            return Path.Combine(
                registration.Directory,
                $"coverage-{_runToken}-{_processId.ToString(CultureInfo.InvariantCulture)}-{_nonce}-{generationId.ToString(CultureInfo.InvariantCulture)}.json");
        }
    }

    internal void RecordGenerationCommit(byte requiredMask, byte committedMask)
    {
        lock (_gate)
        {
            if (requiredMask != committedMask)
            {
                _failed = true;
                return;
            }

            _committedGenerationCount++;
        }
    }

    internal GlobalCoverageStagedMarkerSet? TryStageReadyMarkers(CoverageContextDiagnosticSnapshot diagnostics)
    {
        lock (_gate)
        {
            if (_failed)
            {
                return null;
            }

            if (_registrations.Count == 0)
            {
                return new GlobalCoverageStagedMarkerSet(this, []);
            }

            var mask = GetMaskUnderLock();
            var stagedMarkers = new List<GlobalCoverageStagedMarker>(_registrations.Count);
            foreach (var registration in GetReadyCommitOrderUnderLock())
            {
                var staged = TryStageReadyUnderLock(registration, mask, diagnostics);
                if (staged is null)
                {
                    _failed = true;
                    foreach (var marker in stagedMarkers)
                    {
                        marker.Dispose();
                    }

                    return null;
                }

                stagedMarkers.Add(staged);
            }

            return new GlobalCoverageStagedMarkerSet(this, stagedMarkers);
        }
    }

    private static string CanonicalizeDirectory(string directory, string baseDirectory)
    {
        var candidate = Path.IsPathRooted(directory) ? directory : Path.Combine(baseDirectory, directory);
        var fullPath = Path.GetFullPath(candidate);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        while (fullPath.Length > root.Length &&
               (fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar || fullPath[fullPath.Length - 1] == Path.AltDirectorySeparatorChar))
        {
            fullPath = fullPath.Substring(0, fullPath.Length - 1);
        }

        return fullPath;
    }

    private static string GetRunToken(string runId)
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

    private static void WriteBoundedMarker(string path, Action<JsonTextWriter> write)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
        using var bounded = new MarkerBoundedWriteStream(stream, MarkerMaximumBytes);
        using var textWriter = new StreamWriter(bounded, Utf8WithoutBom, 4096, true);
        using var jsonWriter = new JsonTextWriter(textWriter);
        write(jsonWriter);
        jsonWriter.Flush();
        textWriter.Flush();
        bounded.Flush();
        stream.Flush(true);
    }

    private void CommitReadyMarkers(IReadOnlyList<GlobalCoverageStagedMarker> markers)
    {
        lock (_gate)
        {
            if (_failed)
            {
                throw new InvalidOperationException("Global coverage output became incomplete before ready-marker commit.");
            }

            foreach (var marker in markers)
            {
                marker.Commit();
            }
        }
    }

    private bool TryRegisterUnderLock(string directory, bool coordinator)
    {
        try
        {
            var canonicalDirectory = CanonicalizeDirectory(directory, _baseDirectory);
            foreach (var existing in _registrations)
            {
                if (DirectoryComparer.Equals(existing.Directory, canonicalDirectory))
                {
                    if (coordinator)
                    {
                        foreach (var item in _registrations)
                        {
                            item.IsCoordinator = false;
                        }

                        existing.IsCoordinator = true;
                    }

                    return true;
                }
            }

            if (_frozen || _registrations.Count >= 2)
            {
                _failed = true;
                TryWriteBlockingPendingUnderLock(canonicalDirectory);
                return false;
            }

            EnsureIdentityUnderLock();
            Directory.CreateDirectory(canonicalDirectory);
            var suffix = $"{_runToken}-{_processId.ToString(CultureInfo.InvariantCulture)}-{_nonce}";
            var pendingPath = Path.Combine(canonicalDirectory, $".dd-coverage-process-incomplete-{suffix}");
            var readyPath = Path.Combine(canonicalDirectory, $".dd-coverage-process-ready-{suffix}");
            WriteBoundedMarker(
                pendingPath,
                writer =>
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("version");
                    writer.WriteValue(1);
                    writer.WritePropertyName("status");
                    writer.WriteValue("pending");
                    writer.WritePropertyName("runToken");
                    writer.WriteValue(_runToken);
                    writer.WritePropertyName("pid");
                    writer.WriteValue(_processId);
                    writer.WritePropertyName("nonce");
                    writer.WriteValue(_nonce);
                    writer.WritePropertyName("directory");
                    writer.WriteValue(canonicalDirectory);
                    writer.WriteEndObject();
                });

            var bit = (byte)(1 << _registrations.Count);
            if (coordinator)
            {
                foreach (var item in _registrations)
                {
                    item.IsCoordinator = false;
                }
            }

            _registrations.Add(new GlobalCoverageOutputRegistration(bit, canonicalDirectory, pendingPath, readyPath, coordinator || _registrations.Count == 0));
            return true;
        }
        catch
        {
            _failed = true;
            return false;
        }
    }

    private void TryWriteBlockingPendingUnderLock(string canonicalDirectory)
    {
        try
        {
            EnsureIdentityUnderLock();
            Directory.CreateDirectory(canonicalDirectory);
            var suffix = $"{_runToken}-{_processId.ToString(CultureInfo.InvariantCulture)}-{_nonce}";
            var pendingPath = Path.Combine(canonicalDirectory, $".dd-coverage-process-incomplete-{suffix}");
            if (!File.Exists(pendingPath))
            {
                WriteBoundedMarker(
                    pendingPath,
                    writer =>
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("version");
                        writer.WriteValue(1);
                        writer.WritePropertyName("status");
                        writer.WriteValue("pending");
                        writer.WritePropertyName("runToken");
                        writer.WriteValue(_runToken);
                        writer.WritePropertyName("pid");
                        writer.WriteValue(_processId);
                        writer.WritePropertyName("nonce");
                        writer.WriteValue(_nonce);
                        writer.WritePropertyName("directory");
                        writer.WriteValue(canonicalDirectory);
                        writer.WriteEndObject();
                    });
            }
        }
        catch
        {
            // The original registered pending marker remains the durable blocker when this best-effort marker cannot be written.
        }
    }

    private void EnsureIdentityUnderLock()
    {
        if (_runToken is not null)
        {
            return;
        }

        _runToken = GetRunToken(_runIdProvider());
        _nonce = Guid.NewGuid().ToString("N");
        _processId = DomainMetadata.Instance.ProcessId;
    }

    private byte GetMaskUnderLock()
    {
        byte mask = 0;
        foreach (var registration in _registrations)
        {
            mask |= registration.Bit;
        }

        return mask;
    }

    private IEnumerable<GlobalCoverageOutputRegistration> GetReadyCommitOrderUnderLock()
    {
        foreach (var registration in _registrations)
        {
            if (!registration.IsCoordinator)
            {
                yield return registration;
            }
        }

        foreach (var registration in _registrations)
        {
            if (registration.IsCoordinator)
            {
                yield return registration;
            }
        }
    }

    private GlobalCoverageStagedMarker? TryStageReadyUnderLock(GlobalCoverageOutputRegistration registration, byte mask, CoverageContextDiagnosticSnapshot diagnostics)
    {
        var temporaryPath = registration.ReadyPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            WriteBoundedMarker(
                temporaryPath,
                writer =>
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("version");
                    writer.WriteValue(1);
                    writer.WritePropertyName("status");
                    writer.WriteValue("ready");
                    writer.WritePropertyName("runToken");
                    writer.WriteValue(_runToken);
                    writer.WritePropertyName("pid");
                    writer.WriteValue(_processId);
                    writer.WritePropertyName("nonce");
                    writer.WriteValue(_nonce);
                    writer.WritePropertyName("directory");
                    writer.WriteValue(registration.Directory);
                    writer.WritePropertyName("requiredMask");
                    writer.WriteValue(mask);
                    writer.WritePropertyName("committedGenerations");
                    writer.WriteValue(_committedGenerationCount);
                    writer.WritePropertyName("started");
                    writer.WriteValue(diagnostics.Started);
                    writer.WritePropertyName("closed");
                    writer.WriteValue(diagnostics.Closed);
                    writer.WritePropertyName("disposed");
                    writer.WriteValue(diagnostics.Disposed);
                    writer.WritePropertyName("coordinator");
                    writer.WriteValue(registration.IsCoordinator);
                    writer.WritePropertyName("directories");
                    writer.WriteStartArray();
                    foreach (var item in _registrations)
                    {
                        writer.WriteValue(item.Directory);
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                });
            return new GlobalCoverageStagedMarker(temporaryPath, registration.ReadyPath);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }

            return null;
        }
    }

    private sealed class MarkerBoundedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumBytes;
        private long _writtenBytes;

        internal MarkerBoundedWriteStream(Stream inner, long maximumBytes)
        {
            _inner = inner;
            _maximumBytes = maximumBytes;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _writtenBytes;

        public override long Position
        {
            get => _writtenBytes;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var nextLength = checked(_writtenBytes + count);
            if (nextLength > _maximumBytes)
            {
                throw new InvalidDataException("The global coverage marker-size limit was exceeded.");
            }

            _inner.Write(buffer, offset, count);
            _writtenBytes = nextLength;
        }
    }

    internal sealed class GlobalCoverageStagedMarkerSet : IDisposable
    {
        private readonly IReadOnlyList<GlobalCoverageStagedMarker> _markers;
        private GlobalCoverageOutputManager? _owner;

        internal GlobalCoverageStagedMarkerSet(GlobalCoverageOutputManager owner, IReadOnlyList<GlobalCoverageStagedMarker> markers)
        {
            _owner = owner;
            _markers = markers;
        }

        internal void Commit()
        {
            var owner = _owner ?? throw new InvalidOperationException("The staged ready-marker set is no longer available.");
            owner.CommitReadyMarkers(_markers);
            _owner = null;
        }

        public void Dispose()
        {
            _owner = null;
            foreach (var marker in _markers)
            {
                marker.Dispose();
            }
        }
    }

    internal sealed class GlobalCoverageStagedMarker : IDisposable
    {
        private readonly string _readyPath;
        private string? _temporaryPath;

        internal GlobalCoverageStagedMarker(string temporaryPath, string readyPath)
        {
            _temporaryPath = temporaryPath;
            _readyPath = readyPath;
        }

        internal void Commit()
        {
            var temporaryPath = _temporaryPath ?? throw new InvalidOperationException("The staged ready marker is no longer available.");
            File.Move(temporaryPath, _readyPath);
            _temporaryPath = null;
        }

        public void Dispose()
        {
            var temporaryPath = _temporaryPath;
            _temporaryPath = null;
            if (temporaryPath is null)
            {
                return;
            }

            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }
}
