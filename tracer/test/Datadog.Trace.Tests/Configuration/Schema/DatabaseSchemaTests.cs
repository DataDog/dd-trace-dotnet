// <copyright file="DatabaseSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Configuration.Schema
{
#pragma warning disable SA1201 // A method should not follow a class
    public class DatabaseSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "sql-server", "custom-db" },
            { "mongodb", "my-mongo" },
        };

        public class OperationNameData : IXunitSerializable
        {
            public OperationNameData()
            {
            }

            public OperationNameData(int schemaVersion, int databaseType, string expectedOperationName)
            {
                SchemaVersion = schemaVersion;
                DatabaseType = databaseType;
                ExpectedOperationName = expectedOperationName;
            }

            public int SchemaVersion { get; private set; }

            public int DatabaseType { get; private set; }

            public string ExpectedOperationName { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                DatabaseType = info.GetValue<int>(nameof(DatabaseType));
                ExpectedOperationName = info.GetValue<string>(nameof(ExpectedOperationName));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(DatabaseType), DatabaseType);
                info.AddValue(nameof(ExpectedOperationName), ExpectedOperationName);
            }
        }

        public class ServiceNameData : IXunitSerializable
        {
            public ServiceNameData()
            {
            }

            public ServiceNameData(int schemaVersion, int databaseType, string expectedValue, bool removeClientServiceNamesEnabled)
            {
                SchemaVersion = schemaVersion;
                DatabaseType = databaseType;
                ExpectedValue = expectedValue;
                RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            }

            public int SchemaVersion { get; private set; }

            public int DatabaseType { get; private set; }

            public string ExpectedValue { get; private set; }

            public bool RemoveClientServiceNamesEnabled { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                DatabaseType = info.GetValue<int>(nameof(DatabaseType));
                ExpectedValue = info.GetValue<string>(nameof(ExpectedValue));
                RemoveClientServiceNamesEnabled = info.GetValue<bool>(nameof(RemoveClientServiceNamesEnabled));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(DatabaseType), DatabaseType);
                info.AddValue(nameof(ExpectedValue), ExpectedValue);
                info.AddValue(nameof(RemoveClientServiceNamesEnabled), RemoveClientServiceNamesEnabled);
            }
        }

        public static IEnumerable<OperationNameData> GetOperationNameData()
        {
            yield return new(0, (int)DatabaseSchema.OperationType.MongoDb, "mongodb.query");
            yield return new(0, (int)DatabaseSchema.OperationType.Elasticsearch, "elasticsearch.query");
            yield return new(1, (int)DatabaseSchema.OperationType.MongoDb, "mongodb.query");
            yield return new(1, (int)DatabaseSchema.OperationType.Elasticsearch, "elasticsearch.query");
        }

        public static IEnumerable<ServiceNameData> GetServiceNameData()
        {
            // Mapped service names (always return mapped value)
            yield return new(0, (int)DatabaseSchema.ServiceType.MongoDb, "my-mongo", true);
            yield return new(0, (int)DatabaseSchema.ServiceType.MongoDb, "my-mongo", false);
            yield return new(1, (int)DatabaseSchema.ServiceType.MongoDb, "my-mongo", true);
            yield return new(1, (int)DatabaseSchema.ServiceType.MongoDb, "my-mongo", false);
            // Unmapped service names
            yield return new(0, (int)DatabaseSchema.ServiceType.Elasticsearch, DefaultServiceName, true);
            yield return new(0, (int)DatabaseSchema.ServiceType.Elasticsearch, $"{DefaultServiceName}-elasticsearch", false);
            yield return new(1, (int)DatabaseSchema.ServiceType.Elasticsearch, DefaultServiceName, true);
            yield return new(1, (int)DatabaseSchema.ServiceType.Elasticsearch, DefaultServiceName, false);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] OperationNameData values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Database.GetOperationName((DatabaseSchema.OperationType)values.DatabaseType).Should().Be(values.ExpectedOperationName);
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceNameIsCorrect(
            [CombinatorialMemberData(nameof(GetServiceNameData))] ServiceNameData values,
            bool peerServiceTagsEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, values.RemoveClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Database.GetServiceName((DatabaseSchema.ServiceType)values.DatabaseType).Should().Be(values.ExpectedValue);
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

        [Theory]
        [CombinatorialData]
        public void GetOperationName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(DatabaseSchema.OperationType)).Cast<DatabaseSchema.OperationType>())
            {
                namingSchema.Database.GetOperationName(value).Should().NotBeNull();
            }
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(DatabaseSchema.ServiceType)).Cast<DatabaseSchema.ServiceType>())
            {
                namingSchema.Database.GetServiceName(value).Should().NotBeNull();
            }
        }
    }
#pragma warning restore SA1201 // A method should not follow a class
}
