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
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Configuration.Schema
{
#pragma warning disable SA1201 // A method should not follow a class
    public class ServerSchemaTests
    {
        private const string DefaultServiceName = "MyApplication";

        public class ProtocolOperationNameData : IXunitSerializable
        {
            public ProtocolOperationNameData()
            {
            }

            public ProtocolOperationNameData(int schemaVersion, int protocol, string expectedOperationName)
            {
                SchemaVersion = schemaVersion;
                Protocol = protocol;
                ExpectedOperationName = expectedOperationName;
            }

            public int SchemaVersion { get; private set; }

            public int Protocol { get; private set; }

            public string ExpectedOperationName { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                Protocol = info.GetValue<int>(nameof(Protocol));
                ExpectedOperationName = info.GetValue<string>(nameof(ExpectedOperationName));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(Protocol), Protocol);
                info.AddValue(nameof(ExpectedOperationName), ExpectedOperationName);
            }
        }

        public class ComponentOperationNameData : IXunitSerializable
        {
            public ComponentOperationNameData()
            {
            }

            public ComponentOperationNameData(int schemaVersion, int component, string expectedOperationName)
            {
                SchemaVersion = schemaVersion;
                Component = component;
                ExpectedOperationName = expectedOperationName;
            }

            public int SchemaVersion { get; private set; }

            public int Component { get; private set; }

            public string ExpectedOperationName { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                Component = info.GetValue<int>(nameof(Component));
                ExpectedOperationName = info.GetValue<string>(nameof(ExpectedOperationName));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(Component), Component);
                info.AddValue(nameof(ExpectedOperationName), ExpectedOperationName);
            }
        }

        public class OperationNameSuffixData : IXunitSerializable
        {
            public OperationNameSuffixData()
            {
            }

            public OperationNameSuffixData(int schemaVersion, string expectedSuffix)
            {
                SchemaVersion = schemaVersion;
                ExpectedSuffix = expectedSuffix;
            }

            public int SchemaVersion { get; private set; }

            public string ExpectedSuffix { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                ExpectedSuffix = info.GetValue<string>(nameof(ExpectedSuffix));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(ExpectedSuffix), ExpectedSuffix);
            }
        }

        public static IEnumerable<ProtocolOperationNameData> GetProtocolOperationNameData()
        {
            yield return new(0, (int)ServerSchema.Protocol.Grpc, "grpc.request");
            yield return new(1, (int)ServerSchema.Protocol.Grpc, "grpc.server.request");
        }

        public static IEnumerable<ComponentOperationNameData> GetComponentOperationNameData()
        {
            yield return new(0, (int)ServerSchema.Component.Wcf, "wcf.request");
            yield return new(1, (int)ServerSchema.Component.Wcf, "http.server.request");
        }

        public static IEnumerable<OperationNameSuffixData> GetOperationNameSuffixForRequestData()
        {
            yield return new(0, string.Empty);
            yield return new(1, ".request");
        }

        [Theory]
        [CombinatorialData]
        public void GetOperationNameForProtocolIsCorrect(
            [CombinatorialMemberData(nameof(GetProtocolOperationNameData))] ProtocolOperationNameData values,
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
            [CombinatorialMemberData(nameof(GetComponentOperationNameData))] ComponentOperationNameData values,
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
            [CombinatorialMemberData(nameof(GetOperationNameSuffixForRequestData))] OperationNameSuffixData values,
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
#pragma warning restore SA1201 // A method should not follow a class
}
