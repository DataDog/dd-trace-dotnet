// <copyright file="AgentProcessManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using FluentAssertions;
using Xunit;
using KeepAliveAction = Datadog.Trace.AgentProcessManager.KeepAliveAction;

namespace Datadog.Trace.Tests;

public class AgentProcessManagerTests
{
    [Fact]
    public void GetKeepAliveAction_DoesNotStartWhileOwnProcessIsRunning()
    {
        // e.g. the startup wait timed out while the process we started is still running
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: true)
                           .Should().Be(KeepAliveAction.None);
    }

    [Fact]
    public void GetKeepAliveAction_StartsWhenUnhealthyAndOwnProcessIsNotRunning()
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: false)
                           .Should().Be(KeepAliveAction.Start);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetKeepAliveAction_DoesNothingWhenHealthy(bool ownProcessIsRunning)
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: true, ownProcessIsRunning)
                           .Should().Be(KeepAliveAction.None);
    }

    [Fact]
    public void ProcessIsHealthy_IgnoresProcessNotStartedByUsWhenPipeIsMissing()
    {
        // A process with the agent's name is running (this test process), but nothing serves the pipe
        string runningProcessPath;
        using (var currentProcess = Process.GetCurrentProcess())
        {
            runningProcessPath = currentProcess.MainModule!.FileName;
        }

        var metadata = new AgentProcessManager.ProcessMetadata
        {
            Name = "test-agent",
            PipeName = $"dd-test-missing-pipe-{Guid.NewGuid():N}",
            ProcessPath = runningProcessPath,
        };

        metadata.ProcessIsHealthy().Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 2_000)]
    [InlineData(1, 2_000)]
    [InlineData(2, 4_000)]
    [InlineData(3, 8_000)]
    [InlineData(4, 16_000)]
    [InlineData(5, 32_000)]
    [InlineData(6, 60_000)]
    [InlineData(7, 60_000)]
    [InlineData(1_000, 60_000)]
    [InlineData(int.MaxValue, 60_000)]
    public void GetRetryDelay_DoublesFromTwoSecondsUpToSixtySeconds(int sequentialFailures, int expectedDelay)
    {
        AgentProcessManager.GetRetryDelay(sequentialFailures).Should().Be(expectedDelay);
    }
}
