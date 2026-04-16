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
/// </summary>
public class ConsoleDeadLockTests : TestHelper
{
    public ConsoleDeadLockTests(ITestOutputHelper output)
        : base("Console", output)
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
        _ = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = listener.GetContext();
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
        EnvironmentHelper.CustomEnvironmentVariables["DD_TRACE_DEBUG"] = "1";
        EnvironmentHelper.CustomEnvironmentVariables["HTTP_REQUEST_IN_CONFIG_BUILDER"] = "1";

        // Start a local HTTP server that the config builder will call.
        // We need a real HTTP endpoint so the HttpClient goes through actual async I/O
        // (TCP connect, send, receive on IOCP threads). Without this, the HTTP call may
        // fail synchronously and never trigger the cross-thread deadlock.
        using var agent = EnvironmentHelper.GetMockAgent();

        // Use a shorter timeout than the default 10 minutes — if the deadlock
        // occurs, the process will hang indefinitely. 30 seconds is plenty of
        // time for a console app that just starts and exits.
        using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1", aspNetCorePort: 0, timeout: TimeSpan.FromSeconds(30));
        processResult.StandardOutput.Should().Contain("Sending 1 spans");

        // Stop the listener
        cts.Cancel();
        listener.Stop();

        throw new Exception();
    }
}

#endif
