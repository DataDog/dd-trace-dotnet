// <copyright file="ContainerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Datadog.Trace.TestHelpers.Containers;

public abstract class ContainerFixture : IAsyncLifetime
{
    public abstract string Name { get; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // Technically, the property CAN be null. But in practice, InitializeAsync will always be called before the property is accessed.
    public IContainer Container { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public async Task InitializeAsync()
    {
        Container = await ContainersRegistry.GetOrAdd(Name, InitializeContainer);
    }

    // Do not implement, the ContainesRegistry is responsible for disposing the containers
    public Task DisposeAsync() => Task.CompletedTask;

    protected abstract Task<IContainer> InitializeContainer(ContainerBuilder builder);
}
