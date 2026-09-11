// <copyright file="ContainerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
    private const int ContainerLogTailLineCount = 20;
    private const int ContainerLogTailMaxCharacters = 4 * 1024;
    private const int MaxContainerStartAttempts = 3;
    private static readonly TimeSpan ContainerStartRetryDelay = TimeSpan.FromSeconds(10);

    private readonly string _fixtureRunId = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, object> _resources = new();
    private readonly List<KeyValuePair<string, object>> _resourcesForDisposal = [];

    public async Task InitializeAsync()
    {
        try
        {
            await InitializeResources(RegisterResource).ConfigureAwait(false);
        }
        catch (Exception initializationException)
        {
            var disposalExceptions = await DisposeResourcesAsync(logContainerTails: true).ConfigureAwait(false);
            if (disposalExceptions is not null)
            {
                disposalExceptions.Insert(0, initializationException);
                throw new AggregateException("Container fixture initialization failed and cleanup also encountered errors.", disposalExceptions);
            }

            throw;
        }
    }

    public async Task DisposeAsync()
    {
        var disposalExceptions = await DisposeResourcesAsync(logContainerTails: false).ConfigureAwait(false);
        if (disposalExceptions is not null)
        {
            throw new AggregateException("One or more container fixture resources failed to dispose.", disposalExceptions);
        }
    }

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
        _resourcesForDisposal.Add(new(key, resource));
    }

    private async Task<List<Exception>?> DisposeResourcesAsync(bool logContainerTails)
    {
        List<Exception>? exceptions = null;

        for (var i = _resourcesForDisposal.Count - 1; i >= 0; i--)
        {
            var resourceName = _resourcesForDisposal[i].Key;
            var resource = _resourcesForDisposal[i].Value;
            var container = resource as IContainer;
            var containerLogs = container is null ? null : await CaptureContainerLogsAsync(resourceName, container).ConfigureAwait(false);

            if (logContainerTails && container is not null && containerLogs is not null)
            {
                LogContainerTails(resourceName, container, containerLogs);
            }

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
            catch (Exception exception)
            {
                exceptions ??= [];
                exceptions.Add(exception);

                if (!logContainerTails && container is not null && containerLogs is not null)
                {
                    LogContainerTails(resourceName, container, containerLogs);
                }
            }
        }

        _resources.Clear();
        _resourcesForDisposal.Clear();
        return exceptions;
    }

    private async Task<ContainerLogs?> CaptureContainerLogsAsync(string resourceName, IContainer container)
    {
        ContainerLogs logs;
        try
        {
            var containerLogs = await container.GetLogsAsync().ConfigureAwait(false);
            logs = new(containerLogs.Stdout, containerLogs.Stderr);
        }
        catch (Exception exception)
        {
            container.Logger.LogWarning(exception, "Unable to retrieve logs for container resource {ResourceName}.", resourceName);
            return null;
        }

        string logDirectory;
        try
        {
            logDirectory = Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "artifacts",
                "build_data",
                "container-logs",
                GetType().Name,
                _fixtureRunId);
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception exception)
        {
            container.Logger.LogWarning(exception, "Unable to create the log artifact directory for container resource {ResourceName}.", resourceName);
            return logs;
        }

        await WriteContainerLogAsync(resourceName, "stdout", logs.Stdout, logDirectory, container).ConfigureAwait(false);
        await WriteContainerLogAsync(resourceName, "stderr", logs.Stderr, logDirectory, container).ConfigureAwait(false);
        return logs;
    }

    private async Task WriteContainerLogAsync(string resourceName, string streamName, string contents, string logDirectory, IContainer container)
    {
        var logPath = Path.Combine(logDirectory, $"{resourceName}.{streamName}.log");
        try
        {
            using var writer = new StreamWriter(logPath, append: false);
            await writer.WriteAsync(contents).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            container.Logger.LogWarning(
                exception,
                "Unable to write the {StreamName} log artifact for container resource {ResourceName} to {LogPath}.",
                streamName,
                resourceName,
                logPath);
        }
    }

    private void LogContainerTails(string resourceName, IContainer container, ContainerLogs logs)
    {
        LogContainerTail(resourceName, "stdout", logs.Stdout, container);
        LogContainerTail(resourceName, "stderr", logs.Stderr, container);
    }

    private void LogContainerTail(string resourceName, string streamName, string contents, IContainer container)
    {
        if (contents.Length == 0)
        {
            return;
        }

        container.Logger.LogError(
            "Container resource {ResourceName} {StreamName} tail:{NewLine}{LogTail}",
            resourceName,
            streamName,
            Environment.NewLine,
            GetLogTail(contents));
    }

    private string GetLogTail(string logs)
    {
        var startIndex = Math.Max(0, logs.Length - ContainerLogTailMaxCharacters);
        var lineBreakCount = 0;

        for (var i = logs.Length - 1; i >= startIndex; i--)
        {
            if (logs[i] == '\n' && ++lineBreakCount > ContainerLogTailLineCount)
            {
                startIndex = i + 1;
                break;
            }
        }

        return logs.Substring(startIndex).TrimEnd();
    }

    private sealed class ContainerLogs
    {
        public ContainerLogs(string stdout, string stderr)
        {
            Stdout = stdout;
            Stderr = stderr;
        }

        public string Stdout { get; }

        public string Stderr { get; }
    }
}
