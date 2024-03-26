// <copyright file="NodeHashBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.DataStreamsMonitoring.Hashes;

internal readonly struct NodeHashBase
{
    public readonly ulong Value;

    public NodeHashBase(ulong value)
    {
        Value = value;
    }
}
