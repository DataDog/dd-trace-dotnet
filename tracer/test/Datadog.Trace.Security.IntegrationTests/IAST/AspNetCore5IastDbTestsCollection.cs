// <copyright file="AspNetCore5IastDbTestsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;

namespace Datadog.Trace.Security.IntegrationTests.IAST;

[CollectionDefinition(Name, DisableParallelization = true)]
public class AspNetCore5IastDbTestsCollection : ICollectionFixture<SqlServerFixture>, ICollectionFixture<PostgresFixture>, ICollectionFixture<MySql8Fixture>
{
    public const string Name = "AspNetCore5IastDbTests";
}

#endif
