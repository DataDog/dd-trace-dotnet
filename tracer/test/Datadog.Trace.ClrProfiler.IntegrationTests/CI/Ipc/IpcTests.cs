// <copyright file="IpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Ipc;

public class IpcTests : TestingFrameworkEvpTest
{
    public IpcTests(ITestOutputHelper output)
        : base("CIVisibilityIpc", output)
    {
        SetServiceName("civisibility-ipc-tests");
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "IpcIntegrations")]
    public async Task IpcSampleTest()
    {
        using var agent = MockTracerAgent.Create(Output);
        var sessionId = Guid.NewGuid().ToString("n");
        SetEnvironmentVariable("IPC_SESSION_ID", sessionId);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ipcServer = new IpcServer(sessionId);
        ipcServer.SetMessageReceivedCallback(message =>
        {
            Output.WriteLine(@"IpcServer.Message Received: " + message);
            if ((string)message == "ACK: 🥹")
            {
                tcs.TrySetResult(true);
                return;
            }

            ipcServer.TrySendMessage("🥹");
        });

        using var processResult = await RunSampleAndWaitForExit(agent).ConfigureAwait(false);
        var tskDelay = Task.Delay(30_000);
        if (await Task.WhenAny(tcs.Task, tskDelay).ConfigureAwait(false) == tskDelay)
        {
            throw new Exception("Timeout waiting for response");
        }
    }
}
