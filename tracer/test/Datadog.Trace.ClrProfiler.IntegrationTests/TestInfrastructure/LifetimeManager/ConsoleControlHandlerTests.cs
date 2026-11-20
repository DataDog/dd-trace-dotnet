// <copyright file="ConsoleControlHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LifetimeManager;

public class ConsoleControlHandlerTests : TestHelper
{
    /// <summary>
    /// 0xE0434352 as a signed int: the CLR's "unhandled managed exception" exit code, which is what
    /// ControlCHooker's critical finalizer produces when the console control handler list has been reset.
    /// </summary>
    private const int ManagedExceptionExitCode = -532462766;

    /// <summary>
    /// The exit code the child uses when it survives its sleep, i.e. the control event never took effect.
    /// Must match ConsoleCtrlHandlerHelper.StillAliveExitCode in Samples.Console.
    /// </summary>
    private const int StillAliveExitCode = 42;

    public ConsoleControlHandlerTests(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    // Note: there is deliberately no scenario where the application subscribes to CancelKeyPress and *stays*
    // subscribed. That crashes identically with no Datadog code present at all, because ControlCHooker is armed by
    // the application's own subscription. It is a BCL bug we cannot fix from outside mscorlib, so a test for it
    // would never go green.

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("console-ctrl-reset")]
    [InlineData("console-ctrl-resubscribe")]
    [InlineData("console-ctrl-control")]
    public async Task DoesNotCrashWhenTheConsoleControlHandlerListIsReset(string scenario)
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        var result = await RunSampleAndWaitForExit(agent, scenario);

        // RunSampleAndWaitForExit already fails the test on a non-zero exit code, but be explicit about the
        // regression being pinned: before the fix these scenarios exited with 0xE0434352 from
        // System.Console+ControlCHooker.Finalize().
        result.ExitCode.Should().Be(0).And.NotBe(ManagedExceptionExitCode);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task FlushesTracesOnCtrlBreak()
    {
        // The sample splits itself in two for this: the process we start here is only a harness, which takes a
        // private console (so the control event cannot reach this test host) and starts a child that inherits it.
        // The child is the subject - it never touches the console, so the registration the tracer made
        // before its Main ran is still valid when the event arrives. See Samples.Console's RunCtrlBreakParent.
        using var agent = EnvironmentHelper.GetMockAgent();

        var markerFile = Path.Combine(Path.GetTempPath(), $"dd-ctrl-break-{Guid.NewGuid():N}.marker");
        var readyFile = markerFile + ".ready";
        var exitCodeFile = markerFile + ".exitcode";
        SetEnvironmentVariable("DD_INTERNAL_TEST_SHUTDOWN_MARKER_FILE", markerFile);

        try
        {
            var result = await RunSampleAndWaitForExit(agent, "console-ctrl-break-flush");
            Output.WriteLine($"Harness exit code: 0x{result.ExitCode:X8}");

            // The child is killed by the OS default handler once our handler returns "not handled", so we don't pin
            // the exact kill code, but it must not be the ControlCHooker crash, and it must not be the child
            // exiting normally.
            var childExitCode = int.Parse(File.ReadAllText(exitCodeFile), NumberStyles.Integer);
            childExitCode.Should().NotBe(ManagedExceptionExitCode);
            childExitCode.Should().NotBe(StillAliveExitCode, "the child should have been killed by the control event, not have exited on its own");

            // The marker file is written by a tracer shutdown task, so it only exists if our console control
            // handler ran RunShutdownTasks(). Nothing else runs managed shutdown on a Ctrl+Break.
            File.Exists(markerFile).Should().BeTrue("the tracer's shutdown hooks should have run");
            File.ReadAllText(markerFile).Should().Be("shutdown");

            // And the span the child created before the event should have been flushed by those hooks.
            var spans = await agent.WaitForSpansAsync(1, operationName: "console-ctrl");
            spans.Should().NotBeEmpty();
        }
        finally
        {
            foreach (var file in new[] { markerFile, readyFile, exitCodeFile })
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // We don't care, just cleaning up
                }
            }
        }
    }
}

#endif
