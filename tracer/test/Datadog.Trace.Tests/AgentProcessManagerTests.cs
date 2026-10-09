// <copyright file="AgentProcessManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using KeepAliveAction = Datadog.Trace.AgentProcessManager.KeepAliveAction;

namespace Datadog.Trace.Tests;

public class AgentProcessManagerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetKeepAliveAction_DoesNotStartWhileOwnProcessIsRunning(int failedHealthChecks)
    {
        // e.g. the startup wait timed out while the process we started is still running
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: true, failedHealthChecks)
                           .Should().Be(KeepAliveAction.None);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void GetKeepAliveAction_StartsWhenUnhealthyAndOwnProcessIsNotRunning(int failedHealthChecks)
    {
        // Never Restart: an agent we did not start is never stopped
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: false, failedHealthChecks)
                           .Should().Be(KeepAliveAction.Start);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void GetKeepAliveAction_RestartsOwnProcessAfterRepeatedFailedHealthChecks(int failedHealthChecks)
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: true, failedHealthChecks)
                           .Should().Be(KeepAliveAction.Restart);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetKeepAliveAction_DoesNothingWhenHealthy(bool ownProcessIsRunning)
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: true, ownProcessIsRunning, failedHealthChecks: 0)
                           .Should().Be(KeepAliveAction.None);
    }

    [Fact]
    public async Task ProcessIsHealthyAsync_IgnoresProcessNotStartedByUsWhenPipeIsMissing()
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

        (await metadata.ProcessIsHealthyAsync()).Should().BeFalse();
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
