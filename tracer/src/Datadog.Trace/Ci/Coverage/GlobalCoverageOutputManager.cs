// <copyright file="GlobalCoverageOutputManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageOutputManager
{
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

    public GlobalCoverageOutputManager(string? configuredDirectory, string baseDirectory, Func<string> runIdProvider)
    {
        _configuredDirectory = configuredDirectory;
        _baseDirectory = baseDirectory;
        _runIdProvider = runIdProvider;
    }

    public byte FrozenMask
    {
        get
        {
            lock (_gate)
            {
                return GetMaskUnderLock();
            }
        }
    }

    public bool IsFailed
    {
        get
        {
            lock (_gate)
            {
                return _failed;
            }
        }
    }

    public IReadOnlyList<GlobalCoverageOutputRegistration> GetRegistrations()
    {
        lock (_gate)
        {
            return _registrations.ToArray();
        }
    }

    public bool EnsureConfiguredAndFreeze()
    {
        lock (_gate)
        {
            if (_frozen)
            {
                return !_failed;
            }

            if (!StringUtil.IsNullOrWhiteSpace(_configuredDirectory))
            {
                TryRegisterUnderLock(_configuredDirectory!, coordinator: true);
            }

            _frozen = true;
            return !_failed;
        }
    }

    public bool RegisterCollectorAndFreeze(string directory)
    {
        lock (_gate)
        {
            if (!StringUtil.IsNullOrWhiteSpace(_configuredDirectory))
            {
                TryRegisterUnderLock(_configuredDirectory!, coordinator: true);
            }

            TryRegisterUnderLock(directory, coordinator: _registrations.Count == 0);
            _frozen = true;
            return !_failed;
        }
    }

    public string GetCoveragePath(GlobalCoverageOutputRegistration registration, long generationId)
    {
        lock (_gate)
        {
            EnsureIdentityUnderLock();
            return Path.Combine(
                registration.Directory,
                GlobalCoverageProtocol.GetCoverageFileName(GetProcessIdentityUnderLock(), generationId));
        }
    }

    public void RecordGenerationCommit(byte requiredMask, byte committedMask)
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

    public GlobalCoverageStagedMarkerSet? TryStageReadyMarkers(CoverageContextDiagnosticSnapshot diagnostics)
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
            var stagedMarkers = new List<GlobalCoverageStagedArtifact>(_registrations.Count);
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

    private static void WriteBoundedMarker(string path, Action<JsonTextWriter> write)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
        using var bounded = new GlobalCoverageBoundedWriteStream(
            stream,
            GlobalCoverageProtocol.MarkerMaximumBytes,
            "The global coverage marker-size limit was exceeded.");
        using var textWriter = new StreamWriter(bounded, Utf8WithoutBom, 4096, true);
        using var jsonWriter = new JsonTextWriter(textWriter);
        write(jsonWriter);
        jsonWriter.Flush();
        textWriter.Flush();
        bounded.Flush();
        stream.Flush(true);
    }

    private void CommitReadyMarkers(IReadOnlyList<GlobalCoverageStagedArtifact> markers)
    {
        lock (_gate)
        {
            if (_failed)
            {
                ThrowHelper.ThrowInvalidOperationException("Global coverage output became incomplete before ready-marker commit.");
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
            var suffix = GetProcessIdentityUnderLock();
            var pendingPath = Path.Combine(canonicalDirectory, GlobalCoverageProtocol.GetPendingMarkerFileName(suffix));
            var readyPath = Path.Combine(canonicalDirectory, GlobalCoverageProtocol.GetReadyMarkerFileName(suffix));
            WritePendingMarkerUnderLock(pendingPath, canonicalDirectory);

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
            var suffix = GetProcessIdentityUnderLock();
            var pendingPath = Path.Combine(canonicalDirectory, GlobalCoverageProtocol.GetPendingMarkerFileName(suffix));
            if (!File.Exists(pendingPath))
            {
                WritePendingMarkerUnderLock(pendingPath, canonicalDirectory);
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

        _runToken = GlobalCoverageProtocol.GetRunToken(_runIdProvider());
        _nonce = Guid.NewGuid().ToString("N");
        _processId = DomainMetadata.Instance.ProcessId;
    }

    private string GetProcessIdentityUnderLock()
        => GlobalCoverageProtocol.GetProcessIdentity(_runToken!, _processId, _nonce!);

    private void WritePendingMarkerUnderLock(string pendingPath, string canonicalDirectory)
    {
        // Pending markers are durable blockers: both normal registration and late-registration
        // failure must serialize the exact same process identity and directory.
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

    private GlobalCoverageStagedArtifact? TryStageReadyUnderLock(GlobalCoverageOutputRegistration registration, byte mask, CoverageContextDiagnosticSnapshot diagnostics)
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
            return new GlobalCoverageStagedArtifact(temporaryPath, registration.ReadyPath, replaceExisting: false);
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

    public sealed class GlobalCoverageStagedMarkerSet : IDisposable
    {
        private readonly IReadOnlyList<GlobalCoverageStagedArtifact> _markers;
        private GlobalCoverageOutputManager? _owner;

        public GlobalCoverageStagedMarkerSet(GlobalCoverageOutputManager owner, IReadOnlyList<GlobalCoverageStagedArtifact> markers)
        {
            _owner = owner;
            _markers = markers;
        }

        public void Commit()
        {
            var owner = _owner;
            if (owner is null)
            {
                ThrowHelper.ThrowInvalidOperationException("The staged ready-marker set is no longer available.");
            }

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
}
