// <copyright file="ServerSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema
{
    internal sealed class ServerSchema
    {
        private readonly string[] _protocolOperationNames;
        private readonly string[] _componentOperationNames;
        private readonly string _operationNameSuffix;

        public ServerSchema(SchemaVersion version)
        {
            _protocolOperationNames = version switch
            {
                SchemaVersion.V0 => V0Values.ProtocolOperationNames,
                _ => V1Values.ProtocolOperationNames,
            };

            _componentOperationNames = version switch
            {
                SchemaVersion.V0 => V0Values.ComponentOperationNames,
                _ => V1Values.ComponentOperationNames,
            };

            _operationNameSuffix = version switch
            {
                SchemaVersion.V0 => string.Empty,
                _ => ".request",
            };
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding arrays in <see cref="V0Values"/> and <see cref="V1Values"/>
        /// </summary>
        public enum Protocol
        {
            Grpc,
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding arrays in <see cref="V0Values"/> and <see cref="V1Values"/>
        /// </summary>
        public enum Component
        {
            Wcf,
        }

        public string GetOperationNameForProtocol(Protocol protocol) => _protocolOperationNames[(int)protocol];

        public string GetOperationNameForComponent(Component component) => _componentOperationNames[(int)component];

        public string GetOperationNameSuffixForRequest() => _operationNameSuffix;

        private static class V0Values
        {
            public static readonly string[] ProtocolOperationNames = ["grpc.request"];

            public static readonly string[] ComponentOperationNames = ["wcf.request"];
        }

        private static class V1Values
        {
            public static readonly string[] ProtocolOperationNames = ["grpc.server.request"];

            public static readonly string[] ComponentOperationNames = ["http.server.request"];
        }
    }
}
