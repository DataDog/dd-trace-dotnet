// <copyright file="CircularChannelSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Ipc;

internal class CircularChannelSettings
{
    public int BufferSize { get; set; } = ushort.MaxValue;

    public int PollingInterval { get; set; } = 100;

    public int MutexTimeout { get; set; } = 5000;
}
