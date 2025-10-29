// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

/// <summary>
/// Provides a Couchbase container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class CouchbaseFixture : ContainerFixture
{
    private const int CouchbasePort = 8091;
    private const string CouchbaseUser = "Administrator";
    private const string CouchbasePassword = "password";

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("COUCHBASE_HOST", Container.Hostname);
        yield return new("COUCHBASE_PORT", Container.GetMappedPublicPort(CouchbasePort).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("couchbase:latest")
                       .WithPortBinding(CouchbasePort, true)
                       .WithPortBinding(8092, true)  // Views
                       .WithPortBinding(8093, true)  // Query
                       .WithPortBinding(8094, true)  // Search
                       .WithPortBinding(11210, true) // Data
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilHttpRequestIsSucceeded(r => r
                               .ForPort(CouchbasePort)
                               .ForPath("/ui/index.html")
                               .ForStatusCode(System.Net.HttpStatusCode.OK)))
                       .Build();

        await container.StartAsync();

        // Initialize Couchbase cluster (simplified - may need additional setup in real tests)
        // This would typically involve calling the REST API to configure the cluster
        // For now, just wait for the container to be ready

        registerResource("container", container);
    }
}
