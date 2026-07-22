// <copyright file="DotnetTestRunStateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class DotnetTestRunStateTests
{
    [Fact]
    public void CreateNewClaimElectsOneOwnerAndSharedActivityBlocksReconciliation()
    {
        var directory = CreateDirectory();
        try
        {
            using var owner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            using var participant = DotnetTestRunState.TryCreate(DotnetTestCommandKind.VSTestExecutor, null, directory, "run-id");

            owner.ReconciliationRole.Should().Be(DotnetTestReconciliationRole.ReconciliationOwner);
            participant.ReconciliationRole.Should().Be(DotnetTestReconciliationRole.NonOwnerParticipant);
            owner.ClaimPath.Should().NotBeNull();
            File.Exists(owner.ClaimPath!).Should().BeTrue();

            owner.ReleaseActivity();
            var exclusiveOpen = () => new FileStream(
                Path.Combine(directory, ".dd-coverage-process-reconcile.lock"),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            exclusiveOpen.Should().Throw<IOException>();

            participant.ReleaseActivity();
            using var exclusive = exclusiveOpen();
            exclusive.Should().NotBeNull();
            using var authority = owner.TakeReconciliationAuthority();
            authority!.Complete();
            File.Exists(owner.ClaimPath!).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void OwnerClaimIsFlushedWithoutBomAndRemainsDurableAfterOwnerDisposal()
    {
        var directory = CreateDirectory();
        try
        {
            var owner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            var claimPath = owner.ClaimPath!;

            owner.Dispose();

            File.Exists(claimPath).Should().BeTrue();
            var claimBytes = File.ReadAllBytes(claimPath);
            (claimBytes.Length >= 3 && claimBytes[0] == 0xef && claimBytes[1] == 0xbb && claimBytes[2] == 0xbf).Should().BeFalse();
            Encoding.UTF8.GetString(claimBytes).Should().Contain("\"version\":1").And.Contain("\"kind\":\"dotnet-test\"");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FinalizationAdmissionIsOneShot()
    {
        using var state = DotnetTestRunState.CreateNotApplicable(DotnetTestCommandKind.DotnetTestCommand, null);

        state.TryBeginFinalization().Should().BeTrue();
        state.TryBeginFinalization().Should().BeFalse();
    }

    [Fact]
    public void MissingCoverageDirectoryFailsAuthorityWithoutCreatingAClaim()
    {
        var parent = CreateDirectory();
        try
        {
            var missing = Path.Combine(parent, "missing");
            using var state = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, missing, "run-id");

            state.ReconciliationRole.Should().Be(DotnetTestReconciliationRole.SuppressedAuthorityFailure);
            Directory.Exists(missing).Should().BeFalse();
            Directory.GetFiles(parent, "*.claim", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
}
