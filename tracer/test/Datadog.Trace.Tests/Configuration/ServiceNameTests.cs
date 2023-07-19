// <copyright file="ServiceNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
        private static readonly string[] UnmappedKeys = { "elasticsearch", "postgres", "custom-service" };
        private static readonly Dictionary<string, string> Mappings = new()
        {
            { "sql-server", "custom-db" },
            { "http-client", "some-service" },
            { "mongodb", "my-mongo" },
        };

        private static readonly string MappingsString = string.Join(",", Mappings.ToList().Select(kvp => $"{kvp.Key}:{kvp.Value}"));

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
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.MetadataSchemaVersion, schemaVersionObject.ToString().ToLower() },
                { ConfigurationKeys.PeerServiceDefaultsEnabled, peerServiceTagsEnabled.ToString() },
                { ConfigurationKeys.RemoveClientServiceNamesEnabled, removeClientServiceNamesEnabled.ToString() },
                { ConfigurationKeys.ServiceNameMappings, MappingsString },
            };

            var tracer = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collection)));

            foreach (var kvp in Mappings)
            {
                tracer.CurrentTraceSettings.GetServiceName(tracer, kvp.Key).Should().Be(kvp.Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void RetrievesUnmappedServiceNames(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.MetadataSchemaVersion, schemaVersionObject.ToString().ToLower() },
                { ConfigurationKeys.PeerServiceDefaultsEnabled, peerServiceTagsEnabled.ToString() },
                { ConfigurationKeys.RemoveClientServiceNamesEnabled, removeClientServiceNamesEnabled.ToString() },
                { ConfigurationKeys.ServiceNameMappings, MappingsString },
            };

            var tracer = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collection)));

            foreach (var key in UnmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{tracer.DefaultServiceName}-{key}",
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == true => tracer.DefaultServiceName,
                    _ => tracer.DefaultServiceName,
                };

                tracer.CurrentTraceSettings.GetServiceName(tracer, key).Should().Be(expectedServiceName);
            }
        }

        [Theory]
        [MemberData(nameof(GetAllConfigs))]
        public void DoesNotRequireAnyMappings(object schemaVersionObject, bool peerServiceTagsEnabled, bool removeClientServiceNamesEnabled)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject; // Unbox SchemaVersion, which is only defined internally
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.MetadataSchemaVersion, schemaVersionObject.ToString().ToLower() },
                { ConfigurationKeys.PeerServiceDefaultsEnabled, peerServiceTagsEnabled.ToString() },
                { ConfigurationKeys.RemoveClientServiceNamesEnabled, removeClientServiceNamesEnabled.ToString() },
            };

            var tracer = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collection)));

            foreach (var key in UnmappedKeys)
            {
                var expectedServiceName = schemaVersion switch
                {
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == false => $"{tracer.DefaultServiceName}-{key}",
                    SchemaVersion.V0 when removeClientServiceNamesEnabled == true => tracer.DefaultServiceName,
                    _ => tracer.DefaultServiceName,
                };

                tracer.CurrentTraceSettings.GetServiceName(tracer, key).Should().Be(expectedServiceName);
            }
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
                : base(new ImmutableTracerSettings(tracerSettings), null, Mock.Of<IScopeManager>(), null, null, null, null, null, null, null, null, null, null, null, null, null)
            {
            }
        }
    }
}
