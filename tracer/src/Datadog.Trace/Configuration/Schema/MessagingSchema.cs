// <copyright file="MessagingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema
{
    internal class MessagingSchema
    {
        private readonly SchemaVersion _version;

        public MessagingSchema(SchemaVersion version)
        {
            _version = version;
        }

        public string GetInboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => $"{messagingSystem}.process",
                _ => $"{messagingSystem}.consume",
            };

        public string GetInboundServiceName(string applicationName, string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => applicationName,
                _ => $"{applicationName}-{messagingSystem}",
            };

        public string GetOutboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => $"{messagingSystem}.send",
                _ => $"{messagingSystem}.produce",
            };

        public string GetOutboundServiceName(string applicationName, string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => applicationName,
                _ => $"{applicationName}-{messagingSystem}",
            };
    }
}
