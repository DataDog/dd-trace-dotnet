// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class DatabaseSchema
    {
        private readonly SchemaVersion _version;
        private readonly string _defaultServiceName;
        private readonly IDictionary<string, string>? _serviceNameMappings;

        public DatabaseSchema(SchemaVersion version, string defaultServiceName, IDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
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
                SchemaVersion.V0 => $"{_defaultServiceName}-{databaseType}",
                _ => _defaultServiceName,
            };
        }

        public MongoDbTags CreateMongoDbTags()
            => _version switch
            {
                SchemaVersion.V0 => new MongoDbTags(),
                _ => new MongoDbV1Tags(),
            };

        public SqlTags CreateSqlTags()
            => _version switch
            {
                SchemaVersion.V0 => new SqlTags(),
                _ => new SqlV1Tags(),
            };
    }
}
