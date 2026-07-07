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
        [Theory]
        // requirePipeBound = true: only the bound pipe counts as healthy.
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, true)]
        [InlineData(false, true, true, false)]
        [InlineData(false, false, true, false)]
        // requirePipeBound = false: pipe bound OR process running counts as healthy.
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, false, false)]
        [InlineData(true, true, false, true)]
        public void EvaluateHealth_ReturnsExpected(bool pipeBound, bool programRunning, bool requirePipeBound, bool expected)
        {
            AgentProcessManager.EvaluateHealth(pipeBound, programRunning, requirePipeBound).Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(4, 5)]
        public void NextUnboundCount_Increments(int current, int expected)
        {
            AgentProcessManager.NextUnboundCount(current).Should().Be(expected);
        }

        [Fact]
        public void UnboundPipeGraceChecks_ToleratesOneTransientMiss()
        {
            // With a grace of 2, the first unbound read (count 1) is still tolerated and the
            // second (count 2) declares the process unhealthy. Guards against a single File.Exists blip.
            var afterFirstMiss = AgentProcessManager.NextUnboundCount(0);
            var afterSecondMiss = AgentProcessManager.NextUnboundCount(afterFirstMiss);

            (afterFirstMiss < AgentProcessManager.ProcessMetadata.UnboundPipeGraceChecks).Should().BeTrue();
            (afterSecondMiss < AgentProcessManager.ProcessMetadata.UnboundPipeGraceChecks).Should().BeFalse();
        }
    }
}
