// <copyright file="ClientSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class ClientSchemaTests
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
        public void GetOperationNameForProtocolIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var protocol = "http";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => $"{protocol}.request",
                _ => $"{protocol}.client.request",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.GetOperationNameForProtocol(protocol).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetOperationNameForRequestTypeIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var requestType = "some-remoting.client";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => requestType,
                _ => $"{requestType}.request",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForRequestType(requestType).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesMappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());

            foreach (var kvp in _mappings)
            {
                namingSchema.Client.GetServiceName(kvp.Key).Should().Be(kvp.Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesUnmappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());

            foreach (var key in _unmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{DefaultServiceName}-{key}",
                    _ => DefaultServiceName,
                };

                namingSchema.Client.GetServiceName(key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateHttpTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(HttpTags),
                _ => typeof(HttpV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.CreateHttpTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateGrpcClientTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(GrpcClientTags),
                _ => typeof(GrpcClientV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.CreateGrpcClientTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateServiceRemotingClientTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(ServiceRemotingClientTags),
                _ => typeof(ServiceRemotingClientV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.CreateServiceRemotingClientTags().Should().BeOfType(expectedType);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CreateAzureServiceBusTagsReturnsCorrectImplementation(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var expectedType = schemaVersion switch
            {
                SchemaVersion.V0 when peerServiceTagsEnabled == false => typeof(AzureServiceBusTags),
                _ => typeof(AzureServiceBusV1Tags),
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.CreateAzureServiceBusTags().Should().BeOfType(expectedType);
        }
    }
}
