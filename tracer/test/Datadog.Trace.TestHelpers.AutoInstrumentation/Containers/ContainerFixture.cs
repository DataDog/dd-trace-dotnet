// <copyright file="ContainerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public abstract class ContainerFixture : IAsyncLifetime
{
    private IReadOnlyDictionary<string, object>? _resources;

    public async Task InitializeAsync()
    {
        _resources = await ContainersRegistry.GetOrAdd(GetType(), InitializeResources);
    }

    // Do not implement, the ContainersRegistry is responsible for disposing the containers
    public Task DisposeAsync() => Task.CompletedTask;

    public virtual IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables() => Enumerable.Empty<KeyValuePair<string, string>>();

    protected abstract Task InitializeResources(Action<string, object> registerResource);

    protected T GetResource<T>(string key) => (T)_resources![key];

    private async Task<IReadOnlyDictionary<string, object>> InitializeResources()
    {
        var resources = new Dictionary<string, object>();

        await InitializeResources(resources.Add).ConfigureAwait(false);

        return resources;
    }
}
