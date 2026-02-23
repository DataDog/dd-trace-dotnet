// <copyright file="MessagingSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Configuration.Schema
{
#pragma warning disable SA1201 // A method should not follow a class
    public class MessagingSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "kafka", "custom-kafka" },
            { "rabbitmq", "my-rabbitmq" },
        };

        public class OperationNameData : IXunitSerializable
        {
            public OperationNameData()
            {
            }

            public OperationNameData(int schemaVersion, int operationType, string expectedInboundOpName, string expectedOutboundOpName)
            {
                SchemaVersion = schemaVersion;
                OperationType = operationType;
                ExpectedInboundOpName = expectedInboundOpName;
                ExpectedOutboundOpName = expectedOutboundOpName;
            }

            public int SchemaVersion { get; private set; }

            public int OperationType { get; private set; }

            public string ExpectedInboundOpName { get; private set; }

            public string ExpectedOutboundOpName { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                OperationType = info.GetValue<int>(nameof(OperationType));
                ExpectedInboundOpName = info.GetValue<string>(nameof(ExpectedInboundOpName));
                ExpectedOutboundOpName = info.GetValue<string>(nameof(ExpectedOutboundOpName));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(OperationType), OperationType);
                info.AddValue(nameof(ExpectedInboundOpName), ExpectedInboundOpName);
                info.AddValue(nameof(ExpectedOutboundOpName), ExpectedOutboundOpName);
            }
        }

        public class ServiceNameData : IXunitSerializable
        {
            public ServiceNameData()
            {
            }

            public ServiceNameData(int schemaVersion, int serviceType, string expectedValue, bool removeClientServiceNamesEnabled)
            {
                SchemaVersion = schemaVersion;
                ServiceType = serviceType;
                ExpectedValue = expectedValue;
                RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled;
            }

            public int SchemaVersion { get; private set; }

            public int ServiceType { get; private set; }

            public string ExpectedValue { get; private set; }

            public bool RemoveClientServiceNamesEnabled { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                ServiceType = info.GetValue<int>(nameof(ServiceType));
                ExpectedValue = info.GetValue<string>(nameof(ExpectedValue));
                RemoveClientServiceNamesEnabled = info.GetValue<bool>(nameof(RemoveClientServiceNamesEnabled));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(ServiceType), ServiceType);
                info.AddValue(nameof(ExpectedValue), ExpectedValue);
                info.AddValue(nameof(RemoveClientServiceNamesEnabled), RemoveClientServiceNamesEnabled);
            }
        }

        public static IEnumerable<OperationNameData> GetOperationNameData()
        {
            yield return new(0, (int)MessagingSchema.OperationType.Kafka, "kafka.consume", "kafka.produce");
            yield return new(0, (int)MessagingSchema.OperationType.IbmMq, "ibmmq.consume", "ibmmq.produce");
            yield return new(0, (int)MessagingSchema.OperationType.AwsSqs, "aws.sqs.consume", "aws.sqs.produce");
            yield return new(1, (int)MessagingSchema.OperationType.Kafka, "kafka.process", "kafka.send");
            yield return new(1, (int)MessagingSchema.OperationType.IbmMq, "ibmmq.process", "ibmmq.send");
            yield return new(1, (int)MessagingSchema.OperationType.AwsSqs, "aws.sqs.process", "aws.sqs.send");
        }

        public static IEnumerable<ServiceNameData> GetServiceNameData()
        {
            // Mapped service names (always return mapped value)
            yield return new(0, (int)MessagingSchema.ServiceType.Kafka, "custom-kafka", true);
            yield return new(0, (int)MessagingSchema.ServiceType.Kafka, "custom-kafka", false);
            yield return new(1, (int)MessagingSchema.ServiceType.Kafka, "custom-kafka", true);
            yield return new(1, (int)MessagingSchema.ServiceType.Kafka, "custom-kafka", false);
            // Unmapped service names
            yield return new(0, (int)MessagingSchema.ServiceType.AwsSqs, DefaultServiceName, true);
            yield return new(0, (int)MessagingSchema.ServiceType.AwsSqs, $"{DefaultServiceName}-aws.sqs", false);
            yield return new(1, (int)MessagingSchema.ServiceType.AwsSqs, DefaultServiceName, true);
            yield return new(1, (int)MessagingSchema.ServiceType.AwsSqs, DefaultServiceName, false);
        }

        [Theory]
        [CombinatorialData]
        public void GetInboundOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] OperationNameData values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetInboundOperationName((MessagingSchema.OperationType)values.OperationType).Should().Be(values.ExpectedInboundOpName);
        }

        [Theory]
        [CombinatorialData]
        public void GetOutboundOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] OperationNameData values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetOutboundOperationName((MessagingSchema.OperationType)values.OperationType).Should().Be(values.ExpectedOutboundOpName);
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceNameIsCorrect(
            [CombinatorialMemberData(nameof(GetServiceNameData))] ServiceNameData values,
            bool peerServiceTagsEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, values.RemoveClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetServiceName((MessagingSchema.ServiceType)values.ServiceType).Should().Be(values.ExpectedValue);
        }

        [Theory]
        [CombinatorialData]
        public void CreateKafkaTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(KafkaTags),
                _ => typeof(KafkaV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Messaging.CreateKafkaTags("spanKind").Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateMsmqTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(MsmqTags),
                _ => typeof(MsmqV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Messaging.CreateMsmqTags("spanKind").Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateRabbitMqTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(RabbitMQTags),
                _ => typeof(RabbitMQV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Messaging.CreateRabbitMqTags("spanKind").Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateAzureServiceBusTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(AzureServiceBusTags),
                _ => typeof(AzureServiceBusV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Messaging.CreateAzureServiceBusTags("spanKind").Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void CreateAzureEventHubsTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)] int schemaVersionInt, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionInt;
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(AzureEventHubsTags),
                _ => typeof(AzureEventHubsV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, peerServiceNameMappings: new Dictionary<string, string>());
            namingSchema.Messaging.CreateAzureEventHubsTags("spanKind").Should().BeOfType(expectedType);
        }

        [Theory]
        [CombinatorialData]
        public void GetInboundOperationName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(MessagingSchema.OperationType)).Cast<MessagingSchema.OperationType>())
            {
                namingSchema.Messaging.GetInboundOperationName(value).Should().NotBeNull();
            }
        }

        [Theory]
        [CombinatorialData]
        public void GetOutboundOperationName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(MessagingSchema.OperationType)).Cast<MessagingSchema.OperationType>())
            {
                namingSchema.Messaging.GetOutboundOperationName(value).Should().NotBeNull();
            }
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(MessagingSchema.ServiceType)).Cast<MessagingSchema.ServiceType>())
            {
                namingSchema.Messaging.GetServiceName(value).Should().NotBeNull();
            }
        }
    }
#pragma warning restore SA1201 // A method should not follow a class
}
