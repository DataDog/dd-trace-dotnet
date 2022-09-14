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
    /// Queries datadog-agent and discovers which version we are running against and what endpoints it supports.
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

        Task DisposeAsync();
    }
}
