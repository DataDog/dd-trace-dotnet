// <copyright file="IpcServer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Ci.Ipc;

internal class IpcServer : IpcDualChannel
{
    public IpcServer(string name)
        : base($"{name}.recv", $"{name}.send")
    {
    }

    public event EventHandler<byte[]>? MessageReceived;

    protected override void OnMessageReceived(object? sender, byte[] e)
    {
        MessageReceived?.Invoke(this, e);
    }
}
