// <copyright file="NamingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class NamingSchema
    {
        private readonly IReadOnlyDictionary<string, string>? _peerServiceNameMappings;
        private readonly bool _peerServiceTagsEnabled;

        public NamingSchema(
            SchemaVersion version,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled,
            string defaultServiceName,
            IReadOnlyDictionary<string, string>? serviceNameMappings,
            IReadOnlyDictionary<string, string>? peerServiceNameMappings)
        {
            Version = version;
            RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            Client = new ClientSchema(version, peerServiceTagsEnabled, removeClientServiceNamesEnabled, defaultServiceName, serviceNameMappings);
            Database = new DatabaseSchema(version, peerServiceTagsEnabled, removeClientServiceNamesEnabled, defaultServiceName, serviceNameMappings);
            Messaging = new MessagingSchema(version, peerServiceTagsEnabled, removeClientServiceNamesEnabled, defaultServiceName, serviceNameMappings);
            Server = new ServerSchema(version);
            _peerServiceNameMappings = peerServiceNameMappings;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;
        }

        // TODO: Temporary, we can probably delete this once we migrate all the code off MetadataSchemaVersion
        public SchemaVersion Version { get; }

        public ClientSchema Client { get; }

        public DatabaseSchema Database { get; }

        public MessagingSchema Messaging { get; }

        public ServerSchema Server { get; }

        public bool RemoveClientServiceNamesEnabled { get; }

        public void RemapPeerService(ITags tags)
        {
            if ((Version.Equals(SchemaVersion.V0) && !_peerServiceTagsEnabled) || _peerServiceNameMappings is null || _peerServiceNameMappings.Count == 0)
            {
                return;
            }

            var peerService = tags.GetTag(Tags.PeerService);
            if (peerService is null)
            {
                return;
            }

            if (_peerServiceNameMappings.TryGetValue(peerService, out var mappedServiceName))
            {
                tags.SetTag(Tags.PeerServiceRemappedFrom, peerService);
                tags.SetTag(Tags.PeerService, mappedServiceName);
            }
        }
    }
}
