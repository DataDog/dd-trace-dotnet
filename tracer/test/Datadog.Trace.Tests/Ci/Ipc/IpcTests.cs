// <copyright file="IpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Ipc;
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

        TestMessage? finalValue = null;
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

                Interlocked.Exchange(ref finalValue, value);
            }

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

                Interlocked.Exchange(ref finalValue, value);
            }

            if (value.ClientValue == maxNumber)
            {
                clientTaskCompletion.TrySetResult(true);
            }
        });

        client.TrySendMessage(new TestMessage(0, 0));

        var delayTask = Task.Delay(30_000);
        var ipcTasks = Task.WhenAll(serverTaskCompletion.Task, clientTaskCompletion.Task);
        if (await Task.WhenAny(ipcTasks, delayTask).ConfigureAwait(false) == delayTask)
        {
            throw new TimeoutException($"Timeout waiting for messages. Values went up to [{finalValue?.ServerValue}, {finalValue?.ClientValue}]");
        }

        finalValue.Should().NotBeNull();
        finalValue!.ServerValue.Should().Be(maxNumber);
        finalValue.ClientValue.Should().Be(maxNumber);
    }

    private class TestMessage(int serverValue, int clientValue)
    {
        public int ServerValue { get; set; } = serverValue;

        public int ClientValue { get; set; } = clientValue;
    }
}
