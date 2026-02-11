// <copyright file="ServerSchemaTests.cs" company="Datadog">
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
    public class ServerSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";

        public static IEnumerable<(int SchemaVersion, string Protocol, string ExpectedOperationName)> GetProtocolOperationNameData()
        {
            yield return (0, "grpc", "grpc.request");
            yield return (1, "grpc", "grpc.server.request");
        }

        public static IEnumerable<(int SchemaVersion, string Component, string ExpectedOperationName)> GetComponentOperationNameData()
        {
            yield return (0, "wcf", "wcf.request");
            yield return (1, "wcf", "http.server.request");
        }

        public static IEnumerable<(int SchemaVersion, string ExpectedSuffix)> GetOperationNameSuffixForRequestData()
        {
            yield return (0, string.Empty);
            yield return (1, ".request");
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocolIsCorrect(
            [CombinatorialMemberData(nameof(GetProtocolOperationNameData))] (int SchemaVersion, string Protocol, string ExpectedOperationName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForProtocol(values.Protocol).Should().Be(values.ExpectedOperationName);
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForComponentIsCorrect(
            [CombinatorialMemberData(nameof(GetComponentOperationNameData))] (int SchemaVersion, string Component, string ExpectedOperationName) values,
            bool peerServiceTagsEnabled,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, DefaultServiceName, new Dictionary<string, string>(), new Dictionary<string, string>());
            namingSchema.Server.GetOperationNameForComponent(values.Component).Should().Be(values.ExpectedOperationName);
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
    }
}
