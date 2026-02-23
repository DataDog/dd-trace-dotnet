// <copyright file="NamingSchemaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Configuration.Schema
{
#pragma warning disable SA1201 // A method should not follow a class
    public class NamingSchemaTests
    {
        private readonly Dictionary<string, string> _peerServiceMappings = new()
        {
            { "localhost", "AFancyServer" },
        };

        public class PeerServiceRemapData : IXunitSerializable
        {
            public PeerServiceRemapData()
            {
            }

            public PeerServiceRemapData(int schemaVersion, bool peerServiceTagsEnabled, string expectedPeerServiceValue)
            {
                SchemaVersion = schemaVersion;
                PeerServiceTagsEnabled = peerServiceTagsEnabled;
                ExpectedPeerServiceValue = expectedPeerServiceValue;
            }

            public int SchemaVersion { get; private set; }

            public bool PeerServiceTagsEnabled { get; private set; }

            public string ExpectedPeerServiceValue { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                SchemaVersion = info.GetValue<int>(nameof(SchemaVersion));
                PeerServiceTagsEnabled = info.GetValue<bool>(nameof(PeerServiceTagsEnabled));
                ExpectedPeerServiceValue = info.GetValue<string>(nameof(ExpectedPeerServiceValue));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(SchemaVersion), SchemaVersion);
                info.AddValue(nameof(PeerServiceTagsEnabled), PeerServiceTagsEnabled);
                info.AddValue(nameof(ExpectedPeerServiceValue), ExpectedPeerServiceValue);
            }
        }

        public static IEnumerable<PeerServiceRemapData> GetPeerServiceRemapData()
        {
            // When V1 or peerServiceTagsEnabled, should remap to the mapped value
            yield return new(1, true, "AFancyServer");
            yield return new(1, false, "AFancyServer");
            yield return new(0, true, "AFancyServer");
            // When V0 and peerServiceTagsEnabled is false, should not remap
            yield return new(0, false, "localhost");
        }

        [Theory]
        [CombinatorialData]
        public void SetMappedPeerServiceNames(
            [CombinatorialMemberData(nameof(GetPeerServiceRemapData))] PeerServiceRemapData values,
            bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)values.SchemaVersion;
            var namingSchema = new NamingSchema(schemaVersion, values.PeerServiceTagsEnabled, removeClientServiceNamesEnabled, "DefaultServiceName", new Dictionary<string, string>(), _peerServiceMappings);

            var tags = new TagsList();
            tags.SetTag(Tags.PeerService, "localhost");
            namingSchema.RemapPeerService(tags);
            tags.GetTag(Tags.PeerService).Should().Be(values.ExpectedPeerServiceValue);
        }

        [Fact]
        public void NoPeerServiceRemappingWhenNotConfigured()
        {
            var namingSchema = new NamingSchema(SchemaVersion.V1, true, false, "DefaultServiceName", new Dictionary<string, string>(), null);

            var tags = new TagsList();
            tags.SetTag(Tags.PeerService, "localhost");
            namingSchema.RemapPeerService(tags);
            tags.GetTag(Tags.PeerService).Should().Be("localhost");
        }

        [Fact]
        public void PeerServiceNotRemappedWhenNotSet()
        {
            var namingSchema = new NamingSchema(SchemaVersion.V1, true, false, "DefaultServiceName", new Dictionary<string, string>(), null);

            var tags = new TagsList();
            namingSchema.RemapPeerService(tags);
            tags.GetTag(Tags.PeerService).Should().BeNull();
        }
    }
#pragma warning restore SA1201 // A method should not follow a class
}
