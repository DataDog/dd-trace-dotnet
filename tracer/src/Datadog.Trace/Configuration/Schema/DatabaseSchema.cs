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
    internal sealed class DatabaseSchema
    {
        private static readonly string[] OperationNames =
        [
            "cosmosdb.query",
            "couchbase.query",
            "elasticsearch.query",
            "mongodb.query",
        ];

        private readonly bool _useV0Tags;
        private readonly ServiceNameMetadata[] _serviceNameMetadata;

        public DatabaseSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _useV0Tags = version == SchemaVersion.V0 && !peerServiceTagsEnabled;

            // Calculate service names and source metadata once, to avoid allocations with every call
            var useSuffix = version == SchemaVersion.V0 && !removeClientServiceNamesEnabled;
            var serviceNames = new string[]
            {
                useSuffix ? $"{defaultServiceName}-aerospike" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-cosmosdb" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-couchbase" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-elasticsearch" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-mongodb" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-redis" : defaultServiceName,
            };

            if (serviceNameMappings is not null)
            {
                TryApplyMapping(serviceNames, serviceNameMappings, "aerospike", ServiceType.Aerospike);
                TryApplyMapping(serviceNames, serviceNameMappings, "couchbase", ServiceType.Couchbase);
                TryApplyMapping(serviceNames, serviceNameMappings, "cosmosdb", ServiceType.CosmosDb);
                TryApplyMapping(serviceNames, serviceNameMappings, "elasticsearch", ServiceType.Elasticsearch);
                TryApplyMapping(serviceNames, serviceNameMappings, "mongodb", ServiceType.MongoDb);
                TryApplyMapping(serviceNames, serviceNameMappings, "redis", ServiceType.Redis);
            }

            // Build combined service name + source metadata
            ServiceNameMetadata Build(string serviceName, string sourceName) =>
                new(serviceName, serviceName != defaultServiceName ? sourceName : null);

            _serviceNameMetadata =
            [
                Build(serviceNames[(int)ServiceType.Aerospike], "aerospike"),
                Build(serviceNames[(int)ServiceType.CosmosDb], "cosmosdb"),
                Build(serviceNames[(int)ServiceType.Couchbase], "couchbase"),
                Build(serviceNames[(int)ServiceType.Elasticsearch], "elasticsearch"),
                Build(serviceNames[(int)ServiceType.MongoDb], "mongodb"),
                Build(serviceNames[(int)ServiceType.Redis], "redis"),
            ];

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

        public ServiceNameMetadata GetServiceNameMetadata(ServiceType databaseType) => _serviceNameMetadata[(int)databaseType];

        public CouchbaseTags CreateCouchbaseTags()
            => _useV0Tags ? new CouchbaseTags() : new CouchbaseV1Tags();

        public ElasticsearchTags CreateElasticsearchTags()
            => _useV0Tags ? new ElasticsearchTags() : new ElasticsearchV1Tags();

        public MongoDbTags CreateMongoDbTags()
            => _useV0Tags ? new MongoDbTags() : new MongoDbV1Tags();

        public SqlTags CreateSqlTags()
            => _useV0Tags ? new SqlTags() : new SqlV1Tags();

        public RedisTags CreateRedisTags()
            => _useV0Tags ? new RedisTags() : new RedisV1Tags();

        public CosmosDbTags CreateCosmosDbTags()
            => _useV0Tags ? new CosmosDbTags() : new CosmosDbV1Tags();

        public AerospikeTags CreateAerospikeTags()
            => _useV0Tags ? new AerospikeTags() : new AerospikeV1Tags();

        public AwsDynamoDbTags CreateAwsDynamoDbTags() => new();
    }
}
