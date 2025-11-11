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
    private Dictionary<string, object>? _resources;

    public async Task InitializeAsync()
    {
        _resources = new Dictionary<string, object>();
        await InitializeResources(_resources.Add).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_resources == null)
        {
            return;
        }

        foreach (var resourceKvp in _resources)
        {
            var resource = resourceKvp.Value;

            try
            {
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
                // Continue disposing other resources even if one fails
            }
        }

        _resources.Clear();
    }

    public virtual IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables() => Enumerable.Empty<KeyValuePair<string, string>>();

    protected abstract Task InitializeResources(Action<string, object> registerResource);

    protected T GetResource<T>(string key) => (T)_resources![key];
}
