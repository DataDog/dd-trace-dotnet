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
    internal sealed class ClientSchema
    {
        private const string HttpClientComponent = "http-client";
        private const string GrpcClientComponent = "grpc-client";

        private readonly bool _useV0Tags;
        private readonly string[] _protocols;
        private readonly ServiceNameMetadata[] _serviceNameMetadata;
        private readonly string _operationNameSuffix;

        public ClientSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _useV0Tags = version == SchemaVersion.V0 && !peerServiceTagsEnabled;
            _protocols = version switch
            {
                SchemaVersion.V0 => V0Values.ProtocolOperationNames,
                _ => V1Values.ProtocolOperationNames,
            };
            _operationNameSuffix = version switch
            {
                SchemaVersion.V0 => string.Empty,
                _ => ".request",
            };

            // Calculate service names and source metadata once, to avoid allocations with every call
            var useSuffix = version == SchemaVersion.V0 && !removeClientServiceNamesEnabled;

            ServiceNameMetadata Resolve(string integrationKey)
            {
                if (serviceNameMappings is not null && serviceNameMappings.TryGetValue(integrationKey, out var mappedName))
                {
                    return new(mappedName, mappedName != defaultServiceName ? "opt.service_mapping" : null);
                }

                var name = useSuffix ? $"{defaultServiceName}-{integrationKey}" : defaultServiceName;
                return new(name, name != defaultServiceName ? integrationKey : null);
            }

            _serviceNameMetadata =
            [
                Resolve(HttpClientComponent),
                Resolve(GrpcClientComponent),
            ];
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding arrays in <see cref="V0Values"/> and <see cref="V1Values"/>
        /// </summary>
        public enum Protocol
        {
            Http,
            Grpc
        }

        public enum Component
        {
            Http, // http-client
            Grpc // grpc-client
        }

        public string GetOperationNameForProtocol(Protocol protocol) => _protocols[(int)protocol];

        public string GetOperationNameSuffixForRequest() => _operationNameSuffix;

        public ServiceNameMetadata GetServiceNameMetadata(Component component) => _serviceNameMetadata[(int)component];

        public HttpTags CreateHttpTags()
            => _useV0Tags ? new HttpTags() : new HttpV1Tags();

        public GrpcClientTags CreateGrpcClientTags()
            => _useV0Tags ? new GrpcClientTags() : new GrpcClientV1Tags();

        public RemotingClientTags CreateRemotingClientTags()
            => _useV0Tags ? new RemotingClientTags() : new RemotingClientV1Tags();

        public ServiceRemotingClientTags CreateServiceRemotingClientTags()
            => _useV0Tags ? new ServiceRemotingClientTags() : new ServiceRemotingClientV1Tags();

        public AzureServiceBusTags CreateAzureServiceBusTags()
            => _useV0Tags ? new AzureServiceBusTags() : new AzureServiceBusV1Tags();

        private static class V0Values
        {
            public static readonly string[] ProtocolOperationNames =
            [
                "http.request",
                "grpc.request",
            ];
        }

        private static class V1Values
        {
            public static readonly string[] ProtocolOperationNames =
            [
                "http.client.request",
                "grpc.client.request",
            ];
        }
    }
}
