// <copyright file="TerminationSignalTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#if NET10_0_OR_GREATER

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LifetimeManager;

public class TerminationSignalTests : TestHelper
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public TerminationSignalTests(ITestOutputHelper output)
        : base(new EnvironmentHelper("LifetimeManager.TerminationSignals", typeof(TestHelper), output), output)
    {
    }

    [SkippableFact]
    public async Task SigtermTriggersShutdownOnce_WhenRepeated()
    {
        await RunSigtermTestAsync(signalCount: 2, usePublishWithRid: false);
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dd-lifetime-manager", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void AssertSingleShutdownLine(string shutdownFile)
    {
        File.Exists(shutdownFile).Should().BeTrue();
        var lines = File.ReadAllLines(shutdownFile);
        lines.Should().HaveCount(1);
        lines[0].Should().StartWith("shutdown|");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private async Task RunSigtermTestAsync(int signalCount, bool usePublishWithRid)
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        using var agent = EnvironmentHelper.GetMockAgent();
        var tempDir = CreateTempDirectory();
        var readyFile = Path.Combine(tempDir, "ready.txt");
        var shutdownFile = Path.Combine(tempDir, "shutdown.txt");

        SetEnvironmentVariable("DD_LIFETIME_READY_FILE", readyFile);
        SetEnvironmentVariable("DD_LIFETIME_SHUTDOWN_FILE", shutdownFile);

        using var process = await StartSample(agent, "--wait", packageVersion: string.Empty, aspNetCorePort: 0, usePublishWithRID: usePublishWithRid);
        using var helper = new ProcessHelper(process);

        try
        {
            WaitForReady(process, readyFile);
            SendTerminationSignals(process, signalCount);
            WaitForExit(process, helper);

            process.ExitCode.Should().Be(143);
            AssertSingleShutdownLine(shutdownFile);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private void WaitForReady(Process process, string readyFile)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < ReadyTimeout)
        {
            if (File.Exists(readyFile))
            {
                return;
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException("Sample exited before creating the ready file.");
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException($"Ready file was not created within {ReadyTimeout.TotalSeconds} seconds.");
    }

    private void SendTerminationSignals(Process process, int signalCount)
    {
        for (var i = 0; i < signalCount; i++)
        {
            if (process.HasExited)
            {
                return;
            }

            UnixSignalHelper.SendSigTerm(process.Id);
            Thread.Sleep(PollInterval);
        }
    }

    private void WaitForExit(Process process, ProcessHelper helper)
    {
        if (!process.WaitForExit((int)ExitTimeout.TotalMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"Process did not exit within {ExitTimeout.TotalSeconds} seconds.");
        }

        helper.Drain((int)ExitTimeout.TotalMilliseconds);

        if (!string.IsNullOrWhiteSpace(helper.StandardOutput))
        {
            Output.WriteLine($"StandardOutput:{Environment.NewLine}{helper.StandardOutput}");
        }

        if (!string.IsNullOrWhiteSpace(helper.ErrorOutput))
        {
            Output.WriteLine($"StandardError:{Environment.NewLine}{helper.ErrorOutput}");
        }
    }
}

#endif
