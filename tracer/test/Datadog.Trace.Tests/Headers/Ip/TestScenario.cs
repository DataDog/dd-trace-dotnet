// <copyright file="TestScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.Tests.Headers.Ip;

public class TestScenario(SerializableDictionary data, string name, string customIpHeader, string expectedIp, int? expectedPort, string peerIp, int peerPort)
{
    internal SerializableDictionary Data { get; set; } = data;

    internal string Name { get; set; } = name;

    internal string CustomIpHeader { get; } = customIpHeader;

    internal string ExpectedIp { get; } = expectedIp;

    internal int? ExpectedPort { get; } = expectedPort;

    internal string PeerIp { get; } = peerIp;

    internal int PeerPort { get; } = peerPort;

    public override string ToString()
    {
        return Name;
    }
}
