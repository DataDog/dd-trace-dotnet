// <copyright file="MessagingSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
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
        private readonly string[] _unmappedKeys = { "elasticsearch", "postgres", "custom-service" };
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "sql-server", "custom-db" },
            { "http-client", "some-service" },
            { "mongodb", "my-mongo" },
        };

        public static IEnumerable<object[]> GetAllConfigs()
            => from schemaVersion in new object[] { SchemaVersion.V0, SchemaVersion.V1 }
               from peerServiceTagsEnabled in new[] { true, false }
               from removeClientServiceNamesEnabled in new[] { true, false }
               select new[] { schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled };

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetInboundOperationNameIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject;
            var messagingSystem = "messaging";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => $"{messagingSystem}.consume",
                _ => $"{messagingSystem}.process",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings);
            namingSchema.Messaging.GetInboundOperationName(messagingSystem).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetOutboundOperationNameIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var messagingSystem = "messaging";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => $"{messagingSystem}.produce",
                _ => $"{messagingSystem}.send",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings);
            namingSchema.Messaging.GetOutboundOperationName(messagingSystem).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesMappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings);

            foreach (var kvp in _mappings)
            {
                namingSchema.Messaging.GetInboundServiceName(kvp.Key).Should().Be(kvp.Value);
                namingSchema.Messaging.GetOutboundServiceName(kvp.Key).Should().Be(kvp.Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesUnmappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings);

            foreach (var key in _unmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{DefaultServiceName}-{key}",
                    _ => DefaultServiceName,
                };

                namingSchema.Messaging.GetInboundServiceName(key).Should().Be(expectedServiceName);
                namingSchema.Messaging.GetOutboundServiceName(key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateKafkaTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(KafkaTags),
                _ => typeof(KafkaV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings);
            namingSchema.Messaging.CreateKafkaTags("spanKind").Should().BeOfType(expectedType);
        }
    }
}
