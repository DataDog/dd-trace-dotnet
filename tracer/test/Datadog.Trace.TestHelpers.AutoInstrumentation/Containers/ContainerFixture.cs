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
    private readonly Dictionary<string, object> _resources = new();
    private readonly List<object> _resourcesForDisposal = [];

    public async Task InitializeAsync()
    {
        try
        {
            await InitializeResources(RegisterResource).ConfigureAwait(false);
        }
        catch
        {
            await DisposeResourcesAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task DisposeAsync() => DisposeResourcesAsync();

    public virtual IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables() => Enumerable.Empty<KeyValuePair<string, string>>();

    protected abstract Task InitializeResources(Action<string, object> registerResource);

    protected T GetResource<T>(string key) => (T)_resources[key];

    private void RegisterResource(string key, object resource)
    {
        _resources.Add(key, resource);
        _resourcesForDisposal.Add(resource);
    }

    private async Task DisposeResourcesAsync()
    {
        for (var i = _resourcesForDisposal.Count - 1; i >= 0; i--)
        {
            try
            {
                var resource = _resourcesForDisposal[i];
                if (resource is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (resource is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Continue disposing the remaining resources.
            }
        }

        _resources.Clear();
        _resourcesForDisposal.Clear();
    }
}
