// <copyright file="ConfigurationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration;

internal readonly struct ConfigurationResult(IDictionary<string, ConfigurationEntry> configEntriesLocal, IDictionary<string, ConfigurationEntry> configEntriesFleet)
{
    public IDictionary<string, ConfigurationEntry> ConfigEntriesLocal { get; } = configEntriesLocal;

    public IDictionary<string, ConfigurationEntry> ConfigEntriesFleet { get; } = configEntriesFleet;
}
