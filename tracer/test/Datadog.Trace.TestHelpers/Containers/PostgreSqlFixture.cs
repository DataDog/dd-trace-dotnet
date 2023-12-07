// <copyright file="PostgreSqlFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace Datadog.Trace.TestHelpers.Containers;

public class PostgreSqlFixture : ContainerFixture
{
    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("POSTGRES_HOST", Container.Hostname);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new PostgreSqlBuilder()
                       .WithPortBinding(5432)
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
