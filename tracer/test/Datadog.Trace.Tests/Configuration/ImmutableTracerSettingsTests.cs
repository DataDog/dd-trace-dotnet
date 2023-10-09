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

            // Just basic check that we have the same number of config values
            immutableTelemetry.Should()
                              .BeOfType<ConfigurationTelemetry>()
                              .Which
                              .GetQueueForTesting()
                              .Count.Should()
                              .Be(config.GetQueueForTesting().Count)
                              .And.NotBe(0);
        }

        [Fact]
        public void DoesntInvokeBuiltInToStringMethod()
        {
            var settings = new ImmutableTracerSettings(NullConfigurationSource.Instance);
            var result = settings.ToString();
            result.Should().Be(typeof(ImmutableTracerSettings).FullName);
        }
    }
}
