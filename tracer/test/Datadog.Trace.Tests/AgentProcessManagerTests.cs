// <copyright file="AgentProcessManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
}
