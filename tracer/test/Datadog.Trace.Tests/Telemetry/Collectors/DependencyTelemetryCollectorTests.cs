// <copyright file="DependencyTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class DependencyTelemetryCollectorTests
    {
        [Fact]
        public void HasChangesAfterAssemblyLoaded()
        {
            var collector = new DependencyTelemetryCollector();

            var data = collector.GetData();
            data.Should().BeNull();
            collector.HasChanges().Should().BeFalse();

            var assembly = typeof(DependencyTelemetryCollectorTests).Assembly;
            collector.AssemblyLoaded(assembly.GetName());

            collector.HasChanges().Should().BeTrue();

            data = collector.GetData();
            data.Should()
                .HaveCount(1)
                .And.ContainSingle(x => x.Name == "Datadog.Trace.Tests");
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void DoesNotHaveChangesWhenSameAssemblyAddedTwice()
        {
            var assembly = typeof(DependencyTelemetryCollectorTests).Assembly.GetName();
            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(assembly);

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.AssemblyLoaded(assembly);
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void HasChangesWhenAddingSameAssemblyWithDifferentVersion()
        {
            var assemblyV1 = CreateAssemblyName(new Version(1, 0));
            var assemblyV2 = CreateAssemblyName(new Version(2, 0));
            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(assemblyV1);

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.AssemblyLoaded(assemblyV2);
            collector.HasChanges().Should().BeTrue();
            var data = collector.GetData();
            data.Should().NotBeNull();
            data.Should()
                .NotBeNullOrEmpty()
                .And.HaveCount(2)
                .And.OnlyHaveUniqueItems();
        }

        private static AssemblyName CreateAssemblyName(Version version = null)
        {
            return new AssemblyName()
            {
                Name = "Datadog.Trace.Test.DynamicAssembly",
                Version = version,
            };
        }
    }
}
