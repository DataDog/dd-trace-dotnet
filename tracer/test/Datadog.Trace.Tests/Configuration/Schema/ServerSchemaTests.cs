// <copyright file="ServerSchemaTests.cs" company="Datadog">
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
    public class ServerSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";

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
            var protocol = "some-rpc";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => $"{protocol}.request",
                _ => $"{protocol}.server.request",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForProtocol(protocol).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetOperationNameForComponentIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var component = "some-http-server";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => $"{component}.request",
                _ => "http.server.request",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForComponent(component).Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void GetOperationNameForRequestTypeIsCorrect(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var requestType = "some-remoting.server";
            var expectedValue = schemaVersion switch
            {
                SchemaVersion.V0 => requestType,
                _ => $"{requestType}.request",
            };

            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForRequestType(requestType).Should().Be(expectedValue);
        }
    }
}
