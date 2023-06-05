// <copyright file="ClientSchemaTests.cs" company="Datadog">
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
    public class ClientSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";
        private readonly NamingSchema _namingSchemaV0;
        private readonly NamingSchema _namingSchemaV1;

        public ClientSchemaTests()
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
        public void GetOperationNameForProtocolIsCorrect()
        {
            var protocol = "http";

            _namingSchemaV0.Client.GetOperationNameForProtocol(protocol).Should().Be($"{protocol}.request");
            _namingSchemaV1.Client.GetOperationNameForProtocol(protocol).Should().Be($"{protocol}.client.request");
        }

        [Theory]
        [InlineData("sql-server", "custom-db")]
        [InlineData("http-client", "some-service")]
        [InlineData("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            _namingSchemaV0.Client.GetServiceName(serviceName).Should().Be(expected);
            _namingSchemaV1.Client.GetServiceName(serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNames(string serviceName)
        {
            _namingSchemaV0.Client.GetServiceName(serviceName).Should().Be($"{DefaultServiceName}-{serviceName}");
            _namingSchemaV1.Client.GetServiceName(serviceName).Should().Be(DefaultServiceName);
        }
    }
}
