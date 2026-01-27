// <copyright file="IDiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.DiscoveryService
{
    /// <summary>
    /// Queries the Datadog Agent and discovers which version we are running against and which endpoints it supports.
    /// </summary>
    internal interface IDiscoveryService
    {
        /// <summary>
        /// Subscribe to changes in the agent configuration. The callback should exit quickly when called.
        /// If an <see cref="AgentConfiguration"/> has already been fetched when <see cref="SubscribeToChanges"/>
        /// is called, <paramref name="callback"/> is called immediately with the latest configuration
        /// </summary>
        void SubscribeToChanges(Action<AgentConfiguration> callback);

        /// <summary>
        /// Remove a subscription for agent configuration changes. This callback should be the same
        /// object passed to <see cref="SubscribeToChanges"/>
        /// </summary>
        void RemoveSubscription(Action<AgentConfiguration> callback);

        /// <summary>
        /// Report the current config state hash, as returned in a header from the agent.
        /// </summary>
        /// <param name="configStateHash">The sha256 hash of the config state, as returned by the agent</param>
        void SetCurrentConfigStateHash(string configStateHash);

        Task DisposeAsync();
    }
}
