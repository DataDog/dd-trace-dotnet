// <copyright file="ContainerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Docker.DotNet;
using Xunit;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public abstract class ContainerFixture : IAsyncLifetime
{
    private IReadOnlyDictionary<string, object>? _resources;
    private string? _initializationSkipReason;

    public async Task InitializeAsync()
    {
        try
        {
            _resources = await ContainersRegistry.GetOrAdd(GetType(), InitializeResources);
        }
        catch (DockerApiException ex) when (IsTransientPlatformError(ex))
        {
            _initializationSkipReason = $"Docker failed to start the test container because of a transient platform error: {ex.ResponseBody}";
        }
    }

    // Do not implement, the ContainersRegistry is responsible for disposing the containers
    public Task DisposeAsync() => Task.CompletedTask;

    public virtual IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables() => Enumerable.Empty<KeyValuePair<string, string>>();

    public void SkipIfUnavailable()
    {
        if (_initializationSkipReason is not null)
        {
            throw new SkipException(_initializationSkipReason);
        }
    }

    protected abstract Task InitializeResources(Action<string, object> registerResource);

    protected T GetResource<T>(string key)
    {
        SkipIfUnavailable();

        return (T)_resources![key];
    }

    private static bool IsTransientPlatformError(DockerApiException exception)
        => exception.StatusCode == HttpStatusCode.InternalServerError
        && exception.ResponseBody?.Contains("unable to apply cgroup configuration") == true
        && exception.ResponseBody.Contains("Message recipient disconnected from message bus without replying");

    private async Task<IReadOnlyDictionary<string, object>> InitializeResources()
    {
        var resources = new Dictionary<string, object>();

        await InitializeResources(resources.Add).ConfigureAwait(false);

        return resources;
    }
}
