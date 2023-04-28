// <copyright file="NamingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema
{
    internal class NamingSchema
    {
        public NamingSchema(SchemaVersion version)
        {
            Version = version;
            Database = new DatabaseSchema(version);
            Messaging = new MessagingSchema(version);
        }

        // TODO: Temporary, we can probably delete this once we migrate all the code off MetadataSchemaVersion
        public SchemaVersion Version { get; }

        public DatabaseSchema Database { get; }

        public MessagingSchema Messaging { get; }
    }
}
