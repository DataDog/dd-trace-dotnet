// <copyright file="ImmutableTracerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Reflection;
using Datadog.Trace.Configuration;
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
            nameof(TracerSettings.DirectLogSubmissionEnabledIntegrations),
            nameof(TracerSettings.DirectLogSubmissionHost),
            nameof(TracerSettings.DirectLogSubmissionSource),
            nameof(TracerSettings.DirectLogSubmissionGlobalTags),
            nameof(TracerSettings.DirectLogSubmissionTransport),
            nameof(TracerSettings.DirectLogSubmissionUrl),
            nameof(TracerSettings.DirectLogSubmissionMinimumLevel),
        };

        [Fact]
        public void OnlyHasReadOnlyProperties()
        {
            var type = typeof(ImmutableTracerSettings);

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
        public void HasSamePropertiesAsTracerSettings()
        {
            var mutableProperties = typeof(TracerSettings)
                                   .GetProperties(Flags)
                                   .Select(x => x.Name)
                                   .Where(x => !ExcludedProperties.Contains(x));

            var immutableProperties = typeof(ImmutableTracerSettings)
                                     .GetProperties(Flags)
                                     .Select(x => x.Name);

            immutableProperties.Should().Contain(mutableProperties);
        }
    }
}
