// <copyright file="IRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Numerics;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal interface IRcmSubscriptionManager
{
    bool HasAnySubscription { get; }

    /// <summary>
    /// Gets the id identifying this client to the agent.
    /// </summary>
    string ClientId { get; }

    void SubscribeToChanges(ISubscription subscription);

    void Replace(ISubscription oldSubscription, ISubscription newSubscription);

    void Unsubscribe(ISubscription subscription);

    void SetCapability(BigInteger index, bool available);

    /// <summary>
    /// Fetches the remote configurations once with <paramref name="client"/>, and dispatches the changes to the subscriptions.
    /// </summary>
    void Poll(IRemoteConfigClient client);
}
