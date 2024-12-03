// <copyright file="MessagingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal class MessagingSchema
    {
        private readonly SchemaVersion _version;
        private readonly bool _peerServiceTagsEnabled;
        private readonly bool _removeClientServiceNamesEnabled;
        private readonly string _defaultServiceName;
        private readonly IReadOnlyDictionary<string, string>? _serviceNameMappings;

        public MessagingSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;
            _removeClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            _defaultServiceName = defaultServiceName;
            _serviceNameMappings = serviceNameMappings;
        }

        public string GetInboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V0 => $"{messagingSystem}.consume",
                _ => $"{messagingSystem}.process",
            };

        public string GetServiceName(string messagingSystem)
        {
            if (_serviceNameMappings is not null && _serviceNameMappings.TryGetValue(messagingSystem, out var mappedServiceName))
            {
                return mappedServiceName;
            }

            return _version switch
            {
                SchemaVersion.V0 when !_removeClientServiceNamesEnabled => $"{_defaultServiceName}-{messagingSystem}",
                _ => _defaultServiceName,
            };
        }

        public string GetOutboundCommandOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V0 => $"{messagingSystem}.command",
                _ => $"{messagingSystem}.send",
            };

        public string GetOutboundOperationName(string messagingSystem)
            => _version switch
            {
                SchemaVersion.V0 => $"{messagingSystem}.produce",
                _ => $"{messagingSystem}.send",
            };

        public KafkaTags CreateKafkaTags(string spanKind)
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new KafkaTags(spanKind),
                _ => new KafkaV1Tags(spanKind),
            };

        public IbmMqTags CreateIbmMqTags(string spanKind) => new(spanKind);

        public MsmqTags CreateMsmqTags(string spanKind)
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new MsmqTags(spanKind),
                _ => new MsmqV1Tags(spanKind),
            };

        public AwsSqsTags CreateAwsSqsTags(string spanKind) => _version switch
        {
            SchemaVersion.V0 when !_peerServiceTagsEnabled => new AwsSqsTags(),
            _ => new AwsSqsV1Tags(spanKind),
        };

        public AwsSnsTags CreateAwsSnsTags(string spanKind) => _version switch
        {
            SchemaVersion.V0 when !_peerServiceTagsEnabled => new AwsSnsTags(),
            _ => new AwsSnsV1Tags(spanKind),
        };

        public AwsEventBridgeTags CreateAwsEventBridgeTags(string spanKind) => _version switch
        {
            SchemaVersion.V0 when !_peerServiceTagsEnabled => new AwsEventBridgeTags(),
            _ => new AwsEventBridgeV1Tags(spanKind),
        };

        public AwsKinesisTags CreateAwsKinesisTags(string spanKind) => _version switch
        {
            SchemaVersion.V0 when !_peerServiceTagsEnabled => new AwsKinesisTags(spanKind),
            _ => new AwsKinesisV1Tags(spanKind),
        };

        public RabbitMQTags CreateRabbitMqTags(string spanKind)
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new RabbitMQTags(spanKind),
                _ => new RabbitMQV1Tags(spanKind),
            };
    }
}
