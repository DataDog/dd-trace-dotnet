// <copyright file="TestScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Headers.Ip;

public class TestScenario : IXunitSerializable
{
    // Parameterless constructor required by IXunitSerializable
    public TestScenario()
    {
    }

    public TestScenario(SerializableDictionary data, string name, string customIpHeader, string expectedIp, int? expectedPort, string peerIp, int peerPort)
    {
        Data = data;
        Name = name;
        CustomIpHeader = customIpHeader;
        ExpectedIp = expectedIp;
        ExpectedPort = expectedPort;
        PeerIp = peerIp;
        PeerPort = peerPort;
    }

    internal SerializableDictionary Data { get; set; }

    internal string Name { get; set; }

    internal string CustomIpHeader { get; private set; }

    internal string ExpectedIp { get; private set; }

    internal int? ExpectedPort { get; private set; }

    internal string PeerIp { get; private set; }

    internal int PeerPort { get; private set; }

    public void Deserialize(IXunitSerializationInfo info)
    {
        Data = info.GetValue<SerializableDictionary>(nameof(Data));
        Name = info.GetValue<string>(nameof(Name));
        CustomIpHeader = info.GetValue<string>(nameof(CustomIpHeader));
        ExpectedIp = info.GetValue<string>(nameof(ExpectedIp));
        ExpectedPort = info.GetValue<int?>(nameof(ExpectedPort));
        PeerIp = info.GetValue<string>(nameof(PeerIp));
        PeerPort = info.GetValue<int>(nameof(PeerPort));
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Data), Data);
        info.AddValue(nameof(Name), Name);
        info.AddValue(nameof(CustomIpHeader), CustomIpHeader);
        info.AddValue(nameof(ExpectedIp), ExpectedIp);
        info.AddValue(nameof(ExpectedPort), ExpectedPort);
        info.AddValue(nameof(PeerIp), PeerIp);
        info.AddValue(nameof(PeerPort), PeerPort);
    }

    public override string ToString()
    {
        return Name;
    }
}
