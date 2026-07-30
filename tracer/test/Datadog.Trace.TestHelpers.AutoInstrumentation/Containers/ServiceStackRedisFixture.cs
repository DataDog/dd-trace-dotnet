// <copyright file="ServiceStackRedisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class ServiceStackRedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;
    private const string DefaultHostConfiguration = "localhost:6379";

    private string? _hostConfiguration;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new(
            "SERVICESTACK_REDIS_HOST",
            _hostConfiguration ?? $"{Container.Hostname}:{Container.GetMappedPublicPort(RedisPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var hostConfiguration = Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !string.IsNullOrEmpty(hostConfiguration))
        {
            _hostConfiguration = string.IsNullOrEmpty(hostConfiguration) ? DefaultHostConfiguration : hostConfiguration;
            return;
        }

        // Keep synchronized with the image version in docker-compose.yml.
        var container = new ContainerBuilder("redis:4-alpine@sha256:aaf7c123077a5e45ab2328b5ef7e201b5720616efac498d55e65a7afbb96ae20")
                       .WithCommand("redis-server", "--bind", "0.0.0.0")
                       .WithPortBinding(RedisPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                       .Build();

        registerResource("container", container);
        await container.StartAsync().ConfigureAwait(false);
    }
}
