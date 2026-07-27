// <copyright file="MockRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Simple mock implementation of <see cref="IRcmSubscriptionManager"/> for unit tests.
/// </summary>
internal class MockRcmSubscriptionManager : IRcmSubscriptionManager
{
    public bool HasAnySubscription => LastSubscription != null;

    public ICollection<string> ProductKeys { get; } = new List<string>();

    public ISubscription? LastSubscription { get; private set; }

    public void SubscribeToChanges(ISubscription subscription)
    {
        LastSubscription = subscription;
        foreach (var productKey in subscription.ProductKeys)
        {
            ProductKeys.Add(productKey);
        }
    }

    public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
    {
        LastSubscription = newSubscription;
    }

    public void Unsubscribe(ISubscription subscription)
    {
        if (LastSubscription == subscription)
        {
            LastSubscription = null;
        }
    }

    public void SetCapability(BigInteger index, bool available)
    {
    }

    public byte[] GetCapabilities() => [];

    public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback)
        => Task.CompletedTask;
}
