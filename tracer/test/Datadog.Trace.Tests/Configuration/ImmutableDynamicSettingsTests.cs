// <copyright file="ImmutableDynamicSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableDynamicSettingsTests
    {
        [Fact]
        public void Equality()
        {
            var properties = typeof(ImmutableDynamicSettings).GetProperties();

            properties.Should().NotBeEmpty();

            foreach (var property in properties)
            {
                var settings1 = new ImmutableDynamicSettings();
                property.SetValue(settings1, GetSampleValues(property.PropertyType).Value1);

                var settings2 = new ImmutableDynamicSettings();
                property.SetValue(settings2, GetSampleValues(property.PropertyType).Value1);

                var settings3 = new ImmutableDynamicSettings();
                property.SetValue(settings3, GetSampleValues(property.PropertyType).Value2);

                settings1.Equals(settings2).Should().BeTrue();
                settings1.Equals(settings3).Should().BeFalse();
            }
        }

        private (object Value1, object Value2) GetSampleValues(Type type)
        {
            if (type == typeof(string))
            {
                return ("a", "b");
            }

            if (type == typeof(double?))
            {
                return (0.1d, 0.2d);
            }

            if (type == typeof(bool?))
            {
                return (true, false);
            }

            if (type == typeof(IReadOnlyDictionary<string, string>))
            {
                return (new Dictionary<string, string> { { "a", "1" } }, new Dictionary<string, string> { { "b", "2" } });
            }

            throw new ArgumentOutOfRangeException(nameof(type), type, "Unexpected type");
        }
    }
}
