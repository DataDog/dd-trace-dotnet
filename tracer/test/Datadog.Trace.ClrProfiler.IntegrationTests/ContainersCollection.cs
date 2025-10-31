// <copyright file="ContainersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

/// <summary>
/// Collection definition for Postgres tests.
/// Using ICollectionFixture ensures that ONE PostgresFixture instance is shared across all test classes
/// in this collection. The container starts when the first test runs and stops when the last test finishes.
/// </summary>
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

/// <summary>
/// Collection definition for Aerospike tests.
/// Using ICollectionFixture ensures that ONE AerospikeFixture instance is shared across all test classes
/// in this collection. The container starts when the first test runs and stops when the last test finishes.
/// </summary>
#pragma warning disable SA1402 // File may only contain a single type
[CollectionDefinition(Name)]
public class AerospikeCollection : ICollectionFixture<AerospikeFixture>
{
    public const string Name = "Aerospike";
}
#pragma warning restore SA1402 // File may only contain a single type
