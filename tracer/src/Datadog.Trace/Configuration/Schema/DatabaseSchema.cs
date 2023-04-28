// <copyright file="DatabaseSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema
{
    internal class DatabaseSchema
    {
        private readonly SchemaVersion _version;

        public DatabaseSchema(SchemaVersion version)
        {
            _version = version;
        }

        public string GetOperationName(string databaseType) => $"{databaseType}.query";

        public string GetServiceName(string applicationName, string databaseType)
            => _version switch
            {
                SchemaVersion.V1 => applicationName,
                _ => $"{applicationName}-{databaseType}",
            };
    }
}
