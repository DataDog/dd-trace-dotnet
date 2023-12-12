// <copyright file="LocalStackFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Testcontainers.LocalStack;

namespace Datadog.Trace.TestHelpers.Containers
{
    public class LocalStackFixture : ContainerFixture
    {
        protected override async Task InitializeResources(Action<string, object> registerResource)
        {
            var container = new LocalStackBuilder()
                           .WithImage("localstack/localstack")
                           .WithEnvironment("SERVICES", "sns,sqs,kinesis,dynamodb")
                           .WithEnvironment("DEBUG", "1")
                           .WithEnvironment("DATA_DIR", "/tmp/localstack/data")
                           .WithEnvironment("DEFAULT_REGION", "us-east-1")
                           .WithPortBinding(4566)
                           .WithBindMount(Path.Combine(EnvironmentTools.GetSolutionDirectory(), "localstack"), "/tmp")
                           .Build();

            await container.StartAsync();

            registerResource("container", container);
        }
    }
}
