// <copyright file="ContainersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#pragma warning disable SA1649 // File name should match first type name (this will just store all the classes)
#pragma warning disable SA1402 // File may only contain a single type (this will just store all the classes)
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    [CollectionDefinition(Name)]

    public class AerospikeCollection : ICollectionFixture<AerospikeFixture>
    {
        public const string Name = "Aerospike";
    }

    [CollectionDefinition(Name)]
    public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
    {
        public const string Name = "SqlServer";
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
