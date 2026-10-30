// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

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
                     .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("curl", "--fail", "--silent", "http://localhost:8091/pools"));

        if (useHostNetwork)
        {
            builder = builder.WithCreateParameterModifier(p =>
            {
                p.HostConfig ??= new HostConfig();
                p.HostConfig.NetworkMode = "host";
            });
        }
        else
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
        await RunCliAsync(container, "bucket-create", "--bucket", "default", "--bucket-type", "couchbase", "--bucket-ramsize", "100", "--bucket-replica", "0", "--wait").ConfigureAwait(false);
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
}
