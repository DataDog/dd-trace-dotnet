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
        private readonly string[] _serviceNames;
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

            // Calculate service names once, to avoid allocations with every call
            var useSuffix = version == SchemaVersion.V0 && !removeClientServiceNamesEnabled;
            _serviceNames =
            [
                useSuffix ? $"{defaultServiceName}-{HttpClientComponent}" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-{GrpcClientComponent}" : defaultServiceName,
            ];
            if (serviceNameMappings is not null)
            {
                if (serviceNameMappings.TryGetValue(HttpClientComponent, out var httpName))
                {
                    _serviceNames[(int)Component.Http] = httpName;
                }

                if (serviceNameMappings.TryGetValue(GrpcClientComponent, out var grpcName))
                {
                    _serviceNames[(int)Component.Grpc] = grpcName;
                }
            }
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

        public string GetServiceName(Component component) => _serviceNames[(int)component];

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
