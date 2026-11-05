// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class CouchbaseFixture : ContainerFixture
{
    private const ushort ManagementPort = 8091;
    private const ushort QueryPort = 8093;
    private const ushort KeyValuePort = 11210;
    private const string Image = "couchbase:community-7.6.2@sha256:b189eeb7bebb141e5122064e6112e8178b459e2d639f37ffe97823d7a31ded81";

    public string Host { get; private set; } = string.Empty;

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("COUCHBASE_HOST", Host);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // SDK 2.4 cannot use alternate addresses, so advertise a reachable hostname and use standard ports.
        // In Compose, Couchbase must share the Docker host network to bind that host's address.
        var useHostNetwork = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                          && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_HOSTNAME"));
        var builder = new ContainerBuilder(Image)
                     // Resolve the host after container creation, but configure it before Couchbase starts.
                     // Renaming a running node leaves its old leader lease active for up to 15 seconds.
                     .WithEntrypoint("/bin/sh", "-c")
                     .WithCommand("while [ ! -f /tmp/couchbase-start ]; do sleep 0.1; done; exec /entrypoint.sh couchbase-server")
                     .WithStartupCallback(async (container, cancellationToken) =>
                     {
                         Host = useHostNetwork ? GetDockerHost() : container.Hostname;
                         await container.CopyAsync(Encoding.UTF8.GetBytes(Host), "/opt/couchbase/var/lib/couchbase/ip_start", uid: 1000, gid: 1000, ct: cancellationToken).ConfigureAwait(false);
                         await container.CopyAsync(Array.Empty<byte>(), "/tmp/couchbase-start", ct: cancellationToken).ConfigureAwait(false);
                     })
                     .WithCreateParameterModifier(p =>
                     {
                         p.HostConfig ??= new HostConfig();
                         // Couchbase requires 200000 file descriptors; Docker can default to 1024.
                         p.HostConfig.Ulimits = [new Ulimit { Name = "nofile", Soft = 200000, Hard = 200000 }];
                         if (useHostNetwork)
                         {
                             p.HostConfig.NetworkMode = "host";
                         }
                     })
                     .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("curl", "--fail", "--silent", "http://localhost:8091/pools"));

        if (!useHostNetwork)
        {
            builder = builder.WithPortBinding(ManagementPort, false)
                             .WithPortBinding(QueryPort, false)
                             .WithPortBinding(KeyValuePort, false);
        }

        var container = builder.Build();
        registerResource("container", container);
        await StartContainerAsync(container).ConfigureAwait(false);

        // Community Edition requires the index service when the query service is enabled.
        await RunCliAsync(
            container,
            "cluster-init",
            ["--cluster-username", "default", "--cluster-password", "password",
             "--services", "data,index,query", "--cluster-ramsize", "256", "--cluster-index-ramsize", "256"]).ConfigureAwait(false);
        await CreateBucketAsync(container).ConfigureAwait(false);
        await RunCliAsync(container, "collection-manage", "--bucket", "default", "--create-scope", "tenant_agent_00").ConfigureAwait(false);
        // Scope creation is asynchronous, so collection creation can race with scope propagation.
        await RunCliAsync(
            container,
            "collection-manage",
            ["--bucket", "default", "--create-collection", "tenant_agent_00.users"],
            retryableError: "Scope with name \"tenant_agent_00\" is not found").ConfigureAwait(false);

        // SDK 3.0 needs the query port advertised before bootstrapping, even after /admin/ping succeeds.
        var readinessChecks = Wait.ForUnixContainer()
                                  .UntilCommandIsCompleted("curl", "--fail", "--silent", "http://localhost:8093/admin/ping")
                                  .UntilCommandIsCompleted("sh", "-c", "curl --fail --silent --user default:password http://localhost:8091/pools/default/nodeServices | grep -q '\"n1ql\":8093'")
                                  .Build();
        foreach (var readinessCheck in readinessChecks)
        {
            await WaitStrategy.WaitUntilAsync(() => readinessCheck.UntilAsync(container), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }
    }

    private static string GetDockerHost()
        => NetworkInterface.GetAllNetworkInterfaces()
                           .SelectMany(networkInterface => networkInterface.GetIPProperties().GatewayAddresses)
                           .Select(gateway => gateway.Address)
                           .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork
                                                   && !IPAddress.IsLoopback(address))?.ToString()
        ?? throw new InvalidOperationException("Unable to determine the Docker host from the container's default gateway.");

    private static async Task CreateBucketAsync(IContainer container)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            // The CLI's --wait has its own 60-second deadline, independent of RunCliAsync's timeout.
            // Create once, then poll the existing bucket: a readiness timeout does not undo creation.
            await RunCliAsync(container, "bucket-create", "--bucket", "default", "--bucket-type", "couchbase", "--bucket-ramsize", "100", "--bucket-replica", "0").ConfigureAwait(false);
            elapsed.Restart();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                await WaitForBucketReadyAsync(container, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("Couchbase bucket 'default' did not become healthy within two minutes of creation.", exception);
            }
        }
        catch (Exception exception)
        {
            var logs = await CaptureServerLogsAsync(container).ConfigureAwait(false);
            throw new InvalidOperationException($"Couchbase bucket initialization failed: {exception.Message}{logs}", exception);
        }

        elapsed.Stop();
        if (elapsed.Elapsed > TimeSpan.FromMinutes(1))
        {
            // Preserve evidence even when the longer deadline allows startup to recover.
            var logs = await CaptureServerLogsAsync(container).ConfigureAwait(false);
            container.Logger.LogWarning("Couchbase bucket became healthy after {Elapsed}. {Logs}", elapsed.Elapsed, logs);
        }
    }

    private static async Task WaitForBucketReadyAsync(IContainer container, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await container.ExecAsync(
                             ["curl", "--fail", "--silent", "--show-error", "--max-time", "5", "--user", "default:password", "http://localhost:8091/pools/default/buckets/default"],
                             cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.ExitCode == 0 && IsBucketHealthy(result.Stdout))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsBucketHealthy(string response)
    {
        try
        {
            // Match couchbase-cli's readiness condition, including rejecting an empty node list.
            return JObject.Parse(response)["nodes"] is JArray { Count: > 0 } nodes
                && nodes.All(node => node is JObject && node["status"] is JValue { Value: "healthy" });
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Task RunCliAsync(IContainer container, string command, params string[] arguments)
        => RunCliAsync(container, command, arguments, retryableError: null);

    private static async Task RunCliAsync(IContainer container, string command, string[] arguments, string? retryableError)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        ExecResult result = default;
        try
        {
            while (true)
            {
                result = await container.ExecAsync(
                             ["/opt/couchbase/bin/couchbase-cli", command, "--cluster", "localhost:8091", "--username", "default", "--password", "password", .. arguments],
                             timeout.Token).ConfigureAwait(false);
                if (result.ExitCode == 0)
                {
                    return;
                }

                if (retryableError is null
                 || (result.Stdout.IndexOf(retryableError, StringComparison.Ordinal) < 0
                  && result.Stderr.IndexOf(retryableError, StringComparison.Ordinal) < 0))
                {
                    throw new InvalidOperationException($"Couchbase {command} failed: {result.Stdout}{result.Stderr}");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && retryableError is not null)
        {
            throw new TimeoutException($"Couchbase {command} did not complete within two minutes: {result.Stdout}{result.Stderr}", exception);
        }
    }

    private static async Task<string> CaptureServerLogsAsync(IContainer container)
    {
        // Docker stdout does not contain the server logs that explain why a bucket failed to become healthy.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            const string archivePath = "/tmp/couchbase-startup-logs.tar.gz";
            var result = await container.ExecAsync(
                             ["tar", "-czf", archivePath, "-C", "/opt/couchbase/var/lib/couchbase", "logs"],
                             timeout.Token).ConfigureAwait(false);

            // tar can return a nonzero exit code when active logs change; still retrieve the archive.
            var contents = await container.ReadFileAsync(archivePath, timeout.Token).ConfigureAwait(false);
            var directory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "artifacts", "build_data", "container-logs", nameof(CouchbaseFixture), container.Id);
            Directory.CreateDirectory(directory);
            var logPath = Path.Combine(directory, "couchbase-startup-logs.tar.gz");
            File.WriteAllBytes(logPath, contents);

            return $"\nCouchbase server logs saved to {logPath}.\nArchive exit code: {result.ExitCode}\n{result.Stdout}{result.Stderr}";
        }
        catch (Exception exception)
        {
            return $"\nUnable to capture Couchbase server logs: {exception.Message}";
        }
    }
}
