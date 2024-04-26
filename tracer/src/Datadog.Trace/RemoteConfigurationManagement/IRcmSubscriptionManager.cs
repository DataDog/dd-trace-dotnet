// <copyright file="IRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal interface IRcmSubscriptionManager
{
    bool HasAnySubscription { get; }

    ICollection<string> ProductKeys { get; }

    void SubscribeToChanges(ISubscription subscription);

    void Replace(ISubscription oldSubscription, ISubscription newSubscription);

    void Unsubscribe(ISubscription subscription);

    void SetCapability(BigInteger index, bool available);

    byte[] GetCapabilities();

    Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback);
}
