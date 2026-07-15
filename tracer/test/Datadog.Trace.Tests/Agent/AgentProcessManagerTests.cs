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

        [Theory]
        // A bound pipe is healthy regardless of the process signal and resets the unbound count.
        [InlineData(true, true, 0)]
        [InlineData(true, false, 5)]
        public void EvaluateDebouncedHealth_BoundPipe_IsHealthyAndResetsCount(bool pipeBound, bool programRunning, int currentCount)
        {
            var (isHealthy, nextCount) = AgentProcessManager.EvaluateDebouncedHealth(pipeBound, programRunning, currentCount);

            isHealthy.Should().BeTrue();
            nextCount.Should().Be(0);
        }

        [Theory]
        // An unbound pipe on a dead process is unhealthy immediately - no grace period - and the count is left untouched.
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void EvaluateDebouncedHealth_UnboundPipeAndNotRunning_IsUnhealthyWithoutGrace(int currentCount)
        {
            var (isHealthy, nextCount) = AgentProcessManager.EvaluateDebouncedHealth(pipeBound: false, programRunning: false, consecutiveUnboundChecks: currentCount);

            isHealthy.Should().BeFalse();
            nextCount.Should().Be(currentCount);
        }

        [Fact]
        public void EvaluateDebouncedHealth_UnboundPipeButStillRunning_ToleratesUpToGraceThenFails()
        {
            // A still-running process with an unbound pipe rides out transient File.Exists blips until the
            // grace count is reached, at which point it is declared unhealthy.
            var firstMiss = AgentProcessManager.EvaluateDebouncedHealth(pipeBound: false, programRunning: true, consecutiveUnboundChecks: 0);
            firstMiss.IsHealthy.Should().BeTrue();
            firstMiss.NextUnboundCount.Should().Be(1);

            var secondMiss = AgentProcessManager.EvaluateDebouncedHealth(pipeBound: false, programRunning: true, consecutiveUnboundChecks: firstMiss.NextUnboundCount);
            secondMiss.IsHealthy.Should().BeFalse();
            secondMiss.NextUnboundCount.Should().Be(AgentProcessManager.ProcessMetadata.UnboundPipeGraceChecks);
        }

        [Fact]
        public void EvaluateDebouncedHealth_SlowBindingProcess_BecomesHealthyOncePipeBinds()
        {
            // A pipe-only process that stays alive but takes a while to bind its pipe should be tolerated
            // during the grace window and, once the pipe binds, be treated as healthy again with a reset
            // count - it must not be declared unhealthy (which would trigger a needless kill/restart).
            var transientMiss = AgentProcessManager.EvaluateDebouncedHealth(pipeBound: false, programRunning: true, consecutiveUnboundChecks: 0);
            transientMiss.IsHealthy.Should().BeTrue();

            var afterBind = AgentProcessManager.EvaluateDebouncedHealth(pipeBound: true, programRunning: true, consecutiveUnboundChecks: transientMiss.NextUnboundCount);
            afterBind.IsHealthy.Should().BeTrue();
            afterBind.NextUnboundCount.Should().Be(0);
        }

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
            // Nothing has been started yet, so there is no handle to tear down. The kill must be a
            // safe no-op rather than throwing on a null Process.
            var metadata = new AgentProcessManager.ProcessMetadata { Process = null };

            metadata.Invoking(m => m.KillTrackedProcess()).Should().NotThrow();
            metadata.Process.Should().BeNull();
        }
    }
}
