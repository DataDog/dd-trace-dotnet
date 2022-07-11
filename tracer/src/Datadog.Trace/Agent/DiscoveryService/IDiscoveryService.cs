// <copyright file="IDiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Agent.DiscoveryService
{
    /// <summary>
    /// Queries datadog-agent and discovers which version we are running against and what endpoints it supports.
    /// </summary>
    internal interface IDiscoveryService
    {
        string ConfigurationEndpoint { get; }

        string DebuggerEndpoint { get; }

        string AgentVersion { get; }

        Task<bool> DiscoverAsync();
    }
}
