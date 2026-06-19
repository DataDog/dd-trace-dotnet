// <copyright file="IpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci.Ipc;

public class IpcTests
{
    [Fact]
    public async Task IpcClientTest()
    {
        var name = nameof(IpcClientTest) + "-" + Guid.NewGuid().ToString("n");
        using var server = new IpcServer(name);
        using var client = new IpcClient(name);

        var finalServerValue = 0;
        var finalClientValue = 0;
        var serverTaskCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientTaskCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        const int maxNumber = 20;

        server.SetMessageReceivedCallback(message =>
        {
            var value = (TestMessage)message;
            if (value.ServerValue < maxNumber)
            {
                value.ServerValue++;
                while (!server.TrySendMessage(value))
                {
                    Thread.Sleep(500);
                }
            }

            Interlocked.Exchange(ref finalServerValue, value.ServerValue);
            if (value.ServerValue == maxNumber)
            {
                serverTaskCompletion.TrySetResult(true);
            }
        });

        client.SetMessageReceivedCallback(message =>
        {
            var value = (TestMessage)message;
            if (value.ClientValue < maxNumber)
            {
                value.ClientValue++;
                while (!client.TrySendMessage(value))
                {
                    Thread.Sleep(500);
                }
            }

            Interlocked.Exchange(ref finalClientValue, value.ClientValue);
            if (value.ClientValue == maxNumber)
            {
                clientTaskCompletion.TrySetResult(true);
            }
        });

        client.TrySendMessage(new TestMessage(0, 0));

        var delayTask = Task.Delay(30_000);
        var ipcTasks = Task.WhenAll(serverTaskCompletion.Task, clientTaskCompletion.Task);
        if (await Task.WhenAny(ipcTasks, delayTask) == delayTask)
        {
            throw new TimeoutException($"Timeout waiting for messages. Values went up to [{finalServerValue}, {finalClientValue}]");
        }

        finalServerValue.Should().Be(maxNumber);
        finalClientValue.Should().Be(maxNumber);
    }

    [Fact]
    public async Task IpcClientCanSendCoverageMessageWithBackfillValidation()
    {
        var name = nameof(IpcClientCanSendCoverageMessageWithBackfillValidation) + "-" + Guid.NewGuid().ToString("n");
        using var server = new IpcServer(name);
        using var client = new IpcClient(name);
        var receivedMessage = new TaskCompletionSource<SessionCodeCoverageMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backfillValidation = CodeCoverageBackfillValidation.Create(
            requiredBackendFilesWithCoverage: 1,
            expectedCoveredLinesByBackendPath: new Dictionary<string, int> { ["src/Calculator.cs"] = 1 },
            representedBackendLinesByBackendPath: new Dictionary<string, HashSet<int>> { ["src/Calculator.cs"] = [42] },
            localCandidateByBackendPath: new Dictionary<string, string> { ["src/Calculator.cs"] = "/repo/src/Calculator.cs" },
            requiredBackendPathsWithCoverage: ["src/Calculator.cs"],
            requiredBackendLinesByBackendPath: new Dictionary<string, HashSet<int>> { ["src/Calculator.cs"] = [42] });

        server.SetMessageReceivedCallback(
            message =>
            {
                if (message is SessionCodeCoverageMessage coverageMessage)
                {
                    receivedMessage.TrySetResult(coverageMessage);
                }
            });

        client.TrySendMessage(
            new SessionCodeCoverageMessage(
                CodeCoverageReportSource.Coverlet,
                value: 100,
                backfilled: true,
                executableLines: 1,
                coveredLines: 1,
                resultId: "merged",
                backfillValidation: backfillValidation,
                supersededResultIds: ["partial-a", "partial-b"])).Should().BeTrue();

        var completedTask = await Task.WhenAny(receivedMessage.Task, Task.Delay(30_000));
        completedTask.Should().Be(receivedMessage.Task);
        var receivedCoverageMessage = await receivedMessage.Task;
        receivedCoverageMessage.BackfillValidation.Should().NotBeNull();
        receivedCoverageMessage.BackfillValidation!.CanPublish().Should().BeTrue();
        receivedCoverageMessage.BackfillValidation.LocalCandidateByBackendPath.Should().Contain("src/Calculator.cs", "/repo/src/Calculator.cs");
        receivedCoverageMessage.SupersededResultIds.Should().Equal("partial-a", "partial-b");
    }

    private class TestMessage(int serverValue, int clientValue)
    {
        public int ServerValue { get; set; } = serverValue;

        public int ClientValue { get; set; } = clientValue;
    }
}
