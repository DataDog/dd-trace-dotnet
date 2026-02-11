// <copyright file="DatabaseSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class DatabaseSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "sql-server", "custom-db" },
            { "mongodb", "my-mongo" },
        };

        public static IEnumerable<(int SchemaVersion, string DatabaseType, string ExpectedOperationName)> GetOperationNameData()
        {
            yield return (0, "mongodb", "mongodb.query");
            yield return (0, "elasticsearch", "elasticsearch.query");
            yield return (1, "mongodb", "mongodb.query");
            yield return (1, "elasticsearch", "elasticsearch.query");
        }

        public static IEnumerable<(int SchemaVersion, string DatabaseType, string ExpectedValue, bool RemoveClientServiceNamesEnabled)> GetServiceNameData()
        {
            // Mapped service names (always return mapped value)
            yield return (0, "mongodb", "my-mongo", true);
            yield return (0, "mongodb", "my-mongo", false);
            yield return (1, "mongodb", "my-mongo", true);
            yield return (1, "mongodb", "my-mongo", false);
            // Unmapped service names
            yield return (0, "elasticsearch", DefaultServiceName, true);
            yield return (0, "elasticsearch", $"{DefaultServiceName}-elasticsearch", false);
            yield return (1, "elasticsearch", DefaultServiceName, true);
            yield return (1, "elasticsearch", DefaultServiceName, false);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] (int SchemaVersion, string DatabaseType, string ExpectedOperationName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Database.GetOperationName(values.DatabaseType).Should().Be(values.ExpectedOperationName);
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceNameIsCorrect(
            [CombinatorialMemberData(nameof(GetServiceNameData))] (int SchemaVersion, string DatabaseType, string ExpectedValue, bool RemoveClientServiceNamesEnabled) values,
            bool peerServiceTagsEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, values.RemoveClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Database.GetServiceName(values.DatabaseType).Should().Be(values.ExpectedValue);
        }

        [Theory]
        [CombinatorialData]
        public void CreateCouchbaseTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(CouchbaseTags),
                _ => typeof(CouchbaseV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateCouchbaseTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateElasticsearchTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(ElasticsearchTags),
                _ => typeof(ElasticsearchV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateElasticsearchTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateMongoDbTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(MongoDbTags),
                _ => typeof(MongoDbV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateMongoDbTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateSqlTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(SqlTags),
                _ => typeof(SqlV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateSqlTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateRedisTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(RedisTags),
                _ => typeof(RedisV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateRedisTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateCosmosDbTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(CosmosDbTags),
                _ => typeof(CosmosDbV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateCosmosDbTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateAerospikeTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(AerospikeTags),
                _ => typeof(AerospikeV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateAerospikeTags().Should().BeOfType(expectedType);
        }
    }
}
