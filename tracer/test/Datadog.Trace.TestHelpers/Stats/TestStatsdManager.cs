// <copyright file="TestStatsdManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers.Stats;

/// <summary>
/// An instance of <see cref="IStatsdManager"/> that returns the provided <see cref="IDogStatsd"/> instance
/// </summary>
internal class TestStatsdManager(IDogStatsd client) : IStatsdManager
{
    public static TestStatsdManager NoOp => new(NoOpStatsd.Instance);

    public void Dispose() => client.DisposeAsync().GetAwaiter().GetResult();

    public Task DisposeAsync() => client.DisposeAsync();

    public StatsdManager.StatsdClientLease TryGetClientLease()
        => new(new StatsdManager.StatsdClientHolder(client));

    public void SetRequired(StatsdConsumer consumer, bool enabled)
    {
    }
}
