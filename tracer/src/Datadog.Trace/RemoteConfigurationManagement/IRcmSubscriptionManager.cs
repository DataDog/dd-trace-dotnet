// <copyright file="IRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Numerics;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal delegate Task ManagedRcmPoll(
    RcmClientTracer tracer,
    Func<GetRcmRequest, Task<GetRcmResponse?>> send,
    Func<bool>? acceptResponse = null,
    Action<Action>? commitResponse = null);

internal interface IRcmSubscriptionManager
{
    bool HasAnySubscription { get; }

    void SubscribeToChanges(ISubscription subscription);

    void Replace(ISubscription oldSubscription, ISubscription newSubscription);

    void Unsubscribe(ISubscription subscription);

    void SetCapability(BigInteger index, bool available);

    ManagedRcmPoll CreateManagedPoller();
}
