// <copyright file="MessagingSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class MessagingSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "kafka", "custom-kafka" },
            { "rabbitmq", "my-rabbitmq" },
        };

        public static IEnumerable<(int SchemaVersion, string MessagingSystem, string ExpectedInboundOpName, string ExpectedOutboundOpName)> GetOperationNameData()
        {
            yield return (0, "kafka", "kafka.consume", "kafka.produce");
            yield return (0, "ibmmq", "ibmmq.consume", "ibmmq.produce");
            yield return (0, "aws.sqs", "aws.sqs.consume", "aws.sqs.produce");
            yield return (1, "kafka", "kafka.process", "kafka.send");
            yield return (1, "ibmmq", "ibmmq.process", "ibmmq.send");
            yield return (1, "aws.sqs", "aws.sqs.process", "aws.sqs.send");
        }

        public static IEnumerable<(int SchemaVersion, string MessagingSystem, string ExpectedValue, bool RemoveClientServiceNamesEnabled)> GetServiceNameData()
        {
            // Mapped service names (always return mapped value)
            yield return (0, "kafka", "custom-kafka", true);
            yield return (0, "kafka", "custom-kafka", false);
            yield return (1, "kafka", "custom-kafka", true);
            yield return (1, "kafka", "custom-kafka", false);
            // Unmapped service names
            yield return (0, "aws.sqs", DefaultServiceName, true);
            yield return (0, "aws.sqs", $"{DefaultServiceName}-aws.sqs", false);
            yield return (1, "aws.sqs", DefaultServiceName, true);
            yield return (1, "aws.sqs", DefaultServiceName, false);
        }

        [Theory]
        [CombinatorialData]
        public void GetInboundOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] (int SchemaVersion, string MessagingSystem, string ExpectedInboundOpName, string ExpectedOutboundOpName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetInboundOperationName(values.MessagingSystem).Should().Be(values.ExpectedInboundOpName);
        }

        [Theory]
        [CombinatorialData]
        public void GetOutboundOperationNameIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameData))] (int SchemaVersion, string MessagingSystem, string ExpectedInboundOpName, string ExpectedOutboundOpName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetOutboundOperationName(values.MessagingSystem).Should().Be(values.ExpectedOutboundOpName);
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceNameIsCorrect(
            [CombinatorialMemberData(nameof(GetServiceNameData))] (int SchemaVersion, string MessagingSystem, string ExpectedValue, bool RemoveClientServiceNamesEnabled) values,
            bool peerServiceTagsEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, values.RemoveClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Messaging.GetServiceName(values.MessagingSystem).Should().Be(values.ExpectedValue);
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
    }
}
