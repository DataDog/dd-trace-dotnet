// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
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

            _serviceNameMetadata =
            [
                ServiceNameMetadata.Resolve("aerospike", defaultServiceName, serviceNameMappings, useSuffix),
                ServiceNameMetadata.Resolve("cosmosdb", defaultServiceName, serviceNameMappings, useSuffix),
                ServiceNameMetadata.Resolve("couchbase", defaultServiceName, serviceNameMappings, useSuffix),
                ServiceNameMetadata.Resolve("elasticsearch", defaultServiceName, serviceNameMappings, useSuffix),
                ServiceNameMetadata.Resolve("mongodb", defaultServiceName, serviceNameMappings, useSuffix),
                ServiceNameMetadata.Resolve("redis", defaultServiceName, serviceNameMappings, useSuffix),
            ];
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

        public SqlTags CreateSqlTags()
            => _useV0Tags ? new SqlTags() : new SqlV1Tags();

        public CosmosDbTags CreateCosmosDbTags()
            => _useV0Tags ? new CosmosDbTags() : new CosmosDbV1Tags();

        public AerospikeTags CreateAerospikeTags()
            => _useV0Tags ? new AerospikeTags() : new AerospikeV1Tags();

        public AwsDynamoDbTags CreateAwsDynamoDbTags() => new();
    }
}
