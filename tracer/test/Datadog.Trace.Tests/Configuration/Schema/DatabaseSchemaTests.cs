// <copyright file="DatabaseSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
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
        private readonly string[] _unmappedKeys = { "elasticsearch", "postgres", "custom-service" };
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "sql-server", "custom-db" },
            { "http-client", "some-service" },
            { "mongodb", "my-mongo" },
        };

        public static IEnumerable<object[]> GetAllConfigs()
            => from schemaVersion in new object[] { SchemaVersion.V0, SchemaVersion.V1 }
               from peerServiceTagsEnabled in new[] { true, false }
               from removeClientServiceNamesEnabled in new[] { true, false }
               select new[] { schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled };

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetOperationNameIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var databaseType = "DbCommand";
            var expectedValue = $"{databaseType}.query";

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Database.GetOperationName(databaseType).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesMappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());

            foreach (var kvp in _mappings)
            {
                namingSchema.Database.GetServiceName(kvp.Key).Should().Be(kvp.Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesUnmappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());

            foreach (var key in _unmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{DefaultServiceName}-{key}",
                    _ => DefaultServiceName,
                };

                namingSchema.Database.GetServiceName(key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateCouchbaseTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(CouchbaseTags),
                _ => typeof(CouchbaseV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateCouchbaseTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateElasticsearchTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(ElasticsearchTags),
                _ => typeof(ElasticsearchV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateElasticsearchTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateMongoDbTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(MongoDbTags),
                _ => typeof(MongoDbV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateMongoDbTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateSqlTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(SqlTags),
                _ => typeof(SqlV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateSqlTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateRedisTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(RedisTags),
                _ => typeof(RedisV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateRedisTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateCosmosDbTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(CosmosDbTags),
                _ => typeof(CosmosDbV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Database.CreateCosmosDbTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateAerospikeTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
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
