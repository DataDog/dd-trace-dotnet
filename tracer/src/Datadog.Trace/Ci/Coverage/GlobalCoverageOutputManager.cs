// <copyright file="GlobalCoverageOutputManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Publishes one atomic, process-wide coverage artifact. The pending marker is intentionally the
/// only coordination primitive: it makes an interrupted producer visible without requiring owner
/// election, cross-process leases, or a multi-file commit protocol.
/// </summary>
internal sealed class GlobalCoverageOutputManager
{
    private readonly object _gate = new();
    private readonly string? _configuredDirectory;
    private readonly string _baseDirectory;
    private readonly Func<string> _runIdProvider;
    private string? _directory;
    private string? _coveragePath;
    private string? _pendingPath;
    private bool _frozen;
    private bool _failed;
    private bool _published;

    public GlobalCoverageOutputManager(string? configuredDirectory, string baseDirectory, Func<string> runIdProvider)
    {
        _configuredDirectory = configuredDirectory;
        _baseDirectory = baseDirectory;
        _runIdProvider = runIdProvider;
    }

    public bool EnsureConfiguredAndFreeze()
    {
        lock (_gate)
        {
            if (!_frozen)
            {
                _frozen = true;
                if (!StringUtil.IsNullOrWhiteSpace(_configuredDirectory))
                {
                    ConfigureUnderLock(_configuredDirectory!);
                }
            }

            return !_failed;
        }
    }

    public bool RegisterCollectorAndFreeze(string directory)
    {
        lock (_gate)
        {
            if (!_frozen || (!_failed && _directory is null))
            {
                _frozen = true;
                ConfigureUnderLock(StringUtil.IsNullOrWhiteSpace(_configuredDirectory) ? directory : _configuredDirectory!);
            }

            return !_failed;
        }
    }

    public bool TryPublish(GlobalCoverageInfo model)
    {
        lock (_gate)
        {
            if (_failed || _published)
            {
                return !_failed;
            }

            try
            {
                if (_coveragePath is null)
                {
                    // Coverage can be enabled for in-memory module percentages without configuring
                    // an artifact directory. In that case there is simply nothing to publish.
                    _published = true;
                    return true;
                }

                var writer = new GlobalCoverageArtifactWriter();
                using var staged = writer.StageNoReplace(_coveragePath, model);
                staged.Commit();
                if (_pendingPath is not null)
                {
                    File.Delete(_pendingPath);
                }

                _published = true;
                return true;
            }
            catch
            {
                _failed = true;
                return false;
            }
        }
    }

    private void ConfigureUnderLock(string directory)
    {
        try
        {
            var candidate = Path.IsPathRooted(directory) ? directory : Path.Combine(_baseDirectory, directory);
            _directory = Path.GetFullPath(candidate);
            Directory.CreateDirectory(_directory);

            var runToken = GlobalCoverageProtocol.GetRunToken(_runIdProvider());
            var processIdentity = GlobalCoverageProtocol.GetProcessIdentity(runToken, DomainMetadata.Instance.ProcessId, Guid.NewGuid().ToString("N"));
            _coveragePath = Path.Combine(_directory, GlobalCoverageProtocol.GetCoverageFileName(processIdentity));
            _pendingPath = Path.Combine(_directory, GlobalCoverageProtocol.GetPendingMarkerFileName(processIdentity));

            // FileMode.CreateNew keeps identities collision-free and leaves a durable blocker if
            // the process exits before the atomic coverage artifact is committed.
            using var pending = new FileStream(_pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            pending.Flush(true);
        }
        catch
        {
            _failed = true;
        }
    }
}
