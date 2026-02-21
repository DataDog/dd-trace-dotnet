// <copyright file="IStatsdManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd;

internal interface IStatsdManager : IDisposable
{
    Task DisposeAsync();

    /// <summary>
    /// Obtain a <see cref="StatsdManager.StatsdClientLease"/> for accessing a <see cref="IDogStatsd"/> instance.
    /// The lease must be disposed after all references to the client have gone.
    /// </summary>
    StatsdManager.StatsdClientLease TryGetClientLease();

    /// <summary>
    /// Called by users of <see cref="StatsdManager"/> to indicate that a "live" client is required.
    /// Each unique consumer of <see cref="StatsdManager"/> should set a different
    /// <see cref="StatsdConsumer"/> value.
    /// </summary>
    void SetRequired(StatsdConsumer consumer, bool enabled);
}
