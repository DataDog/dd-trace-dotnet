// <copyright file="DependencyTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Parsing;
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
            collector.HasChanges().Should().BeTrue();

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.AssemblyLoaded(assembly);
            collector.HasChanges().Should().BeFalse();
        }

        [Theory]
        [InlineData("App_global.asax.zt8edv4m")]
        [InlineData("App_Web_login.cshtml.6331810a.tvsbhzc3")]
        [InlineData("App_GlobalResources.9ccedwue")]
        [InlineData("App_Code.hhzpytyv")]
        [InlineData("App_Theme_Standard.6wkna0wf")]
        [InlineData("App_WebReferences.dvkaf7ph")]
        [InlineData("0018eae6-bd49-41a4-9bd2-6be3a6544a15")]
        [InlineData("005ec706-91d7-4237-9466-bac51a64d90f")]
        [InlineData("00821386-7d9a-499b-8e7f-53dbbefcaf3d")]
        [InlineData("ℛ*00093a17-a657-432d-ad25-13cf53f44319#2-0")]
        [InlineData("ℛ*71ccc5b6-6f30-4c09-9e23-4e7ac5a9ad31#13-0")]
        [InlineData("ℛ*1887feb5-1546-46da-a64e-07cba2cb32fa#112-0")]
        [InlineData("ℛ*bcd9d48c-2728-46f5-bd56-bfb58cb0bb22#1156-0")]
        [InlineData("OK_IM_NO-GUID-BUUT-NOT_-THAT_FAR_OFF")]
        public void DoesNotHaveChangesWhenAssemblyNameIsIgnoredAssembly(string assemblyName)
        {
            var ignoredName = CreateAssemblyName(new Version(1, 0), name: assemblyName);

            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(ignoredName);

            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void DoesNotHaveChangesWhenUsingDuckTypeAssembly()
        {
            // create a random proxy (this needs to succeed, but can be anything)
            var original = new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Debug,
                exception: null,
                new MessageTemplate("Some text", Enumerable.Empty<MessageTemplateToken>()),
                Enumerable.Empty<LogEventProperty>());
            var proxy = original.DuckCast<ILogEvent>();

            var assembly = proxy.GetType().Assembly;

            var collector = new DependencyTelemetryCollector();
            collector.AssemblyLoaded(assembly);

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
                .And.HaveCount(1) // as we send to the backend only new versions
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
