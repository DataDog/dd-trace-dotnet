// <copyright file="NamingSchemaTests.cs" company="Datadog">
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
    public class NamingSchemaTests
    {
        private readonly Dictionary<string, string> _peerServiceMappings = new()
        {
            { "localhost", "AFancyServer" },
        };

        public static IEnumerable<object[]> GetAllConfigs()
            => from schemaVersion in new object[] { SchemaVersion.V0, SchemaVersion.V1 }
               from peerServiceTagsEnabled in new[] { true, false }
               from removeClientServiceNamesEnabled in new[] { true, false }
               select new[] { schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled };

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void SetMappedPeerServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var namingSchema = new NamingSchema(schemaVersion, peerServiceTagsEnabled, removeClientServiceNamesEnabled, "DefaultServiceName", new Dictionary<string, string>(), _peerServiceMappings);

            var tags = new CommonTags();
            tags.SetTag(Tags.PeerService, "localhost");
            namingSchema.RemapPeerService(tags);
            if (schemaVersion == SchemaVersion.V1 || peerServiceTagsEnabled)
            {
                tags.GetTag(Tags.PeerService).Should().Be("AFancyServer");
            }
            else
            {
                tags.GetTag(Tags.PeerService).Should().Be("localhost");
            }
        }

        [Fact]
        public void NoPeerServiceRemappingWhenNotConfigured()
        {
            var namingSchema = new NamingSchema(SchemaVersion.V1, true, false, "DefaultServiceName", new Dictionary<string, string>(), null);

            var tags = new CommonTags();
            tags.SetTag(Tags.PeerService, "localhost");
            namingSchema.RemapPeerService(tags);
            tags.GetTag(Tags.PeerService).Should().Be("localhost");
        }

        [Fact]
        public void PeerServiceNotRemappedWhenNotSet()
        {
            var namingSchema = new NamingSchema(SchemaVersion.V1, true, false, "DefaultServiceName", new Dictionary<string, string>(), null);

            var tags = new CommonTags();
            namingSchema.RemapPeerService(tags);
            tags.GetTag(Tags.PeerService).Should().BeNull();
        }
    }
}
