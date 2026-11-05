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
        Host = useHostNetwork ? GetDockerHost() : container.Hostname;

        await RunCliAsync(container, "node-init", "--node-init-hostname", Host).ConfigureAwait(false);
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
        var history = new StringBuilder();
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
                await WaitForBucketReadyAsync(container, history, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("Couchbase bucket 'default' did not become healthy within two minutes of creation.", exception);
            }
        }
        catch (Exception exception)
        {
            var diagnostics = await CaptureBucketDiagnosticsAsync(container, history.ToString()).ConfigureAwait(false);
            throw new InvalidOperationException($"Couchbase bucket initialization failed: {exception.Message}{diagnostics}", exception);
        }

        elapsed.Stop();
        if (elapsed.Elapsed > TimeSpan.FromMinutes(1))
        {
            // Preserve evidence even when the longer deadline allows startup to recover.
            var diagnostics = await CaptureBucketDiagnosticsAsync(container, history.ToString()).ConfigureAwait(false);
            container.Logger.LogWarning("Couchbase bucket became healthy after {Elapsed}. {Diagnostics}", elapsed.Elapsed, diagnostics);
        }
    }

    private static async Task WaitForBucketReadyAsync(IContainer container, StringBuilder history, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = DateTime.UtcNow;
            var result = await container.ExecAsync(
                             ["curl", "--fail", "--silent", "--show-error", "--max-time", "5", "--user", "default:password", "http://localhost:8091/pools/default/buckets/default"],
                             cancellationToken).ConfigureAwait(false);
            history.AppendLine($"{started:O}: exit code {result.ExitCode}\n{result.Stdout}{result.Stderr}");
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

    private static async Task<string> CaptureBucketDiagnosticsAsync(IContainer container, string history)
    {
        // Docker stdout does not contain the server logs that explain why a bucket failed to become healthy.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var directory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "artifacts", "build_data", "container-logs", nameof(CouchbaseFixture), container.Id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "bucket-readiness.log"), history);
            var failures = new List<string>();
            const string systemStateCommand = """
                date -u
                ps -eo pid,ppid,stat,pcpu,pmem,comm
                df -h /opt/couchbase/var/lib/couchbase
                for file in /proc/loadavg /proc/meminfo /proc/pressure/* /proc/diskstats /proc/sys/fs/file-nr /proc/net/route \
                            /sys/fs/cgroup/cpu.stat /sys/fs/cgroup/memory.current /sys/fs/cgroup/memory.max \
                            /sys/fs/cgroup/memory.events /sys/fs/cgroup/io.stat /sys/fs/cgroup/pids.*; do
                    if [ -r "$file" ]; then printf '\n%s\n' "$file"; cat "$file"; fi
                done
                for pid in $(pidof beam.smp memcached); do
                    for file in /proc/$pid/status /proc/$pid/limits /proc/$pid/io /proc/$pid/wchan; do
                        if [ -r "$file" ]; then printf '\n%s\n' "$file"; cat "$file"; fi
                    done
                    printf '\nOpen file descriptors for process %s: ' "$pid"
                    ls /proc/$pid/fd | wc -l
                done
                """;
            await CaptureCommandAsync("system-state.txt", ["sh", "-c", systemStateCommand.Replace("\r\n", "\n")]).ConfigureAwait(false);
            await CaptureEndpointAsync("bucket-status.txt", "/pools/default/buckets/default").ConfigureAwait(false);
            await CaptureEndpointAsync("cluster-status.txt", "/pools/default").ConfigureAwait(false);
            await CaptureEndpointAsync("node-services.txt", "/pools/default/nodeServices").ConfigureAwait(false);

            // Include rotated logs, whose names cannot be predicted in advance.
            const string archivePath = "/tmp/couchbase-startup-logs.tar.gz";
            await CaptureCommandAsync(
                "archive-logs.txt",
                ["tar", "-czf", archivePath, "-C", "/opt/couchbase/var/lib/couchbase", "logs"],
                timeoutSeconds: 15).ConfigureAwait(false);
            try
            {
                // tar can return a nonzero exit code when active logs change; still retrieve the archive.
                var contents = await container.ReadFileAsync(archivePath, timeout.Token).ConfigureAwait(false);
                File.WriteAllBytes(Path.Combine(directory, "couchbase-startup-logs.tar.gz"), contents);
            }
            catch (Exception exception)
            {
                failures.Add($"Unable to retrieve Couchbase server logs: {exception.Message}");
            }

            return $"\nCouchbase bucket diagnostics saved to {directory}.\n{string.Join("\n", failures)}";

            Task CaptureEndpointAsync(string file, string endpoint)
                => CaptureCommandAsync(file, ["curl", "--silent", "--show-error", "--max-time", "4", "--include", "--user", "default:password", $"http://localhost:8091{endpoint}"]);

            async Task CaptureCommandAsync(string file, string[] command, int timeoutSeconds = 5)
            {
                using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                commandTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                var started = DateTime.UtcNow;
                try
                {
                    var result = await container.ExecAsync(command, commandTimeout.Token).ConfigureAwait(false);
                    File.WriteAllText(Path.Combine(directory, file), $"Started: {started:O}\nExit code: {result.ExitCode}\n{result.Stdout}{result.Stderr}");
                }
                catch (Exception exception)
                {
                    failures.Add($"Unable to capture {file}: {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            return $"\nUnable to capture Couchbase bucket diagnostics: {exception.Message}";
        }
    }
}
