// <copyright file="MessagingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Configuration.Schema
{
    internal sealed class MessagingSchema
    {
        private readonly SchemaVersion _version;
        private readonly bool _peerServiceTagsEnabled;
        private readonly string[] _inboundOperationNames;
        private readonly string[] _outboundOperationNames;
        private readonly string[] _serviceNames;

        public MessagingSchema(SchemaVersion version, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled, string defaultServiceName, IReadOnlyDictionary<string, string>? serviceNameMappings)
        {
            _version = version;
            _peerServiceTagsEnabled = peerServiceTagsEnabled;

            _inboundOperationNames = version switch
            {
                SchemaVersion.V0 => V0Values.InboundOperationNames,
                _ => V1Values.InboundOperationNames,
            };

            _outboundOperationNames = version switch
            {
                SchemaVersion.V0 => V0Values.OutboundOperationNames,
                _ => V1Values.OutboundOperationNames,
            };

            // Calculate service names once, to avoid allocations with every call
            var useSuffix = version == SchemaVersion.V0 && !removeClientServiceNamesEnabled;
            _serviceNames =
            [
                useSuffix ? $"{defaultServiceName}-aws.eventbridge" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-aws.kinesis" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-aws.sns" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-aws.sqs" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-aws.stepfunctions" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-azureeventhubs" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-azureservicebus" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-ibmmq" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-kafka" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-msmq" : defaultServiceName,
                useSuffix ? $"{defaultServiceName}-rabbitmq" : defaultServiceName,
            ];

            if (serviceNameMappings is not null)
            {
                TryApplyMapping(serviceNameMappings, "aws.eventbridge", ServiceType.AwsEventBridge);
                TryApplyMapping(serviceNameMappings, "aws.kinesis", ServiceType.AwsKinesis);
                TryApplyMapping(serviceNameMappings, "aws.sns", ServiceType.AwsSns);
                TryApplyMapping(serviceNameMappings, "aws.sqs", ServiceType.AwsSqs);
                TryApplyMapping(serviceNameMappings, "aws.stepfunctions", ServiceType.AwsStepFunctions);
                TryApplyMapping(serviceNameMappings, "azureeventhubs", ServiceType.AzureEventHubs);
                TryApplyMapping(serviceNameMappings, "azureservicebus", ServiceType.AzureServiceBus);
                TryApplyMapping(serviceNameMappings, "ibmmq", ServiceType.IbmMq);
                TryApplyMapping(serviceNameMappings, "kafka", ServiceType.Kafka);
                TryApplyMapping(serviceNameMappings, "msmq", ServiceType.Msmq);
                TryApplyMapping(serviceNameMappings, "rabbitmq", ServiceType.RabbitMq);
            }

            void TryApplyMapping(IReadOnlyDictionary<string, string> mappings, string key, ServiceType system)
            {
                if (mappings.TryGetValue(key, out var mappedName))
                {
                    _serviceNames[(int)system] = mappedName;
                }
            }
        }

        /// <summary>
        /// WARNING: when adding new values, you _must_ update the corresponding arrays in <see cref="V0Values"/> and <see cref="V1Values"/>
        /// and update the service name initialization in the constructor.
        /// </summary>
        public enum OperationType
        {
            Amqp,
            AwsEventBridge,
            AwsKinesis,
            AwsSns,
            AwsSqs,
            AwsStepFunctions,
            AzureEventHubs,
            AzureServiceBus,
            IbmMq,
            Kafka,
            Msmq,
        }

        public enum ServiceType
        {
            AwsEventBridge,
            AwsKinesis,
            AwsSns,
            AwsSqs,
            AwsStepFunctions,
            AzureEventHubs,
            AzureServiceBus,
            IbmMq,
            Kafka,
            Msmq,
            RabbitMq,
        }

        public string GetInboundOperationName(OperationType operationType) => _inboundOperationNames[(int)operationType];

        public string GetOutboundOperationName(OperationType operationType) => _outboundOperationNames[(int)operationType];

        public string GetServiceName(ServiceType messagingSystem) => _serviceNames[(int)messagingSystem];

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

        public AwsS3Tags CreateAwsS3Tags(string spanKind) => new AwsS3Tags(spanKind);

        public AwsSqsTags CreateAwsSqsTags(string spanKind) => new AwsSqsTags(spanKind);

        public AwsSnsTags CreateAwsSnsTags(string spanKind) => new AwsSnsTags(spanKind);

        public AwsEventBridgeTags CreateAwsEventBridgeTags(string spanKind) => new AwsEventBridgeTags(spanKind);

        public AwsKinesisTags CreateAwsKinesisTags(string spanKind) => new AwsKinesisTags(spanKind);

        public AwsStepFunctionsTags CreateAwsStepFunctionsTags(string spanKind) => new AwsStepFunctionsTags(spanKind);

        public RabbitMQTags CreateRabbitMqTags(string spanKind)
            => _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new RabbitMQTags(spanKind),
                _ => new RabbitMQV1Tags(spanKind),
            };

        public AzureServiceBusTags CreateAzureServiceBusTags(string spanKind)
        {
            var tags = _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new AzureServiceBusTags(),
                _ => new AzureServiceBusV1Tags(),
            };
            tags.SpanKind = spanKind;
            return tags;
        }

        public AzureEventHubsTags CreateAzureEventHubsTags(string spanKind)
        {
            var tags = _version switch
            {
                SchemaVersion.V0 when !_peerServiceTagsEnabled => new AzureEventHubsTags(spanKind),
                _ => new AzureEventHubsV1Tags(spanKind),
            };
            return tags;
        }

        private static class V0Values
        {
            public static readonly string[] InboundOperationNames =
            [
                "amqp.consume",
                "aws.eventbridge.consume",
                "aws.kinesis.consume",
                "aws.sns.consume",
                "aws.sqs.consume",
                "aws.stepfunctions.consume",
                "azureeventhubs.consume",
                "azureservicebus.consume",
                "ibmmq.consume",
                "kafka.consume",
                "msmq.consume",
            ];

            public static readonly string[] OutboundOperationNames =
            [
                "amqp.produce",
                "aws.eventbridge.produce",
                "aws.kinesis.produce",
                "aws.sns.produce",
                "aws.sqs.produce",
                "aws.stepfunctions.produce",
                "azureeventhubs.produce",
                "azureservicebus.produce",
                "ibmmq.produce",
                "kafka.produce",
                "msmq.produce",
            ];
        }

        private static class V1Values
        {
            public static readonly string[] InboundOperationNames =
            [
                "amqp.process",
                "aws.eventbridge.process",
                "aws.kinesis.process",
                "aws.sns.process",
                "aws.sqs.process",
                "aws.stepfunctions.process",
                "azureeventhubs.process",
                "azureservicebus.process",
                "ibmmq.process",
                "kafka.process",
                "msmq.process",
            ];

            public static readonly string[] OutboundOperationNames =
            [
                "amqp.send",
                "aws.eventbridge.send",
                "aws.kinesis.send",
                "aws.sns.send",
                "aws.sqs.send",
                "aws.stepfunctions.send",
                "azureeventhubs.send",
                "azureservicebus.send",
                "ibmmq.send",
                "kafka.send",
                "msmq.send",
            ];
        }
    }
}
