// <copyright file="AgentProcessManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class AgentProcessManagerTests
    {
        [Fact]
        public void MarkHealthy_SetsHealthyStateAndResetsUnboundCount()
        {
            var metadata = new AgentProcessManager.ProcessMetadata
            {
                ProcessState = AgentProcessManager.ProcessState.ReadyToStart,
                ConsecutiveUnboundPipeChecks = 3,
            };

            metadata.MarkHealthy();

            metadata.ProcessState.Should().Be(AgentProcessManager.ProcessState.Healthy);
            metadata.ConsecutiveUnboundPipeChecks.Should().Be(0);
        }

        [Fact]
        public void KillTrackedProcess_WithNoTrackedProcess_IsNoOp()
        {
            var metadata = new AgentProcessManager.ProcessMetadata { Process = null };

            metadata.Invoking(m => m.KillTrackedProcess()).Should().NotThrow();
            metadata.Process.Should().BeNull();
        }
    }
}
