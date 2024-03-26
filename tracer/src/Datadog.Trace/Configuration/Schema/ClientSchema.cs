// <copyright file="ClientSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class ClientSchema
    {
        private readonly SchemaVersion _version;
        private readonly bool _peerServiceTagsEnabled;
        private readonly bool _removeClientServiceNamesEnabled;
        private readonly string _defaultServiceName;
        private readonly IReadOnlyDictionary<string, string>? _serviceNameMappings;

        public ClientSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;
            _removeClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            _defaultServiceName = defaultServiceName;
            _serviceNameMappings = serviceNameMappings;
        }

        public string GetOperationNameForProtocol(string protocol) =>
            _version switch
            {
                SchemaVersion.V0 => $"{protocol}.request",
                _ => $"{protocol}.client.request",
            };

        public string GetOperationNameForRequestType(string requestType) =>
            _version switch
            {
                SchemaVersion.V0 => $"{requestType}",
                _ => $"{requestType}.request",
            };

        public string GetServiceName(string component)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(component, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V0 when !_removeClientServiceNamesEnabled => $"{_defaultServiceName}-{component}",
                _ => _defaultServiceName,
            };
        }

        public HttpTags CreateHttpTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new HttpTags(),
                _ => new HttpV1Tags(),
            };

        public GrpcClientTags CreateGrpcClientTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new GrpcClientTags(),
                _ => new GrpcClientV1Tags(),
            };

        public RemotingClientTags CreateRemotingClientTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new RemotingClientTags(),
                _ => new RemotingClientV1Tags(),
            };

        public ServiceRemotingClientTags CreateServiceRemotingClientTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new ServiceRemotingClientTags(),
                _ => new ServiceRemotingClientV1Tags(),
            };

        public AzureServiceBusTags CreateAzureServiceBusTags()
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new AzureServiceBusTags(),
                _ => new AzureServiceBusV1Tags(),
            };
    }
}
