// <copyright file="GlobalCoverageReconciliationLease.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageReconciliationLease : IDisposable
{
    private readonly Dictionary<string, GlobalCoverageCertifiedInput> _selectedByPath;
    private FileStream? _lockStream;
    private GlobalCoverageReconciliationAuthority? _authority;
    private int _completed;

    public GlobalCoverageReconciliationLease(
        FileStream lockStream,
        GlobalCoverageReconciliationAuthority authority,
        string runToken,
        IReadOnlyList<GlobalCoverageCertifiedInput> selectedInputs,
        IReadOnlyList<GlobalCoverageCertifiedInput> allRawInputs,
        IReadOnlyList<string> readyMarkers,
        IReadOnlyList<string> pendingMarkers,
        IReadOnlyList<string> directories)
    {
        _lockStream = lockStream;
        _authority = authority;
        RunToken = runToken;
        SelectedInputs = selectedInputs;
        AllRawInputs = allRawInputs;
        _selectedByPath = new Dictionary<string, GlobalCoverageCertifiedInput>(FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var input in selectedInputs)
        {
            _selectedByPath.Add(input.Path, input);
        }

        ReadyMarkers = readyMarkers;
        PendingMarkers = pendingMarkers;
        Directories = directories;
    }

    public string RunToken { get; }

    public IReadOnlyList<GlobalCoverageCertifiedInput> SelectedInputs { get; }

    public IReadOnlyList<GlobalCoverageCertifiedInput> AllRawInputs { get; }

    public IReadOnlyList<string> ReadyMarkers { get; }

    public IReadOnlyList<string> PendingMarkers { get; }

    public IReadOnlyList<string> Directories { get; }

    public GlobalCoverageCertifiedInput? GetCertifiedInput(string path)
        => _selectedByPath.TryGetValue(path, out var input) ? input : null;

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        // Certify the complete set before moving anything. Otherwise a changed later input could
        // strand already-moved generations while their protocol markers still describe the full set.
        foreach (var input in AllRawInputs)
        {
            if (!input.Matches(input.Path))
            {
                throw new InvalidDataException("A certified global coverage artifact changed before archival.");
            }
        }

        foreach (var marker in ReadyMarkers)
        {
            if (!File.Exists(marker))
            {
                throw new InvalidDataException("A certified global coverage ready marker disappeared before archival.");
            }
        }

        foreach (var marker in PendingMarkers)
        {
            if (!File.Exists(marker))
            {
                throw new InvalidDataException("A certified global coverage pending marker disappeared before archival.");
            }
        }

        var publicationId = Guid.NewGuid().ToString("N");
        var archives = new Dictionary<string, string>(FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var directory in Directories)
        {
            var archive = Path.Combine(directory, ".dd-coverage-completed", $"{RunToken}-{publicationId}");
            Directory.CreateDirectory(archive);
            archives[directory] = archive;
        }

        var sources = new List<string>(ReadyMarkers.Count + PendingMarkers.Count + AllRawInputs.Count);
        var destinations = new List<string>(sources.Capacity);
        foreach (var marker in ReadyMarkers)
        {
            AddToArchivePlan(marker);
        }

        foreach (var marker in PendingMarkers)
        {
            AddToArchivePlan(marker);
        }

        var rawStart = sources.Count;
        foreach (var input in AllRawInputs)
        {
            AddToArchivePlan(input.Path);
        }

        var movedCount = 0;
        try
        {
            for (var i = 0; i < sources.Count; i++)
            {
                File.Move(sources[i], destinations[i]);
                movedCount++;

                if (i >= rawStart && !AllRawInputs[i - rawStart].Matches(destinations[i]))
                {
                    throw new InvalidDataException("An archived global coverage artifact does not match its certified contents.");
                }
            }
        }
        catch (Exception archivalException)
        {
            var rollbackExceptions = new List<Exception>();
            for (var i = movedCount - 1; i >= 0; i--)
            {
                try
                {
                    File.Move(destinations[i], sources[i]);
                }
                catch (Exception rollbackException)
                {
                    rollbackExceptions.Add(rollbackException);
                }
            }

            if (rollbackExceptions.Count > 0)
            {
                rollbackExceptions.Insert(0, archivalException);
                throw new AggregateException("Global coverage archival failed and could not be fully rolled back.", rollbackExceptions);
            }

            throw;
        }

        Interlocked.Exchange(ref _authority, null)?.Complete();

        void AddToArchivePlan(string source)
        {
            var directory = Path.GetDirectoryName(source);
            if (directory is null)
            {
                ThrowHelper.ThrowInvalidOperationException("A certified coverage artifact has no parent directory.");
            }

            if (!archives.TryGetValue(directory, out var archive))
            {
                ThrowHelper.ThrowInvalidOperationException("A certified coverage artifact belongs to an unknown directory.");
            }

            var destination = Path.Combine(archive, Path.GetFileName(source));
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                throw new IOException("A global coverage archive destination already exists.");
            }

            sources.Add(source);
            destinations.Add(destination);
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _authority, null)?.Dispose();
        Interlocked.Exchange(ref _lockStream, null)?.Dispose();
    }
}
