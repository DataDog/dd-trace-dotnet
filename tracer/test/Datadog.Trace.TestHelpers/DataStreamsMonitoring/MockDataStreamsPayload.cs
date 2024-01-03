// <copyright file="MockDataStreamsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsPayload
{
    [Key(nameof(Env))]
    public string Env { get; set; }

    // Service is the service of the application
    [Key(nameof(Service))]
    public string Service { get; set; }

    [Key(nameof(PrimaryTag))]
    public string PrimaryTag { get; set; }

    [Key(nameof(TracerVersion))]
    public string TracerVersion { get; set; }

    [Key(nameof(Lang))]
    public string Lang { get; set; }

    [Key(nameof(Stats))]
    public MockDataStreamsBucket[] Stats { get; set; }
}
