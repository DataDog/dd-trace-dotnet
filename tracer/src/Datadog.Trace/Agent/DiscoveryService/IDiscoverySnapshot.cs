// <copyright file="IDiscoverySnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent.DiscoveryService;

internal interface IDiscoverySnapshot
{
    AgentConfiguration? CurrentConfiguration { get; }

    // Hold publication through the synchronous effect. Subscribers run outside this guard.
    bool TryRunWithCurrentConfiguration(AgentConfiguration configuration, Func<bool> apply);
}
