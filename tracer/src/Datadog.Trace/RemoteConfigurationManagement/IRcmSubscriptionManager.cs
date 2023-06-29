// <copyright file="IRcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal interface IRcmSubscriptionManager
{
    bool HasAnySubscription { get; }

    ICollection<string> ProductKeys { get; }

    void SubscribeToChanges(ISubscription subscription);

    void Replace(ISubscription oldSubscription, ISubscription newSubscription);

    void Unsubscribe(ISubscription subscription);

    List<ApplyDetails> Update(Dictionary<string, List<RemoteConfiguration>> configByProducts, Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct);

    void SetCapability(BigInteger index, bool available);

    byte[] GetCapabilities();

    GetRcmRequest BuildRequest(RcmClientTracer rcmTracer, string? lastPollError);

    void ProcessResponse(GetRcmResponse response);
}
