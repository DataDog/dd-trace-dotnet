// <copyright file="ImmutableExporterSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableExporterSettingsTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        // These properties are present on ExporterSettings, but not on ImmutableExporterSettings
        private static readonly string[] ExcludedProperties =
        {
            // No exclusions yet
        };

        [Fact]
        public void OnlyHasReadOnlyProperties()
        {
            var type = typeof(ImmutableExporterSettings);

            using var scope = new AssertionScope();

            var properties = type.GetProperties(Flags);
            foreach (var propertyInfo in properties)
            {
                propertyInfo.CanWrite.Should().BeFalse($"{propertyInfo.Name} should be read only");
            }

            var fields = type.GetFields(Flags);
            foreach (var field in fields)
            {
                field.IsInitOnly.Should().BeTrue($"{field.Name} should be read only");
            }
        }

        [Fact]
        public void HasSamePropertiesAsExporterSettings()
        {
            var mutableProperties = typeof(ExporterSettings)
                                   .GetProperties(Flags)
                                   .Select(x => x.Name)
                                   .Where(x => !ExcludedProperties.Contains(x));

            var immutableProperties = typeof(ImmutableExporterSettings)
                                     .GetProperties(Flags)
                                     .Select(x => x.Name);

            immutableProperties.Should().Contain(mutableProperties);
        }

        [Fact]
        public void AllPropertyValuesMatch()
        {
            var equalityCheckers = new List<Func<ExporterSettings, ImmutableExporterSettings, bool>>()
            {
                (e, i) => e.MetricsPipeName == i.MetricsPipeName,
                (e, i) => e.TracesPipeName == i.TracesPipeName,
                (e, i) => e.DogStatsdPort == i.DogStatsdPort,
                (e, i) => e.MetricsTransport == i.MetricsTransport,
                (e, i) => e.TracesTransport == i.TracesTransport,
                (e, i) => e.TracesTimeout == i.TracesTimeout,
                (e, i) => e.TracesPipeTimeoutMs == i.TracesPipeTimeoutMs,
                (e, i) => e.AgentUri == i.AgentUri,
                (e, i) => e.PartialFlushEnabled == i.PartialFlushEnabled,
                (e, i) => e.PartialFlushMinSpans == i.PartialFlushMinSpans,
                (e, i) => e.MetricsUnixDomainSocketPath == i.MetricsUnixDomainSocketPath,
                (e, i) => e.TracesUnixDomainSocketPath == i.TracesUnixDomainSocketPath,
                (e, i) => e.ValidationWarnings.Count == i.ValidationWarnings.Count,
            };

            var mutableProperties = typeof(ExporterSettings)
                                   .GetProperties(Flags);

            // Ensure that all properties are represented
            Assert.Equal(mutableProperties.Count(), equalityCheckers.Count);

            var exporterSettings = new ExporterSettings();

            exporterSettings.AgentUri = new Uri("http://127.0.0.1:8282");
            exporterSettings.MetricsUnixDomainSocketPath = "metricsuds";
            exporterSettings.TracesUnixDomainSocketPath = "tracesuds";
            exporterSettings.MetricsPipeName = "metricspipe";
            exporterSettings.TracesPipeName = "tracespipe";
            exporterSettings.DogStatsdPort = 1234;
            exporterSettings.MetricsTransport = Vendors.StatsdClient.Transport.TransportType.NamedPipe;
            exporterSettings.TracesTransport = TracesTransportType.WindowsNamedPipe;
            exporterSettings.TracesTimeout = 7778;
            exporterSettings.TracesPipeTimeoutMs = 5556;

            var immutableSettings = new ImmutableExporterSettings(exporterSettings);

            foreach (var equalityCheck in equalityCheckers)
            {
                Assert.True(equalityCheck(exporterSettings, immutableSettings));
            }
        }
    }
}
