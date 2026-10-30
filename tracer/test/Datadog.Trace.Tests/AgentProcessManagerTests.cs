// <copyright file="AgentProcessManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using KeepAliveAction = Datadog.Trace.AgentProcessManager.KeepAliveAction;
using ProcessState = Datadog.Trace.AgentProcessManager.ProcessState;

namespace Datadog.Trace.Tests;

public class AgentProcessManagerTests
{
    private const int Cap = 300_000;

    private enum AgentCheck
    {
        /// <summary>The agent answers.</summary>
        Healthy,

        /// <summary>The pipe exists, but the agent does not answer in time.</summary>
        NotAnswering,

        /// <summary>No agent has the pipe.</summary>
        Gone,
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetKeepAliveAction_DoesNotStartWhileOwnProcessIsRunning(int failedHealthChecks)
    {
        // For example, the startup wait timed out while the process we started is still running
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: true, otherAgentHasPipe: false, failedHealthChecks)
                           .Should().Be(KeepAliveAction.None);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void GetKeepAliveAction_StartsAfterOneFailedCheckWhenNoOtherAgentHasThePipe(int failedHealthChecks)
    {
        // Never Restart, even after many failed checks, because we never stop an agent we did not start
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: false, otherAgentHasPipe: false, failedHealthChecks)
                           .Should().Be(KeepAliveAction.Start);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void GetKeepAliveAction_WaitsForRepeatedFailuresWhileAnAgentWeDidNotStartHasThePipe(int failedHealthChecks, bool expectStart)
    {
        // For example, one slow GET /info from another worker's agent must not start a copy that cannot take the pipe
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: false, otherAgentHasPipe: true, failedHealthChecks)
                           .Should().Be(expectStart ? KeepAliveAction.Start : KeepAliveAction.None);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void GetKeepAliveAction_RestartsOwnProcessAfterRepeatedFailedHealthChecks(int failedHealthChecks)
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: false, ownProcessIsRunning: true, otherAgentHasPipe: false, failedHealthChecks)
                           .Should().Be(KeepAliveAction.Restart);
    }

    [Theory]
    [CombinatorialData]
    public void GetKeepAliveAction_DoesNothingWhenHealthy(bool ownProcessIsRunning, bool otherAgentHasPipe)
    {
        AgentProcessManager.GetKeepAliveAction(isHealthy: true, ownProcessIsRunning, otherAgentHasPipe, failedHealthChecks: 0)
                           .Should().Be(KeepAliveAction.None);
    }

    [Fact]
    public async Task ProcessIsHealthyAsync_IgnoresProcessNotStartedByUsWhenPipeIsMissing()
    {
        // A process with the agent's name is running (this test process), but nothing serves the pipe
        string runningProcessPath;
        using (var currentProcess = Process.GetCurrentProcess())
        {
            var mainModule = currentProcess.MainModule ?? throw new InvalidOperationException("The test process has no main module.");
            runningProcessPath = mainModule.FileName;
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
    [InlineData(6, 64_000)]
    [InlineData(7, 128_000)]
    [InlineData(8, 256_000)]
    [InlineData(9, 300_000)]
    [InlineData(1_000, 300_000)]
    [InlineData(int.MaxValue, 300_000)]
    public void GetRetryDelay_DoublesFromTwoSecondsUpToTheDefaultFiveMinuteCap(int sequentialFailures, int expectedDelay)
    {
        var defaultCap = AgentProcessManager.GetMaxRetryInterval(ImmutableAzureAppServiceSettings.DefaultAgentMaxRetryIntervalSeconds);
        AgentProcessManager.GetRetryDelay(sequentialFailures, defaultCap).Should().Be(expectedDelay);
    }

    [Theory]
    [InlineData(5, 32_000)]
    [InlineData(6, 60_000)]
    [InlineData(int.MaxValue, 60_000)]
    public void GetRetryDelay_UsesTheConfiguredCap(int sequentialFailures, int expectedDelay)
    {
        AgentProcessManager.GetRetryDelay(sequentialFailures, AgentProcessManager.GetMaxRetryInterval(60)).Should().Be(expectedDelay);
    }

    [Theory]
    [InlineData(300, 300_000)]
    [InlineData(60, 60_000)]
    [InlineData(1, 2_000)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void GetMaxRetryInterval_ConvertsSecondsWithoutOverflowAndNeverGoesBelowTheFirstDelay(int seconds, int expectedMilliseconds)
    {
        AgentProcessManager.GetMaxRetryInterval(seconds).Should().Be(expectedMilliseconds);
    }

    [Theory]
    [InlineData(21, 2_097_152_000)]
    [InlineData(22, int.MaxValue)]
    [InlineData(1_000, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void GetRetryDelay_DoesNotOverflowAtTheLargestAcceptedCap(int sequentialFailures, int expectedDelay)
    {
        var largestCap = AgentProcessManager.GetMaxRetryInterval(int.MaxValue);
        AgentProcessManager.GetRetryDelay(sequentialFailures, largestCap).Should().Be(expectedDelay);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(1_000, true)]
    public void IsRetryLoggedAboveDebug_LogsTheFirstFailureAndEveryRetryAtTheCap(int sequentialFailures, bool expected)
    {
        // The wait doubles from 2 s and reaches a 5 minute cap at the 9th failure in a row
        AgentProcessManager.IsRetryLoggedAboveDebug(sequentialFailures, maxRetryInterval: 300_000).Should().Be(expected);
    }

    [Fact]
    public void GetStartDelay_IsImmediateUnlessOurOwnProcessJustExited()
    {
        var cap = AgentProcessManager.GetMaxRetryInterval(ImmutableAzureAppServiceSettings.DefaultAgentMaxRetryIntervalSeconds);

        // First start, or an agent we did not start went away
        AgentProcessManager.GetStartDelay(ownProcessExited: false, sequentialFailures: 5, cap).Should().Be(0);

        // Our own agent exited: back off with the shared counter
        AgentProcessManager.GetStartDelay(ownProcessExited: true, sequentialFailures: 1, cap).Should().Be(2_000);
        AgentProcessManager.GetStartDelay(ownProcessExited: true, sequentialFailures: 4, cap).Should().Be(16_000);
    }

    [Fact]
    public void RecordOwnProcessExit_CountsAChildThatExitsBetweenPolls()
    {
        // An agent that started, failed its startup health check, then exited before the next poll
        var metadata = new AgentProcessManager.ProcessMetadata { Name = "test-agent", ProcessPath = "test-agent" };

        for (var expectedFailures = 1; expectedFailures <= 2; expectedFailures++)
        {
            var child = StartShortLivedChild();
            child.WaitForExit(10_000).Should().BeTrue();
            metadata.Process = child;

            AgentProcessManager.RecordOwnProcessExit(metadata).Should().BeTrue();
            metadata.SequentialFailures.Should().Be(expectedFailures);
            metadata.Process.Should().BeNull();
        }

        // Each exit is counted once
        AgentProcessManager.RecordOwnProcessExit(metadata).Should().BeFalse();
        metadata.SequentialFailures.Should().Be(2);
    }

    [Fact]
    public void RecordOwnProcessExit_IgnoresAChildThatIsStillRunning()
    {
        var metadata = new AgentProcessManager.ProcessMetadata { Name = "test-agent", ProcessPath = "test-agent" };
        var child = StartLongRunningChild();
        try
        {
            metadata.Process = child;

            AgentProcessManager.RecordOwnProcessExit(metadata).Should().BeFalse();
            metadata.SequentialFailures.Should().Be(0);
            metadata.Process.Should().BeSameAs(child);
        }
        finally
        {
            child.Kill();
            child.WaitForExit(10_000);
            child.Dispose();
        }
    }

    [SkippableFact]
    public async Task ProcessIsHealthyAsync_TraceAgentThatAnswersIsHealthy()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var pipeName = $"dd-test-agent-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var serve = Task.Run(
            async () =>
            {
                await server.WaitForConnectionAsync();
                var buffer = new byte[1024];
                (await server.ReadAsync(buffer, 0, buffer.Length)).Should().BeGreaterThan(0); // the request
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
                try
                {
                    await server.WriteAsync(response, 0, response.Length);
                }
                catch (IOException)
                {
                    // The probe closes the pipe once it has read the status line. The pipe has no
                    // buffer, so the rest of this write can fail with "Pipe is broken".
                }
            });

        var metadata = new AgentProcessManager.ProcessMetadata { Name = "test-agent", PipeName = pipeName, IsTraceAgent = true };

        (await metadata.ProcessIsHealthyAsync()).Should().BeTrue();
        metadata.LastHealthCheckFailure.Should().BeNull();
        await serve;
    }

    [SkippableFact]
    public async Task ProcessIsHealthyAsync_TraceAgentThatAcceptsButNeverAnswersFailsWithinTheTimeout()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var pipeName = $"dd-test-agent-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        // Read the request, then never answer. The pipe has no buffer, so the probe's write only
        // completes once this side reads it.
        var requestBytes = Task.Run(
            async () =>
            {
                await server.WaitForConnectionAsync();
                var buffer = new byte[1024];
                return await server.ReadAsync(buffer, 0, buffer.Length);
            });

        var metadata = new AgentProcessManager.ProcessMetadata { Name = "test-agent", PipeName = pipeName, IsTraceAgent = true };

        var stopwatch = Stopwatch.StartNew();
        (await metadata.ProcessIsHealthyAsync()).Should().BeFalse();
        // Allow a little slack over the timeout for slow CI machines
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(AgentProcessManager.TraceAgentResponseTimeout + 1_000));
        metadata.LastHealthCheckFailure.Should().Contain("no response to GET /info").And.Contain(pipeName);
        (await requestBytes).Should().BeGreaterThan(0);

        // The probe closed its connection: the server's next read ends instead of waiting forever
        var closed = await ReadUntilClosedAsync(server, TimeSpan.FromSeconds(10));
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForExistingAgentAsync_StartsWhenNoAgentExists()
    {
        var agent = new ScriptedAgent(AgentCheck.Gone);

        await AgentProcessManager.CheckForExistingAgentAsync(agent.Metadata, Cap);

        agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
    }

    [Fact]
    public async Task CheckForExistingAgentAsync_UsesAnAgentThatStaysUpEvenIfItAnswersSlowly()
    {
        // A slow answer is not a shutdown while the pipe exists
        var agent = new ScriptedAgent(AgentCheck.Healthy, AgentCheck.NotAnswering, AgentCheck.Healthy) { RepeatLastCheck = true };

        await AgentProcessManager.CheckForExistingAgentAsync(agent.Metadata, Cap);

        agent.Metadata.ProcessState.Should().Be(ProcessState.Healthy);
        agent.Metadata.Process.Should().BeNull();
    }

    [Fact]
    public async Task CheckForExistingAgentAsync_StartsWhenTheAgentItFoundShutsDown()
    {
        var agent = new ScriptedAgent(AgentCheck.Healthy, AgentCheck.Healthy, AgentCheck.Gone);

        await AgentProcessManager.CheckForExistingAgentAsync(agent.Metadata, Cap);

        agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
    }

    [Fact]
    public async Task CheckForExistingAgentAsync_CountsASlowFirstAnswerAsOneOfTheFailedChecksBeforeStarting()
    {
        // Another worker's agent holds the pipe but answers slowly: a copy started now could not take the pipe
        var agent = new ScriptedAgent(AgentCheck.NotAnswering, AgentCheck.NotAnswering, AgentCheck.NotAnswering);

        await AgentProcessManager.CheckForExistingAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
        agent.Delays.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAgentAsync_WaitsForThreeFailedChecksBeforeReplacingAnAgentWeDidNotStart()
    {
        var agent = new ScriptedAgent(AgentCheck.NotAnswering, AgentCheck.NotAnswering, AgentCheck.NotAnswering);
        agent.Metadata.ProcessState = ProcessState.Healthy;

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
        agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
        agent.Delays.Should().BeEmpty();
        agent.RemainingChecks.Should().Be(0);
    }

    [Fact]
    public async Task CheckAgentAsync_StartsAtOnceWhenAnAgentWeDidNotStartIsGone()
    {
        var agent = new ScriptedAgent(AgentCheck.Gone);
        agent.Metadata.ProcessState = ProcessState.Healthy;

        await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);

        agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
        agent.Delays.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAgentAsync_AfterRestartingOurAgentAndUsingAnotherWorkersAgent_OneSlowAnswerDoesNotStartACopy()
    {
        var agent = new ScriptedAgent(
            AgentCheck.NotAnswering,
            AgentCheck.NotAnswering,
            AgentCheck.NotAnswering, // third failed check: our agent is restarted
            AgentCheck.Healthy, // after the retry wait, another worker's agent answers
            AgentCheck.NotAnswering); // one slow answer from that agent
        var child = StartLongRunningChild();
        var childPid = child.Id;
        try
        {
            agent.Metadata.Process = child;
            agent.Metadata.ProcessState = ProcessState.Healthy;

            for (var check = 0; check < 3; check++)
            {
                await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
            }

            IsRunning(childPid).Should().BeFalse();
            agent.Metadata.Process.Should().BeNull();
            agent.Metadata.ProcessState.Should().Be(ProcessState.Healthy);
            agent.Metadata.SequentialFailures.Should().Be(1);
            agent.Delays.Should().Equal(2_000);

            await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);

            agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);
            agent.RemainingChecks.Should().Be(0);
        }
        finally
        {
            StopChild(child);
        }
    }

    [Fact]
    public async Task CheckAgentAsync_CountsAnExitOfOurAgentDuringTheHealthCheckAndBacksOffEvenWhileAnotherAgentHasThePipe()
    {
        var agent = new ScriptedAgent(AgentCheck.NotAnswering, AgentCheck.NotAnswering);
        var child = StartLongRunningChild();
        try
        {
            agent.Metadata.Process = child;
            agent.Metadata.ProcessState = ProcessState.Healthy;
            agent.BeforeNextCheck = () => StopChild(child);

            await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);

            // Our agent's exit is the reason to start a new one, even while another agent holds the pipe
            agent.Metadata.SequentialFailures.Should().Be(1);
            agent.Metadata.Process.Should().BeNull();
            agent.Delays.Should().Equal(2_000);
            agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
        }
        finally
        {
            StopChild(child);
        }
    }

    [Fact]
    public async Task CheckAgentAsync_KeepsOurRunningAgentUntilItFailsThreeChecks()
    {
        var agent = new ScriptedAgent(AgentCheck.NotAnswering, AgentCheck.NotAnswering, AgentCheck.NotAnswering, AgentCheck.Gone);
        var child = StartLongRunningChild();
        var childPid = child.Id;
        try
        {
            agent.Metadata.Process = child;
            agent.Metadata.ProcessState = ProcessState.Healthy;

            await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
            await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
            agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);
            agent.Metadata.Process.Should().BeSameAs(child);
            IsRunning(childPid).Should().BeTrue();

            // The third failed check restarts it after the retry wait, because no other agent answers by then
            await AgentProcessManager.CheckAgentAsync(agent.Metadata, Cap);
            IsRunning(childPid).Should().BeFalse();
            agent.Metadata.ProcessState.Should().Be(ProcessState.ReadyToStart);
            agent.Metadata.SequentialFailures.Should().Be(1);
            agent.Delays.Should().Equal(2_000);
        }
        finally
        {
            StopChild(child);
        }
    }

    [Fact]
    public async Task StartAgentAsync_ReportsAnAgentThatExitsDuringStartupAsFaulted()
    {
        var agent = new ScriptedAgent(AgentCheck.Gone) { RepeatLastCheck = true, RealDelays = true };
        SetShortLivedChildCommand(agent.Metadata);
        agent.Metadata.ProcessState = ProcessState.ReadyToStart;

        await AgentProcessManager.StartAgentAsync(agent.Metadata, Cap);

        agent.Metadata.ProcessState.Should().Be(ProcessState.Faulted);
        agent.Metadata.Process.Should().BeNull();
    }

    [Fact]
    public async Task StartAgentAsync_LeavesAnAgentThatIsStillStartingRunning()
    {
        // Real waits, so the startup wait times out as it would in production
        var agent = new ScriptedAgent(AgentCheck.Gone) { RepeatLastCheck = true, RealDelays = true };
        SetLongRunningChildCommand(agent.Metadata);
        agent.Metadata.ProcessState = ProcessState.ReadyToStart;
        try
        {
            await AgentProcessManager.StartAgentAsync(agent.Metadata, Cap);

            agent.Metadata.ProcessState.Should().Be(ProcessState.Unhealthy);
            agent.Metadata.Process.Should().NotBeNull();
            agent.Metadata.Process.HasExited.Should().BeFalse();
        }
        finally
        {
            StopChild(agent.Metadata.Process);
        }
    }

    private static async Task<bool> ReadUntilClosedAsync(Stream stream, TimeSpan timeout)
    {
        // Pipe reads may ignore cancellation on .NET Framework, so bound the wait with WhenAny instead
        var readToEnd = Task.Run(
            async () =>
            {
                var buffer = new byte[1024];
                try
                {
                    while (await stream.ReadAsync(buffer, 0, buffer.Length) > 0)
                    {
                    }
                }
                catch (IOException)
                {
                    // A broken pipe also means the client closed it
                }
            });

        return await Task.WhenAny(readToEnd, Task.Delay(timeout)) == readToEnd;
    }

    private static ProcessStartInfo ShortLivedChildStartInfo()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
               ? new ProcessStartInfo("cmd.exe", "/c exit 3") { UseShellExecute = false, CreateNoWindow = true }
               : new ProcessStartInfo("/bin/sh", "-c \"exit 3\"") { UseShellExecute = false };

    private static ProcessStartInfo LongRunningChildStartInfo()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
               ? new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }
               : new ProcessStartInfo("/bin/sh", "-c \"sleep 60\"") { UseShellExecute = false };

    private static Process StartShortLivedChild() => Process.Start(ShortLivedChildStartInfo());

    private static Process StartLongRunningChild() => Process.Start(LongRunningChildStartInfo());

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void StopChild(Process child)
    {
        if (child is null)
        {
            return;
        }

        try
        {
            if (!child.HasExited)
            {
                child.Kill();
            }

            child.WaitForExit(10_000);
        }
        catch (InvalidOperationException)
        {
            // Already disposed by the code under test
        }
    }

    private static void SetShortLivedChildCommand(AgentProcessManager.ProcessMetadata metadata)
        => SetChildCommand(metadata, ShortLivedChildStartInfo());

    private static void SetLongRunningChildCommand(AgentProcessManager.ProcessMetadata metadata)
        => SetChildCommand(metadata, LongRunningChildStartInfo());

    private static void SetChildCommand(AgentProcessManager.ProcessMetadata metadata, ProcessStartInfo startInfo)
    {
        metadata.ProcessPath = startInfo.FileName;
        metadata.ProcessArguments = startInfo.Arguments;
    }

    /// <summary>
    /// Stands in for the agent's named pipe: each health check returns the next scripted answer, and waits are recorded instead of slept.
    /// </summary>
    private sealed class ScriptedAgent
    {
        private readonly Queue<AgentCheck> _checks;
        private AgentCheck _lastCheck;

        public ScriptedAgent(params AgentCheck[] checks)
        {
            _checks = new Queue<AgentCheck>(checks);
            Metadata = new AgentProcessManager.ProcessMetadata { Name = "test-agent", ProcessPath = "test-agent", PipeName = "test-pipe" };
            Metadata.CheckHealthAsync = CheckHealthAsync;
            Metadata.DelayAsync = DelayAsync;
        }

        public AgentProcessManager.ProcessMetadata Metadata { get; }

        public List<int> Delays { get; } = [];

        public int RemainingChecks => _checks.Count;

        public bool RepeatLastCheck { get; set; }

        public bool RealDelays { get; set; }

        public Action BeforeNextCheck { get; set; }

        private Task<bool> CheckHealthAsync()
        {
            var before = BeforeNextCheck;
            BeforeNextCheck = null;
            before?.Invoke();

            if (_checks.Count > 0)
            {
                _lastCheck = _checks.Dequeue();
            }
            else if (!RepeatLastCheck)
            {
                throw new InvalidOperationException("The keep-alive ran more health checks than the test scripted.");
            }

            Metadata.PipeIsPresent = _lastCheck != AgentCheck.Gone;
            Metadata.LastHealthCheckFailure = _lastCheck == AgentCheck.Healthy ? null : _lastCheck.ToString();
            return Task.FromResult(_lastCheck == AgentCheck.Healthy);
        }

        private Task DelayAsync(int milliseconds)
        {
            if (RealDelays)
            {
                return Task.Delay(milliseconds);
            }

            Delays.Add(milliseconds);
            return Task.CompletedTask;
        }
    }
}
