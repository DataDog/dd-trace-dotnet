// <copyright file="ConsoleDeadLockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

/// <summary>
/// Tests that the tracer does not deadlock on startup when a .NET Framework console app
/// uses ConfigurationManager config builders that make outbound HTTP calls.
///
/// Regression test for APMS-19239: Azure App Configuration config builders make HTTP calls
/// during ConfigurationManager.AppSettings initialization. The tracer reads AppSettings
/// during its own initialization (GlobalConfigurationSource), creating a cross-thread
/// type-initializer deadlock if the HTTP calls are instrumented by CallTarget.
///
/// See: docs/development/for-ai/ConsoleLock.md
/// See: https://github.com/DataDog/dd-trace-dotnet/pull/6147 (IIS fix)
/// </summary>
public class ConsoleDeadLockTests : TestHelper
{
    public ConsoleDeadLockTests(ITestOutputHelper output)
        : base("ConsoleDeadLock", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    public async Task DoesNotDeadlockWithConfigBuilder()
    {
        // Start a local HTTP server that the config builder will call.
        // We need a real HTTP endpoint so the HttpClient goes through actual async I/O
        // (TCP connect, send, receive on IOCP threads). Without this, the HTTP call may
        // fail synchronously and never trigger the cross-thread deadlock.
        var port = TcpPortProvider.GetOpenPort();
        var listenerUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(listenerUrl);
        listener.Start();

        using var cts = new CancellationTokenSource();

        // Handle requests in the background — respond with 200 OK
        var listenerTask = Task.Run(
            () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = listener.GetContext();
                        // Delay the response so HttpClient's async I/O parks the continuation
                        // on an IOCP/ThreadPool thread. Without this delay, the response may
                        // arrive synchronously and never trigger the cross-thread deadlock.
                        Thread.Sleep(500);
                        context.Response.StatusCode = 200;
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // Listener was stopped
                        break;
                    }
                }
            });

        // Tell the sample app's config builder where to send its HTTP request
        EnvironmentHelper.CustomEnvironmentVariables["CONFIGBUILDER_HTTP_URL"] = listenerUrl;

        // Enable debug logging so we can see what the tracer is doing
        EnvironmentHelper.CustomEnvironmentVariables["DD_TRACE_DEBUG"] = "1";

        using var agent = EnvironmentHelper.GetMockAgent();

        // Use a shorter timeout than the default 10 minutes — if the deadlock
        // occurs, the process will hang indefinitely. 30 seconds is plenty of
        // time for a console app that just starts and exits.
        var process = await StartSample(agent, arguments: null, packageVersion: string.Empty, aspNetCorePort: 0);
        using var helper = new ProcessHelper(process);

        var completed = process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds)
                     && helper.Drain((int)TimeSpan.FromSeconds(5).TotalMilliseconds);

        // Stop the listener
        cts.Cancel();
        listener.Stop();

        if (!completed && !process.HasExited)
        {
            process.Kill();
            throw new TimeoutException(
                "The console app did not exit within 30 seconds — likely deadlocked during tracer initialization. "
              + $"Stdout: {helper.StandardOutput} | Stderr: {helper.ErrorOutput}");
        }

        Output.WriteLine($"Exit code: {process.ExitCode}");
        Output.WriteLine($"Stdout: {helper.StandardOutput}");
        Output.WriteLine($"Stderr: {helper.ErrorOutput}");

        process.ExitCode.Should().Be(0, "the app should exit cleanly without deadlock");
        helper.StandardOutput.Should().Contain("Main() reached - no deadlock!");
        helper.StandardOutput.Should().Contain("Program completed successfully.");
    }
}

#endif
