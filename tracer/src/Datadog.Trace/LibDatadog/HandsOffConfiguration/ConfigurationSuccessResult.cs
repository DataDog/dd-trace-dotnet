// <copyright file="ConfigurationSuccessResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal readonly struct ConfigurationSuccessResult(Dictionary<string, string> configEntriesLocal, Dictionary<string, string> configEntriesFleet)
{
    public Dictionary<string, string> ConfigEntriesLocal { get; } = configEntriesLocal;

    public Dictionary<string, string> ConfigEntriesFleet { get; } = configEntriesFleet;
}
