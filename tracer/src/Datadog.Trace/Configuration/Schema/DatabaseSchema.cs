// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal sealed class DatabaseSchema
    {
        private static readonly string[] OperationNames =
        [
            "cosmosdb.query",
            "couchbase.query",
            "elasticsearch.query",
            "mongodb.query",
        ];

        private readonly SchemaVersion _version;
        private readonly bool _peerServiceTagsEnabled;
        private readonly string[] _serviceNames;

        public DatabaseSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;

            // Calculate service names once, to avoid allocations with every call
            var useSuffix = version == SchemaVersion.V0 && !removeClientServiceNamesEnabled;
            _serviceNames =
            [
                useSuffix ? $"{defaultServiceName}-aerospike" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-cosmosdb" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-couchbase" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-elasticsearch" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-mongodb" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-redis" : defaultServiceName,
            ];

            if (serviceNameMappings is not null)
            {
                TryApplyMapping(_serviceNames, serviceNameMappings, "aerospike", ServiceType.Aerospike);
                TryApplyMapping(_serviceNames, serviceNameMappings, "couchbase", ServiceType.Couchbase);
                TryApplyMapping(_serviceNames, serviceNameMappings, "cosmosdb", ServiceType.CosmosDb);
                TryApplyMapping(_serviceNames, serviceNameMappings, "elasticsearch", ServiceType.Elasticsearch);
                TryApplyMapping(_serviceNames, serviceNameMappings, "mongodb", ServiceType.MongoDb);
                TryApplyMapping(_serviceNames, serviceNameMappings, "redis", ServiceType.Redis);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void TryApplyMapping(string[] serviceNames, IReadOnlyDictionary<string, string> mappings, string key, ServiceType dbType)
            {
                if (mappings.TryGetValue(key, out var mappedName))
                {
                    serviceNames[(int)dbType] = mappedName;
                }
            }
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding array in <see cref="OperationNames"/>
        /// and update the service name initialization in the constructor.
        /// </summary>
        public enum ServiceType
        {
            Aerospike,
            CosmosDb,
            Couchbase,
            Elasticsearch,
            MongoDb,
            Redis,
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding array in <see cref="OperationNames"/>
        /// and update the service name initialization in the constructor.
        /// </summary>
        public enum OperationType
        {
            CosmosDb,
            Couchbase,
            Elasticsearch,
            MongoDb,
        }

        public string GetOperationName(OperationType databaseType) => OperationNames[(int)databaseType];

        public string GetServiceName(ServiceType databaseType) => _serviceNames[(int)databaseType];

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

        public AwsDynamoDbTags CreateAwsDynamoDbTags() => new AwsDynamoDbTags();
    }
}
