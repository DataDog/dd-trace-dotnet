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

namespace Datadog.Trace.Tests.Configuration.Schema
{
    public class NamingSchemaTests
    {
        private readonly Dictionary<string, string> _peerServiceMappings = new()
        {
            { "localhost", "AFancyServer" },
        };

        public static IEnumerable<(int SchemaVersion, bool PeerServiceTagsEnabled, string ExpectedPeerServiceValue)> GetPeerServiceRemapData()
        {
            // When V1 or peerServiceTagsEnabled, should remap to the mapped value
            yield return (1, true, "AFancyServer");
            yield return (1, false, "AFancyServer");
            yield return (0, true, "AFancyServer");
            // When V0 and peerServiceTagsEnabled is false, should not remap
            yield return (0, false, "localhost");
        }

        [Theory]
        [CombinatorialData]
        public void SetMappedPeerServiceNames(
            [CombinatorialMemberData(nameof(GetPeerServiceRemapData))] (int SchemaVersion, bool PeerServiceTagsEnabled, string ExpectedPeerServiceValue) values,
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
}
