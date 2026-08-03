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
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public abstract class ContainerFixture : IAsyncLifetime
{
    private const int MaxContainerStartAttempts = 3;
    private static readonly TimeSpan ContainerStartRetryDelay = TimeSpan.FromSeconds(10);

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

    protected static async Task StartContainerAsync(IContainer container)
    {
        var attempt = 1;
        while (true)
        {
            try
            {
                await container.StartAsync().ConfigureAwait(false);
                return;
            }
            catch (DockerApiException exception) when (attempt < MaxContainerStartAttempts && IsTransientSystemdCgroupFailure(exception))
            {
                container.Logger.LogWarning(
                    "Docker failed to start container {ContainerId} because its systemd cgroup request was interrupted. Retrying in {RetryDelaySeconds} seconds (attempt {NextAttempt}/{MaxAttempts}).",
                    container.Id,
                    ContainerStartRetryDelay.TotalSeconds,
                    attempt + 1,
                    MaxContainerStartAttempts);

                attempt++;
                await Task.Delay(ContainerStartRetryDelay).ConfigureAwait(false);
            }
        }
    }

    protected abstract Task InitializeResources(Action<string, object> registerResource);

    protected T GetResource<T>(string key) => (T)_resources[key];

    private static bool IsTransientSystemdCgroupFailure(DockerApiException exception)
    {
        var responseBody = exception.ResponseBody;
        return exception.StatusCode == HttpStatusCode.InternalServerError
            && responseBody is not null
            && responseBody.IndexOf("unable to apply cgroup configuration", StringComparison.Ordinal) >= 0
            && responseBody.IndexOf("Message recipient disconnected from message bus without replying", StringComparison.Ordinal) >= 0;
    }

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
