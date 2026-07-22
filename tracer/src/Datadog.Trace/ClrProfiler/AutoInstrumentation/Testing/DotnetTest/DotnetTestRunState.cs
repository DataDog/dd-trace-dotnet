// <copyright file="DotnetTestRunState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false, true);
    private FileStream? _activityStream;
    private FileStream? _claimStream;
    private int _finalizationStarted;

    private DotnetTestRunState(
        DotnetTestCommandKind commandKind,
        TestSession? session,
        DotnetTestReconciliationRole reconciliationRole,
        string? coverageDirectory,
        string? claimPath,
        FileStream? activityStream,
        FileStream? claimStream)
    {
        CommandKind = commandKind;
        Session = session;
        ReconciliationRole = reconciliationRole;
        CoverageDirectory = coverageDirectory;
        ClaimPath = claimPath;
        _activityStream = activityStream;
        _claimStream = claimStream;
    }

    internal DotnetTestCommandKind CommandKind { get; }

    internal TestSession? Session { get; }

    internal DotnetTestReconciliationRole ReconciliationRole { get; }

    internal string? CoverageDirectory { get; }

    internal string? ClaimPath { get; }

    internal bool IsReconciliationOwner => ReconciliationRole == DotnetTestReconciliationRole.ReconciliationOwner;

    internal static DotnetTestRunState CreateNotApplicable(DotnetTestCommandKind commandKind, TestSession? session)
        => new(commandKind, session, DotnetTestReconciliationRole.NotApplicable, null, null, null, null);

    internal static DotnetTestRunState TryCreate(DotnetTestCommandKind commandKind, TestSession? session, string coverageDirectory, string runId)
    {
        FileStream? activityStream = null;
        FileStream? claimStream = null;
        string? canonicalDirectory = null;
        string? claimPath = null;
        try
        {
            canonicalDirectory = Path.GetFullPath(coverageDirectory);
            if (!Directory.Exists(canonicalDirectory))
            {
                throw new DirectoryNotFoundException("The global coverage output directory does not exist.");
            }

            var runToken = GetRunToken(runId);
            activityStream = new FileStream(
                Path.Combine(canonicalDirectory, ".dd-coverage-process-reconcile.lock"),
                FileMode.OpenOrCreate,
                FileAccess.Read,
                FileShare.Read);
            claimPath = Path.Combine(canonicalDirectory, $".dd-coverage-command-owner-{runToken}.claim");

            try
            {
                claimStream = new FileStream(claimPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (File.Exists(claimPath))
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

            using (var writer = new StreamWriter(claimStream, Utf8WithoutBom, 1024, true))
            {
                writer.Write("{\"version\":1,\"runToken\":\"");
                writer.Write(runToken);
                writer.Write("\",\"pid\":");
                writer.Write(DomainMetadata.Instance.ProcessId.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"kind\":\"");
                writer.Write(commandKind == DotnetTestCommandKind.DotnetTestCommand ? "dotnet-test" : "vstest-executor");
                writer.Write("\"}");
                writer.Flush();
                claimStream.Flush(true);
            }

            return new DotnetTestRunState(
                commandKind,
                session,
                DotnetTestReconciliationRole.ReconciliationOwner,
                canonicalDirectory,
                claimPath,
                activityStream,
                claimStream);
        }
        catch
        {
            claimStream?.Dispose();
            activityStream?.Dispose();
            if (claimStream is not null && claimPath is not null)
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

    internal bool TryBeginFinalization()
        => Interlocked.CompareExchange(ref _finalizationStarted, 1, 0) == 0;

    internal void ReleaseActivity()
        => Interlocked.Exchange(ref _activityStream, null)?.Dispose();

    internal GlobalCoverageReconciliationAuthority? TakeReconciliationAuthority()
    {
        if (!IsReconciliationOwner || ClaimPath is null)
        {
            return null;
        }

        var stream = Interlocked.Exchange(ref _claimStream, null);
        return stream is null ? null : new GlobalCoverageReconciliationAuthority(ClaimPath, stream);
    }

    public void Dispose()
    {
        ReleaseActivity();
        Interlocked.Exchange(ref _claimStream, null)?.Dispose();
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
