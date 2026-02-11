// <copyright file="ClientSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        private readonly Dictionary<string, string> _mappings = new()
        {
            { "sql-server", "custom-db" },
            { "http-client", "some-service" },
            { "mongodb", "my-mongo" },
        };

        public static IEnumerable<(int SchemaVersion, int Protocol, string ExpectedValue)> GetOperationNameForProtocolData()
        {
            yield return (0, (int)ClientSchema.Protocol.Http, "http.request");         // SchemaVersion.V0
            yield return (0, (int)ClientSchema.Protocol.Grpc, "grpc.request");
            yield return (1, (int)ClientSchema.Protocol.Http, "http.client.request");  // SchemaVersion.V1
            yield return (1, (int)ClientSchema.Protocol.Grpc, "grpc.client.request");
        }

        public static IEnumerable<(int SchemaVersion, int Component, string ExpectedValue, bool RemoveClientServiceNamesEnabled)> GetServiceNameData()
        {
            yield return (0, (int)ClientSchema.Component.Http, "some-service", true);   // V0, mapped
            yield return (0, (int)ClientSchema.Component.Http, "some-service", false);
            yield return (1, (int)ClientSchema.Component.Http, "some-service", true);   // V1, mapped
            yield return (1, (int)ClientSchema.Component.Http, "some-service", false);
            yield return (0, (int)ClientSchema.Component.Grpc, DefaultServiceName, true);   // V0, unmapped
            yield return (0, (int)ClientSchema.Component.Grpc, $"{DefaultServiceName}-grpc-client", false);
            yield return (1, (int)ClientSchema.Component.Grpc, DefaultServiceName, true);   // V1, unmapped
            yield return (1, (int)ClientSchema.Component.Grpc, DefaultServiceName, false);
        }

        public static IEnumerable<(int SchemaVersion, string ExpectedSuffix)> GetOperationNameSuffixForRequestData()
        {
            yield return (0, string.Empty);           // SchemaVersion.V0
            yield return (0,  string.Empty);
            yield return (1, ".request");   // SchemaVersion.V1
            yield return (1, ".request");
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocolIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameForProtocolData))] (int SchemaVersion, int Protocol, string ExpectedValue) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.GetOperationNameForProtocol((ClientSchema.Protocol)values.Protocol).Should().Be(values.ExpectedValue);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameSuffixForRequestIsCorrect(
            [CombinatorialMemberData(nameof(GetOperationNameSuffixForRequestData))] (int SchemaVersion, string ExpectedSuffix) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Client.GetOperationNameSuffixForRequest().Should().Be(values.ExpectedSuffix);
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceNameIsCorrect(
            [CombinatorialMemberData(nameof(GetServiceNameData))] (int SchemaVersion, int Component, string ExpectedValue, bool RemoveClientServiceNamesEnabled) values,
            bool peerServiceTagsEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, values.RemoveClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            namingSchema.Client.GetServiceName((ClientSchema.Component)values.Component).Should().Be(values.ExpectedValue);
        }

        [Theory]
        [CombinatorialData]
        public void CreateHttpTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)]int schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
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
        [CombinatorialData]
        public void CreateGrpcClientTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)]int schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
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
        [CombinatorialData]
        public void CreateServiceRemotingClientTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)]int schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
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
        [CombinatorialData]
        public void CreateAzureServiceBusTagsReturnsCorrectImplementation([CombinatorialValues(0, 1)]int schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
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

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocol_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(ClientSchema.Protocol)).Cast<ClientSchema.Protocol>())
            {
                namingSchema.Client.GetOperationNameForProtocol(value).Should().NotBeNull();
            }
        }

        [Theory]
        [CombinatorialData]
        public void GetServiceName_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, _mappings, new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(ClientSchema.Component)).Cast<ClientSchema.Component>())
            {
                namingSchema.Client.GetServiceName(value).Should().NotBeNull();
            }
        }
    }
}
