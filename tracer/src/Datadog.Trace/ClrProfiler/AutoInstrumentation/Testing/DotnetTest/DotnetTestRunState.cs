// <copyright file="DotnetTestRunState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

internal enum DotnetTestCommandKind
{
    DotnetTestCommand,
    VSTestExecutor,
}

internal enum DotnetTestReconciliationRole
{
    NotApplicable,
    ReconciliationOwner,
    NonOwnerParticipant,
    SuppressedAuthorityFailure,
}

internal sealed class DotnetTestRunState : IDisposable
{
    private FileStream? _activityStream;
    private GlobalCoverageReconciliationAuthority? _authority;
    private int _finalizationStarted;

    private DotnetTestRunState(
        DotnetTestCommandKind commandKind,
        TestSession? session,
        DotnetTestReconciliationRole reconciliationRole,
        string? coverageDirectory,
        string? claimPath,
        FileStream? activityStream,
        GlobalCoverageReconciliationAuthority? authority)
    {
        CommandKind = commandKind;
        Session = session;
        ReconciliationRole = reconciliationRole;
        CoverageDirectory = coverageDirectory;
        ClaimPath = claimPath;
        _activityStream = activityStream;
        _authority = authority;
    }

    public DotnetTestCommandKind CommandKind { get; }

    public TestSession? Session { get; }

    public DotnetTestReconciliationRole ReconciliationRole { get; }

    public string? CoverageDirectory { get; }

    public string? ClaimPath { get; }

    public bool IsReconciliationOwner => ReconciliationRole == DotnetTestReconciliationRole.ReconciliationOwner;

    public static DotnetTestRunState CreateNotApplicable(DotnetTestCommandKind commandKind, TestSession? session)
        => new(commandKind, session, DotnetTestReconciliationRole.NotApplicable, null, null, null, null);

    public static DotnetTestRunState TryCreate(DotnetTestCommandKind commandKind, TestSession? session, string coverageDirectory, string runId)
    {
        FileStream? activityStream = null;
        GlobalCoverageReconciliationAuthority? authority = null;
        string? canonicalDirectory = null;
        string? claimPath = null;
        try
        {
            canonicalDirectory = Path.GetFullPath(coverageDirectory);
            if (!Directory.Exists(canonicalDirectory))
            {
                throw new DirectoryNotFoundException("The global coverage output directory does not exist.");
            }

            var runToken = GlobalCoverageProtocol.GetRunToken(runId);
            activityStream = new FileStream(
                Path.Combine(canonicalDirectory, GlobalCoverageProtocol.ReconciliationLockFileName),
                FileMode.OpenOrCreate,
                FileAccess.Read,
                FileShare.Read);
            claimPath = Path.Combine(
                canonicalDirectory,
                GlobalCoverageProtocol.GetCommandOwnerClaimFileName(runToken));

            var claimKind = commandKind == DotnetTestCommandKind.DotnetTestCommand
                                ? GlobalCoverageProtocol.DotnetTestClaimKind
                                : GlobalCoverageProtocol.VSTestExecutorClaimKind;
            authority = GlobalCoverageReconciliationAuthority.TryCreate(
                canonicalDirectory,
                runToken,
                claimKind);
            if (authority is null)
            {
                return new DotnetTestRunState(
                    commandKind,
                    session,
                    DotnetTestReconciliationRole.NonOwnerParticipant,
                    canonicalDirectory,
                    claimPath,
                    activityStream,
                    null);
            }

            return new DotnetTestRunState(
                commandKind,
                session,
                DotnetTestReconciliationRole.ReconciliationOwner,
                canonicalDirectory,
                claimPath,
                activityStream,
                authority);
        }
        catch
        {
            authority?.Dispose();
            activityStream?.Dispose();
            if (authority is not null && claimPath is not null)
            {
                TryDelete(claimPath);
            }

            return new DotnetTestRunState(
                commandKind,
                session,
                DotnetTestReconciliationRole.SuppressedAuthorityFailure,
                canonicalDirectory,
                claimPath,
                null,
                null);
        }
    }

    public bool TryBeginFinalization()
        => Interlocked.CompareExchange(ref _finalizationStarted, 1, 0) == 0;

    public void ReleaseActivity()
        => Interlocked.Exchange(ref _activityStream, null)?.Dispose();

    public GlobalCoverageReconciliationAuthority? TakeReconciliationAuthority()
    {
        if (!IsReconciliationOwner || ClaimPath is null)
        {
            return null;
        }

        return Interlocked.Exchange(ref _authority, null);
    }

    public void Dispose()
    {
        ReleaseActivity();
        Interlocked.Exchange(ref _authority, null)?.Dispose();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
