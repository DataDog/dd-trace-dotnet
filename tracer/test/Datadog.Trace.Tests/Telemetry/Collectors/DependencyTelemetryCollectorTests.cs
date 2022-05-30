// <copyright file="DependencyTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
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
            collector.AssemblyLoaded(assembly);

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
            var assembly = typeof(DependencyTelemetryCollectorTests).Assembly;
            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(assembly);

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.AssemblyLoaded(assembly);
            collector.HasChanges().Should().BeFalse();
        }

        [Theory]
        [InlineData("DuckTypeAssembly.SomeTest")]
        [InlineData("App_global.asax.zt8edv4m")]
        [InlineData("App_Web_login.cshtml.6331810a.tvsbhzc3")]
        [InlineData("App_GlobalResources.9ccedwue")]
        [InlineData("App_Code.hhzpytyv")]
        [InlineData("App_Theme_Standard.6wkna0wf")]
        [InlineData("App_WebReferences.dvkaf7ph")]
        public void DoesNotHaveChangesWhenAssemblyNameIsIgnoredAssembly(string assemblyName)
        {
            var ignoredName = CreateAssemblyName(new Version(1, 0), name: assemblyName);

            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(ignoredName);

            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void DoesNotHaveChangesWhenAssemblyNameIsTempPath()
        {
            for (var i = 0; i < 1_000; i++)
            {
                var name = Path.GetRandomFileName();
                var ignoredName = CreateAssemblyName(new Version(1, 0), name: name);

                var collector = new DependencyTelemetryCollector();
                collector.AssemblyLoaded(ignoredName);

                collector.HasChanges().Should().BeFalse($"{name} is a temp file name");
            }
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

        private static AssemblyName CreateAssemblyName(Version version = null, string name = null)
        {
            return new AssemblyName()
            {
                Name = name ?? "Datadog.Trace.Test.DynamicAssembly",
                Version = version,
            };
        }
    }
}
