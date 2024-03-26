// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class DatabaseSchema
    {
        private readonly SchemaVersion _version;
        private readonly bool _peerServiceTagsEnabled;
        private readonly bool _removeClientServiceNamesEnabled;
        private readonly string _defaultServiceName;
        private readonly IReadOnlyDictionary<string, string>? _serviceNameMappings;

        public DatabaseSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;
            _removeClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            _defaultServiceName = defaultServiceName;
            _serviceNameMappings = serviceNameMappings;
        }

        public string GetOperationName(string databaseType) => $"{databaseType}.query";

        public string GetServiceName(string databaseType)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(databaseType, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V0 when !_removeClientServiceNamesEnabled => $"{_defaultServiceName}-{databaseType}",
                _ => _defaultServiceName,
            };
        }

        public CouchbaseTags CreateCouchbaseTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new CouchbaseTags(),
                _ => new CouchbaseV1Tags(),
            };

        public ElasticsearchTags CreateElasticsearchTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new ElasticsearchTags(),
                _ => new ElasticsearchV1Tags(),
            };

        public MongoDbTags CreateMongoDbTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new MongoDbTags(),
                _ => new MongoDbV1Tags(),
            };

        public SqlTags CreateSqlTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new SqlTags(),
                _ => new SqlV1Tags(),
            };

        public RedisTags CreateRedisTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new RedisTags(),
                _ => new RedisV1Tags(),
            };

        public CosmosDbTags CreateCosmosDbTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new CosmosDbTags(),
                _ => new CosmosDbV1Tags(),
            };

        public AerospikeTags CreateAerospikeTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new AerospikeTags(),
                _ => new AerospikeV1Tags(),
            };

        public AwsDynamoDbTags CreateAwsDynamoDbTags() => _version switch
        {
            SchemaVersion.V0 when !_peerServiceTagsEnabled => new AwsDynamoDbTags(),
            _ => new AwsDynamoDbV1Tags(),
        };
    }
}
