// <copyright file="AerospikeFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;

namespace Datadog.Trace.TestHelpers.Containers;

public class AerospikeFixture : ContainerFixture
{
    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
                       .WithImage("aerospike/aerospike-server:6.2.0.6")
                       .WithPortBinding(3000)
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
