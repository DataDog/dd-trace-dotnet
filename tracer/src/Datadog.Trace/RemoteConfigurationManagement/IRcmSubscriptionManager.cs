// <copyright file="IRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Numerics;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal interface IRcmSubscriptionManager
{
    bool HasAnySubscription { get; }

    void SubscribeToChanges(ISubscription subscription);

    void Replace(ISubscription oldSubscription, ISubscription newSubscription);

    void Unsubscribe(ISubscription subscription);

    void SetCapability(BigInteger index, bool available);

    /// <summary>
    /// Fetches the remote configurations for <paramref name="tracer"/> once, and dispatches the changes to the subscriptions.
    /// All callers share one client, created with <paramref name="clientFactory"/> by the first poll, so that polling for
    /// another target (e.g. by the remote configuration manager of a tracer replaced by Tracer.Configure) only reports the differences.
    /// </summary>
    /// <returns>False if the client could not be created.</returns>
    bool Poll(Func<string, RcmClientTracer, RemoteConfigEndpoint, IRemoteConfigClient> clientFactory, RcmClientTracer tracer, RemoteConfigEndpoint endpoint, string[]? extraServices);

    /// <summary>
    /// Cancels the pending and all further requests of the client, on shutdown.
    /// </summary>
    void Cancel();
}
