// <copyright file="Signed.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;

internal class Signed
{
    public Dictionary<string, Target> Targets { get; set; } = new();

    public int Version { get; set; }

    public TargetsCustom Custom { get; set; }
}
