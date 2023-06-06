// <copyright file="MessagingSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class MessagingSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly NamingSchema _namingSchemaV0;
        private readonly NamingSchema _namingSchemaV1;

        public MessagingSchemaTests()
        {
            var mappings = new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            };

            _namingSchemaV0 = new NamingSchema(SchemaVersion.V0, DefaultServiceName, mappings);
            _namingSchemaV1 = new NamingSchema(SchemaVersion.V1, DefaultServiceName, mappings);
        }

        [Fact]
        public void GetInboundOperationNameIsCorrect()
        {
            var messagingSystem = "messaging";

            _namingSchemaV0.Messaging.GetInboundOperationName(messagingSystem).Should().Be($"{messagingSystem}.consume");
            _namingSchemaV1.Messaging.GetInboundOperationName(messagingSystem).Should().Be($"{messagingSystem}.process");
        }

        [Fact]
        public void GetOutboundOperationNameIsCorrect()
        {
            var messagingSystem = "messaging";

            _namingSchemaV0.Messaging.GetOutboundOperationName(messagingSystem).Should().Be($"{messagingSystem}.produce");
            _namingSchemaV1.Messaging.GetOutboundOperationName(messagingSystem).Should().Be($"{messagingSystem}.send");
        }

        [Theory]
        [InlineData("sql-server", "custom-db")]
        [InlineData("http-client", "some-service")]
        [InlineData("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            _namingSchemaV0.Messaging.GetInboundServiceName(serviceName).Should().Be(expected);
            _namingSchemaV1.Messaging.GetInboundServiceName(serviceName).Should().Be(expected);

            _namingSchemaV0.Messaging.GetOutboundServiceName(serviceName).Should().Be(expected);
            _namingSchemaV1.Messaging.GetOutboundServiceName(serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNames(string serviceName)
        {
            _namingSchemaV0.Messaging.GetInboundServiceName(serviceName).Should().Be($"{DefaultServiceName}-{serviceName}");
            _namingSchemaV1.Messaging.GetInboundServiceName(serviceName).Should().Be(DefaultServiceName);

            _namingSchemaV0.Messaging.GetOutboundServiceName(serviceName).Should().Be($"{DefaultServiceName}-{serviceName}");
            _namingSchemaV1.Messaging.GetOutboundServiceName(serviceName).Should().Be(DefaultServiceName);
        }
    }
}
