// <copyright file="ProduceDeliveryCallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

internal struct ProduceDeliveryCallbacks : IBegin1Callbacks, IVoidReturnCallback
{
    public void OnException(object? sender, Exception ex)
    {
    }

    public void OnDelegateEnd(object? sender, Exception? exception, object? state)
    {
    }

    public object? OnDelegateBegin<TDeliveryReport>(object? sender, ref TDeliveryReport deliveryReport)
    {
        // no need to add logic here, we just need a dummy handler
        return null;
    }
}
