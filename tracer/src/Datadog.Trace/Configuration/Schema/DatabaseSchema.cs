// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal sealed class DatabaseSchema
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatabaseSchema>();

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
                Log.Debug("DBM: Service name resolved via mapping. MappingPresent: true");
                return mappedServiceName;
            }

            var result = _version switch
            {
                SchemaVersion.V0 when !_removeClientServiceNamesEnabled => $"{_defaultServiceName}-{databaseType}",
                _ => _defaultServiceName,
            };

            Log.Information(
                "DBM: Service name resolved via schema. SchemaVersion: '{Version}', RemoveClientServiceNames: {RemoveClientServiceNames}, ServiceNamePresent: {ServiceNamePresent}",
                _version,
                _removeClientServiceNamesEnabled,
                !string.IsNullOrEmpty(result));

            return result;
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
        {
            Log.Information(
                "DBM: CreateSqlTags called. SchemaVersion: '{Version}', PeerServiceTagsEnabled: {PeerServiceTagsEnabled}",
                _version,
                _peerServiceTagsEnabled);

            var result = _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new SqlTags(),
                _ => new SqlV1Tags(),
            };

            Log.Information(
                "DBM: CreateSqlTags result. TagsType: '{TagsType}'",
                result.GetType().Name);

            return result;
        }

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

        public AwsDynamoDbTags CreateAwsDynamoDbTags() => new AwsDynamoDbTags();
    }
}
