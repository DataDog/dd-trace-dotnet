// <copyright file="LocalStackFixture.cs" company="Datadog">
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
/// Provides a LocalStack container for AWS SDK integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class LocalStackFixture : ContainerFixture
{
    private const int LocalStackPort = 4566;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("AWS_SDK_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(LocalStackPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("localstack/localstack:latest")
                       .WithPortBinding(LocalStackPort, true)
                       .WithEnvironment("SERVICES", "s3,sqs,sns,dynamodb,kinesis,secretsmanager")
                       .WithEnvironment("DEBUG", "1")
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilHttpRequestIsSucceeded(r => r
                               .ForPort(LocalStackPort)
                               .ForPath("/_localstack/health")
                               .ForStatusCode(System.Net.HttpStatusCode.OK)))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
