// <copyright file="IpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Ci.Ipc;
using Xunit;

namespace Datadog.Trace.Tests.Ci.Ipc;

public class IpcTests
{
    [Fact]
    public void IpcClientTest()
    {
        var name = Guid.NewGuid().ToString("n");
        using var server = new IpcServer(name);
        using var client = new IpcClient(name);

        var finalValue = 0;
        var endManualResetEvent = new ManualResetEventSlim();
        server.MessageReceived += (sender, bytes) =>
        {
            var value = BitConverter.ToInt32(bytes);
            if (value < 100)
            {
                value++;
                var valueBytes = BitConverter.GetBytes(value);
                while (!server.TrySendMessage(valueBytes))
                {
                    Thread.Sleep(100);
                }

                finalValue = value;
            }
            else
            {
                endManualResetEvent.Set();
            }
        };

        client.MessageReceived += (sender, bytes) =>
        {
            var value = BitConverter.ToInt32(bytes);
            if (value < 100)
            {
                value++;
                var valueBytes = BitConverter.GetBytes(value);
                while (!client.TrySendMessage(valueBytes))
                {
                    Thread.Sleep(100);
                }

                finalValue = value;
            }
            else
            {
                endManualResetEvent.Set();
            }
        };

        client.TrySendMessage(BitConverter.GetBytes(0));

        if (!endManualResetEvent.Wait(30_000))
        {
            throw new TimeoutException("Timeout waiting for messages. Value went up to: " + finalValue);
        }
    }
}
