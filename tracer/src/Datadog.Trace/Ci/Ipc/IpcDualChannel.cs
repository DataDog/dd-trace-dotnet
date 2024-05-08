// <copyright file="IpcDualChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Ci.Ipc;

internal abstract class IpcDualChannel : IDisposable
{
    private readonly IChannel _recvChannel;
    private readonly IChannelReceiver _recvChannelReceiver;
    private readonly IChannel _sendChannel;
    private readonly IChannelWriter _sendChannelWriter;

    protected IpcDualChannel(string recvName, string sendName)
    {
        _recvChannel = new CircularChannel(recvName);
        _recvChannelReceiver = _recvChannel.GetReceiver();
        _recvChannelReceiver.MessageReceived += OnMessageReceived;

        _sendChannel = new CircularChannel(sendName);
        _sendChannelWriter = _sendChannel.GetWriter();
    }

    protected abstract void OnMessageReceived(object? sender, byte[] message);

    protected internal bool TrySendMessage(byte[] message)
    {
        return _sendChannelWriter.TryWrite(message);
    }

    public void Dispose()
    {
        _recvChannel.Dispose();
        _sendChannel.Dispose();
    }
}
