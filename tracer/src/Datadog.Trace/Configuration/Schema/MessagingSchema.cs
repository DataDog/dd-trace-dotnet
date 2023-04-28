// <copyright file="MessagingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Configuration.Schema
{
    internal class MessagingSchema
    {
        private readonly SchemaVersion _version;
        private readonly string _defaultServiceName;
        private readonly IDictionary<string, string>? _serviceNameMappings;

        public MessagingSchema(SchemaVersion version, string defaultServiceName, IDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _defaultServiceName = defaultServiceName;
            _serviceNameMappings = serviceNameMappings;
        }

        public string GetInboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => $"{messagingSystem}.process",
                _ => $"{messagingSystem}.consume",
            };

        public string GetInboundServiceName(string messagingSystem)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(messagingSystem, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V1 => _defaultServiceName,
                _ => $"{_defaultServiceName}-{messagingSystem}",
            };
        }

        public string GetOutboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V1 => $"{messagingSystem}.send",
                _ => $"{messagingSystem}.produce",
            };

        public string GetOutboundServiceName(string messagingSystem)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(messagingSystem, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V1 => _defaultServiceName,
                _ => $"{_defaultServiceName}-{messagingSystem}",
            };
        }
    }
}
