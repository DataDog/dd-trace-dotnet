// <copyright file="ImmutableTracerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Reflection;
using AgileObjects.NetStandardPolyfills;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableTracerSettingsTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        // These properties are present on TracerSettings, but not on ImmutableTracerSettings
        private static readonly string[] ExcludedProperties =
        {
            nameof(TracerSettings.DisabledIntegrationNames),
            nameof(TracerSettings.DiagnosticSourceEnabled),
            nameof(TracerSettings.ProfilingEnabledInternal)
        };

        [Fact]
        public void OnlyHasReadOnlyProperties()
        {
            var type = typeof(ImmutableTracerSettings);

            using var scope = new AssertionScope();

            var properties = type.GetProperties(Flags);
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.CanWrite)
                {
                    propertyInfo.SetMethod!.ReturnParameter!.GetRequiredCustomModifiers()
                       .Should()
                       .ContainSingle(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit", $"{propertyInfo.Name} should be read only or init only");
                }
            }

            var fields = type.GetFields(Flags);
            foreach (var field in fields)
            {
                field.IsInitOnly.Should().BeTrue($"{field.Name} should be read only");
            }
        }

        [Fact]
        public void HasSamePropertiesAsTracerSettings()
        {
            var mutableProperties = typeof(TracerSettings)
                                   .GetProperties(Flags)
                                   .Where(x => !x.HasAttribute<GeneratePublicApiAttribute>())
                                   .Select(x => x.Name)
                                   .Where(x => !ExcludedProperties.Contains(x));

            var immutableProperties = typeof(ImmutableTracerSettings)
                                     .GetProperties(Flags)
                                     .Select(x => x.Name);

            immutableProperties.Should().Contain(mutableProperties);
        }

        [Fact]
        public void CopiesTelemetryFromTracerSettings()
        {
            var config = new ConfigurationTelemetry();
            var tracerSettings = new TracerSettings(NullConfigurationSource.Instance, config);

            var immutable = tracerSettings.Build();
            var immutableTelemetry = immutable.Telemetry;

            // Basic check that all the telemetry in the settings are _also_ in the immutable settings
            // The immutable tracer settings may add additional config (e.g. the disabled integrations telemetry)
            immutableTelemetry.Should()
                              .BeOfType<ConfigurationTelemetry>()
                              .Which
                              .GetQueueForTesting()
                              .Should()
                              .NotBeEmpty()
                              .And.ContainInOrder(config.GetQueueForTesting());
        }

        [Fact]
        public void DoesntInvokeBuiltInToStringMethod()
        {
            var settings = new ImmutableTracerSettings(NullConfigurationSource.Instance);
            var result = settings.ToString();
            result.Should().Be(typeof(ImmutableTracerSettings).FullName);
        }

        [Fact]
        public void RecordsDisabledSettingsInTelemetry()
        {
            var source = new NameValueConfigurationSource(new()
            {
                { "DD_TRACE_FOO_ENABLED", "true" },
                { "DD_TRACE_FOO_ANALYTICS_ENABLED", "true" },
                { "DD_TRACE_FOO_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_TRACE_BAR_ENABLED", "false" },
                { "DD_TRACE_BAR_ANALYTICS_ENABLED", "false" },
                { "DD_BAZ_ENABLED", "false" },
                { "DD_BAZ_ANALYTICS_ENABLED", "false" },
                { "DD_BAZ_ANALYTICS_SAMPLE_RATE", "0.6" },
                { "DD_TRACE_Kafka_ENABLED", "true" },
                { "DD_TRACE_Kafka_ANALYTICS_ENABLED", "true" },
                { "DD_TRACE_Kafka_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_TRACE_GraphQL_ENABLED", "false" },
                { "DD_TRACE_GraphQL_ANALYTICS_ENABLED", "false" },
                { "DD_Wcf_ENABLED", "false" },
                { "DD_Wcf_ANALYTICS_ENABLED", "false" },
                { "DD_Wcf_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_Msmq_ENABLED", "true" },
                { "DD_TRACE_stackexchangeredis_ENABLED", "false" },
                { ConfigurationKeys.DisabledIntegrations, "foobar;MongoDb;Msmq" },
            });

            var expected = new[] { "MongoDb", "Msmq", "GraphQL", "Wcf", "StackExchangeRedis" };

            var telemetry = new ConfigurationTelemetry();
            var tracerSettings = new TracerSettings(source, telemetry);
            var immutable = tracerSettings.Build();

            var config = immutable
                        .Telemetry
                        .Should()
                        .BeOfType<ConfigurationTelemetry>()
                        .Subject;

            var entry = config.GetQueueForTesting()
                              .Where(x => x.Key == ConfigurationKeys.DisabledIntegrations)
                              .OrderByDescending(x => x.SeqId)
                              .Should()
                              .HaveCountGreaterThan(0)
                              .And.Subject.First();

            entry.Key.Should().Be(ConfigurationKeys.DisabledIntegrations);
            entry.StringValue.Should().NotBeNullOrEmpty();
            entry.StringValue!.Split(';')
                  .Should()
                  .Contain(expected);
        }
    }
}
