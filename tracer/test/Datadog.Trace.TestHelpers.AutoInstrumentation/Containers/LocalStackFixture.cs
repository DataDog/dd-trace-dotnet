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

public class LocalStackFixture : ContainerFixture
{
    private const ushort EdgePort = 4566;

    // Keep synchronized with the image version in docker-compose.yml.
    private const string Image = "localstack/localstack:4.14.0@sha256:3ebc37595918b8accb852f8048fef2aff047d465167edd655528065b07bc364a";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(EdgePort);

    public string HostAndPort => $"{Host}:{Port}";

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("AWS_SDK_HOST", HostAndPort);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       .WithPortBinding(EdgePort, true)
                       .WithEnvironment("SERVICES", "sns,sqs,kinesis,dynamodb,events,s3,stepfunctions,lambda")
                       .WithEnvironment("DEBUG", "1")
                       .WithEnvironment("DEFAULT_REGION", "us-east-1")
                       .WithEnvironment("SQS_ENDPOINT_STRATEGY", "dynamic")
                       .WithWaitStrategy(
                            Wait.ForUnixContainer()
                                .UntilHttpRequestIsSucceeded(
                                     request => request.ForPort(EdgePort).ForPath("/_localstack/health"),
                                     strategy => strategy.WithTimeout(TimeSpan.FromMinutes(2))))
                       .Build();

        registerResource("container", container);
        await StartContainerAsync(container).ConfigureAwait(false);
    }
}
