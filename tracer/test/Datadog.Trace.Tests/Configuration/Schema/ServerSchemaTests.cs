// <copyright file="ServerSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class ServerSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";

        public static IEnumerable<(int SchemaVersion, int Protocol, string ExpectedOperationName)> GetProtocolOperationNameData()
        {
            yield return (0, (int)ServerSchema.Protocol.Grpc, "grpc.request");
            yield return (1, (int)ServerSchema.Protocol.Grpc, "grpc.server.request");
        }

        public static IEnumerable<(int SchemaVersion, int Component, string ExpectedOperationName)> GetComponentOperationNameData()
        {
            yield return (0, (int)ServerSchema.Component.Wcf, "wcf.request");
            yield return (1, (int)ServerSchema.Component.Wcf, "http.server.request");
        }

        public static IEnumerable<(int SchemaVersion, string ExpectedSuffix)> GetOperationNameSuffixForRequestData()
        {
            yield return (0, string.Empty);
            yield return (1, ".request");
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocolIsCorrect(
            [CombinatorialMemberData(nameof(GetProtocolOperationNameData))] (int SchemaVersion, int Protocol, string ExpectedOperationName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForProtocol((ServerSchema.Protocol)values.Protocol).Should().Be(values.ExpectedOperationName);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForComponentIsCorrect(
            [CombinatorialMemberData(nameof(GetComponentOperationNameData))] (int SchemaVersion, int Component, string ExpectedOperationName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForComponent((ServerSchema.Component)values.Component).Should().Be(values.ExpectedOperationName);
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
            namingSchema.Server.GetOperationNameSuffixForRequest().Should().Be(values.ExpectedSuffix);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocol_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(ServerSchema.Protocol)).Cast<ServerSchema.Protocol>())
            {
                namingSchema.Server.GetOperationNameForProtocol(value).Should().NotBeNull();
            }
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForComponent_SupportsAllEnumValues([CombinatorialValues(0, 1)]int schemaVersion, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var namingSchema = new NamingSchema((SchemaVersion)schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            foreach (var value in Enum.GetValues(typeof(ServerSchema.Component)).Cast<ServerSchema.Component>())
            {
                namingSchema.Server.GetOperationNameForComponent(value).Should().NotBeNull();
            }
        }
    }
}
