// <copyright file="IpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task IpcClientCanSendCoverageResultReferenceMessage()
    {
        var name = nameof(IpcClientCanSendCoverageResultReferenceMessage) + "-" + Guid.NewGuid().ToString("n");
        using var server = new IpcServer(name);
        using var client = new IpcClient(name);
        var receivedMessage = new TaskCompletionSource<SessionCodeCoverageReferenceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.SetMessageReceivedCallback(
            message =>
            {
                if (message is SessionCodeCoverageReferenceMessage referenceMessage)
                {
                    receivedMessage.TrySetResult(referenceMessage);
                }
            });

        client.TrySendMessage(
            new SessionCodeCoverageReferenceMessage(
                CodeCoverageReportSource.Coverlet,
                "result-123")).Should().BeTrue();

        var completedTask = await Task.WhenAny(receivedMessage.Task, Task.Delay(30_000));
        completedTask.Should().Be(receivedMessage.Task);
        var receivedReferenceMessage = await receivedMessage.Task;
        receivedReferenceMessage.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        receivedReferenceMessage.ResultId.Should().Be("result-123");
    }

    [Fact]
    public async Task IpcClientCanSendCoverageResultReferenceWithOversizedPersistedBackfillValidation()
    {
        var name = nameof(IpcClientCanSendCoverageResultReferenceWithOversizedPersistedBackfillValidation) + "-" + Guid.NewGuid().ToString("n");
        using var server = new IpcServer(name);
        using var client = new IpcClient(name);
        var receivedMessage = new TaskCompletionSource<SessionCodeCoverageReferenceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var largeBackfillValidation = CreateLargeBackfillValidation(fileCount: 200);

        server.SetMessageReceivedCallback(
            message =>
            {
                if (message is SessionCodeCoverageReferenceMessage referenceMessage)
                {
                    receivedMessage.TrySetResult(referenceMessage);
                }
            });

        var inlineMessageWasTooLarge = client.TrySendMessage(
            new SessionCodeCoverageMessage(
                CodeCoverageReportSource.Coverlet,
                value: 100,
                backfilled: true,
                executableLines: 100,
                coveredLines: 100,
                resultId: "large-result",
                backfillValidation: largeBackfillValidation));
        inlineMessageWasTooLarge.Should().BeFalse("this reproduces the old oversized IPC payload shape");

        client.TrySendMessage(
            new SessionCodeCoverageReferenceMessage(
                CodeCoverageReportSource.Coverlet,
                "large-result")).Should().BeTrue();

        var completedTask = await Task.WhenAny(receivedMessage.Task, Task.Delay(30_000));
        completedTask.Should().Be(receivedMessage.Task);
        var receivedReferenceMessage = await receivedMessage.Task;
        receivedReferenceMessage.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        receivedReferenceMessage.ResultId.Should().Be("large-result");
    }

    private static CodeCoverageBackfillValidation CreateLargeBackfillValidation(int fileCount)
    {
        var expectedLines = new Dictionary<string, int>(StringComparer.Ordinal);
        var representedLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var localCandidates = new Dictionary<string, string>(StringComparer.Ordinal);
        var requiredPaths = new List<string>();
        var requiredLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        for (var i = 0; i < fileCount; i++)
        {
            var backendPath = $"src/components/component-{i:D4}/subsystem/VeryLongFileNameForCoverageBackfillValidation{i:D4}.cs";
            expectedLines[backendPath] = 25;
            representedLines[backendPath] = Enumerable.Range(1, 25).ToHashSet();
            localCandidates[backendPath] = $"/workspace/customer/repository/{backendPath}";
            requiredPaths.Add(backendPath);
            requiredLines[backendPath] = Enumerable.Range(1, 25).ToHashSet();
        }

        return CodeCoverageBackfillValidation.Create(
            fileCount,
            expectedLines,
            representedLines,
            localCandidates,
            requiredPaths,
            requiredLines);
    }

    private class TestMessage(int serverValue, int clientValue)
    {
        public int ServerValue { get; set; } = serverValue;

        public int ClientValue { get; set; } = clientValue;
    }
}
