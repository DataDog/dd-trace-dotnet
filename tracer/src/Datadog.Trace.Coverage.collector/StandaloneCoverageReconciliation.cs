// <copyright file="StandaloneCoverageReconciliation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Owns reconciliation when the in-process collector runs without an enclosing Datadog test command.
/// </summary>
internal sealed class StandaloneCoverageReconciliation : IDisposable
{
    private readonly string _directory;
    private FileStream? _activityStream;
    private GlobalCoverageReconciliationAuthority? _authority;
    private int _publicationStarted;

    private StandaloneCoverageReconciliation(
        string directory,
        FileStream activityStream,
        GlobalCoverageReconciliationAuthority? authority)
    {
        _directory = directory;
        _activityStream = activityStream;
        _authority = authority;
    }

    public static StandaloneCoverageReconciliation? TryCreate(string directory, string runId)
    {
        FileStream? activityStream = null;
        try
        {
            var canonicalDirectory = Path.GetFullPath(directory);
            activityStream = new FileStream(
                Path.Combine(canonicalDirectory, GlobalCoverageProtocol.ReconciliationLockFileName),
                FileMode.OpenOrCreate,
                FileAccess.Read,
                FileShare.Read);
            var authority = GlobalCoverageReconciliationAuthority.TryCreate(
                canonicalDirectory,
                GlobalCoverageProtocol.GetRunToken(runId),
                GlobalCoverageProtocol.CollectorClaimKind);

            var reconciliation = new StandaloneCoverageReconciliation(canonicalDirectory, activityStream, authority);
            activityStream = null;
            return reconciliation;
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "Global coverage collector could not acquire standalone reconciliation ownership.");
            return null;
        }
        finally
        {
            activityStream?.Dispose();
        }
    }

    public bool TryPublish()
    {
        if (Interlocked.CompareExchange(ref _publicationStarted, 1, 0) != 0)
        {
            return false;
        }

        Interlocked.Exchange(ref _activityStream, null)?.Dispose();
        var authority = Interlocked.Exchange(ref _authority, null);

        GlobalCoverageReconciliationLease? lease = null;
        try
        {
            var outputPath = Path.Combine(
                _directory,
                $"session-coverage-{DateTime.UtcNow:yyyy-MM-dd_HH_mm_ss_fffffff}-{Guid.NewGuid():N}.json");
            if (!global::CoverageUtils.TryReadAndCombine(_directory, outputPath, authority, out var coverage, out lease) ||
                coverage is null)
            {
                return false;
            }

            var writer = new GlobalCoverageArtifactWriter();
            using var stagedOutput = writer.StageReplace(outputPath, coverage);
            lease!.Complete(stagedOutput.Commit);
            return true;
        }
        catch (Exception ex)
        {
            TestOptimization.Instance.Log.Warning(ex, "Global coverage collector could not publish the standalone coverage result.");
            return false;
        }
        finally
        {
            if (lease is null)
            {
                authority?.Dispose();
            }

            lease?.Dispose();
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _activityStream, null)?.Dispose();
        Interlocked.Exchange(ref _authority, null)?.Dispose();
    }
}
