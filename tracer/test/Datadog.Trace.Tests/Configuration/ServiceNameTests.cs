// <copyright file="ServiceNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
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
        public void RetrievesMappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var tracer = new LockedTracer(
                new TracerSettings()
                {
                    MetadataSchemaVersion = schemaVersion,
                    PeerServiceTagsEnabled = peerServiceTagsEnabled,
                    RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled,
                    ServiceNameMappings = _mappings
                });

            foreach (var kvp in _mappings)
            {
                tracer.Settings.GetServiceName(tracer, kvp.Key).Should().Be(kvp.Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesUnmappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var tracer = new LockedTracer(
                new TracerSettings()
                {
                    MetadataSchemaVersion = schemaVersion,
                    PeerServiceTagsEnabled = peerServiceTagsEnabled,
                    RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled,
                    ServiceNameMappings = _mappings
                });

            foreach (var key in _unmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{tracer.DefaultServiceName}-{key}",
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == true => tracer.DefaultServiceName,
                    _ => tracer.DefaultServiceName,
                };

                tracer.Settings.GetServiceName(tracer, key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void DoesNotRequireAnyMappings(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var tracer = new LockedTracer(
                new TracerSettings()
                {
                    MetadataSchemaVersion = schemaVersion,
                    PeerServiceTagsEnabled = peerServiceTagsEnabled,
                    RemoveClientServiceNamesEnabled = removeClientServiceNamesEnabled,
                });

            foreach (var key in _unmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{tracer.DefaultServiceName}-{key}",
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == true => tracer.DefaultServiceName,
                    _ => tracer.DefaultServiceName,
                };

                tracer.Settings.GetServiceName(tracer, key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void CanAddMappingsViaConfigurationSource(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.MetadataSchemaVersion, schemaVersionObject.ToString().ToLower() },
                { ConfigurationKeys.PeerServiceDefaultsEnabled, peerServiceTagsEnabled.ToString() },
                { ConfigurationKeys.RemoveClientServiceNamesEnabled, removeClientServiceNamesEnabled.ToString() },
                { ConfigurationKeys.ServiceNameMappings, $"{serviceName}:{expected}" }
            };

            var tracer = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collection)));
            tracer.Settings.GetServiceName(tracer, serviceName).Should().Be(expected);
        }

        private class LockedTracer : Tracer
        {
            internal LockedTracer(TracerSettings tracerSettings)
                : base(new LockedTracerManager(tracerSettings))
            {
            }
        }

        private class LockedTracerManager : TracerManager, ILockedTracer
        {
            public LockedTracerManager(TracerSettings tracerSettings)
                : base(new ImmutableTracerSettings(tracerSettings), null, null, null, null, null, null, null, null, null, null, null, null)
            {
            }
        }
    }
}
