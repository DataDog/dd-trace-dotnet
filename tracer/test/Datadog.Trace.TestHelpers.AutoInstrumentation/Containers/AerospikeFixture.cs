// <copyright file="AerospikeFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class AerospikeFixture : ContainerFixture
{
    private const int AerospikePort = 3000;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("AEROSPIKE_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(AerospikePort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // pinning to a known good version because the latest version
        // (6.3.0.5 at time of issue) causes 'Server memory error' and flake
        // Keep syncronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("aerospike/aerospike-server:6.2.0.6")
                       .WithPortBinding(AerospikePort, true)
                       .WithCreateParameterModifier(p =>
                        {
                            p.HostConfig ??= new HostConfig();
                            p.HostConfig.Ulimits = new List<Ulimit>
                            {
                                // Aerospike requires a minimum of 15000 file descriptors, otherwise it'll fail to start
                                // Some versions of dockerengine set a lower limit (1024)
                                new Ulimit { Name = "nofile", Soft = 15000, Hard = 15000 }
                            };
                        })
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(AerospikePort))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
