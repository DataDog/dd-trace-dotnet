// <copyright file="ImmutableTracerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Configuration;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableTracerSettingsTests
    {
        [Fact]
        public void OnlyHasReadOnlyProperties()
        {
            var type = typeof(ImmutableTracerSettings);

            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            using var scope = new AssertionScope();

            var properties = type.GetProperties(flags);
            foreach (var propertyInfo in properties)
            {
                propertyInfo.CanWrite.Should().BeFalse($"{propertyInfo.Name} should be read only");
            }

            var fields = type.GetFields(flags);
            foreach (var field in fields)
            {
                field.IsInitOnly.Should().BeTrue($"{field.Name} should be read only");
            }
        }
    }
}
