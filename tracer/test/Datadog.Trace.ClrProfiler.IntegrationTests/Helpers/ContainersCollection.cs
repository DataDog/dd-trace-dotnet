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
    // Container-backed collections must set DisableParallelization = true. CustomTestFramework only auto-serializes
    // collections whose display name contains the assembly namespace, which named [CollectionDefinition]s lose.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class AerospikeCollection : ICollectionFixture<AerospikeFixture>
    {
        public const string Name = "Aerospike";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class MongoDbCollection : ICollectionFixture<MongoDbFixture>
    {
        public const string Name = "MongoDb";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class Elasticsearch5Collection : ICollectionFixture<Elasticsearch5Fixture>
    {
        public const string Name = "Elasticsearch5";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class Elasticsearch6Collection : ICollectionFixture<Elasticsearch6Fixture>
    {
        public const string Name = "Elasticsearch6";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class Elasticsearch7Collection : ICollectionFixture<Elasticsearch7Fixture>
    {
        public const string Name = "Elasticsearch7";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ServiceStackRedisCollection : ICollectionFixture<ServiceStackRedisFixture>
    {
        public const string Name = "ServiceStackRedis";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
    {
        public const string Name = "SqlServer";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        public const string Name = "Postgres";
    }

    // MySQL 8 is shared by the MySql.Data and MySqlConnector tests to avoid restarting the container.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class MySqlCollection : ICollectionFixture<MySql8Fixture>
    {
        public const string Name = "MySql";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
    {
        public const string Name = "RabbitMq";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class KafkaCollection : ICollectionFixture<KafkaFixture>
    {
        public const string Name = "Kafka";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class CouchbaseCollection : ICollectionFixture<CouchbaseFixture>
    {
        public const string Name = "Couchbase";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class CosmosDbVnextCollection : ICollectionFixture<CosmosDbVnextFixture>
    {
        public const string Name = "CosmosDbVnext";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class LocalStackCollection : ICollectionFixture<LocalStackFixture>
    {
        public const string Name = "LocalStack";
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
