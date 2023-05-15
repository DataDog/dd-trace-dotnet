// <copyright file="IntegrationTelemetryCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Telemetry
{
    public class IntegrationTelemetryCollectorTests
    {
        private const IntegrationId IntegrationId = Trace.Configuration.IntegrationId.Kafka;
        private static readonly string IntegrationName = IntegrationRegistry.GetName(IntegrationId);

        [Fact]
        public void HasChangesWhenNewIntegrationRunning()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationRunning(IntegrationId);
            collector.HasChanges().Should().BeTrue();
        }

        [Fact]
        public void DoesNotHaveChangesWhenSameIntegrationRunning()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationRunning(IntegrationId);
            collector.HasChanges().Should().BeTrue();
            collector.GetData();

            collector.IntegrationRunning(IntegrationId);
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void HasChangesWhenNewIntegrationGeneratedSpan()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationGeneratedSpan(IntegrationId);
            collector.HasChanges().Should().BeTrue();
        }

        [Fact]
        public void DoesNotHaveChangesWhenSameIntegrationGeneratedSpan()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationGeneratedSpan(IntegrationId);
            collector.HasChanges().Should().BeTrue();
            collector.GetData();

            collector.IntegrationGeneratedSpan(IntegrationId);
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void HasChangesWhenNewIntegrationDisabled()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationDisabledDueToError(IntegrationId, "Testing!");
            collector.HasChanges().Should().BeTrue();
        }

        [Fact]
        public void DoesNotHaveChangesWhenSameIntegrationDisabled()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.GetData();
            collector.HasChanges().Should().BeFalse();

            collector.IntegrationDisabledDueToError(IntegrationId, "Testing");
            collector.HasChanges().Should().BeTrue();
            collector.GetData();

            collector.IntegrationDisabledDueToError(IntegrationId, "Another error");
            collector.HasChanges().Should().BeFalse();
        }

        [Fact]
        public void WhenIntegrationRunsSuccessfullyHasExpectedValues()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.IntegrationRunning(IntegrationId);
            collector.IntegrationGeneratedSpan(IntegrationId);

            var data = collector.GetData();
            var integration = data.FirstOrDefault(x => x.Name == IntegrationName);
            integration.Should().NotBeNull();
            integration.AutoEnabled.Should().BeTrue();
            integration.Enabled.Should().BeTrue();
            integration.Error.Should().BeNull();
        }

        [Fact]
        public void WhenIntegrationRunsButDoesNotGenerateSpanHasExpectedValues()
        {
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.IntegrationRunning(IntegrationId);

            var data = collector.GetData();
            var integration = data.FirstOrDefault(x => x.Name == IntegrationName);
            integration.Should().NotBeNull();
            integration.AutoEnabled.Should().BeTrue();
            integration.Enabled.Should().BeFalse();
            integration.Error.Should().BeNull();
        }

        [Fact]
        public void WhenIntegrationErrorsHasExpectedValues()
        {
            const string error = "Some error";
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.IntegrationRunning(IntegrationId);
            collector.IntegrationDisabledDueToError(IntegrationId, error);

            var data = collector.GetData();
            var integration = data.FirstOrDefault(x => x.Name == IntegrationName);
            integration.Should().NotBeNull();
            integration.AutoEnabled.Should().BeTrue();
            integration.Enabled.Should().BeFalse();
            integration.Error.Should().Be(error);
        }

        [Fact]
        public void WhenIntegrationRunsThenErrorsHasExpectedValues()
        {
            const string error = "Some error";
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            collector.IntegrationRunning(IntegrationId);
            collector.IntegrationGeneratedSpan(IntegrationId);
            collector.IntegrationDisabledDueToError(IntegrationId, error);

            var data = collector.GetData();
            var integration = data.FirstOrDefault(x => x.Name == IntegrationName);
            integration.Should().NotBeNull();
            integration.AutoEnabled.Should().BeTrue();
            integration.Enabled.Should().BeTrue();
            integration.Error.Should().Be(error);
        }

        [Fact]
        public void OnlyIncludesChangedValues()
        {
            const string error = "Some error";
            var collector = new IntegrationTelemetryCollector();
            collector.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()));

            // first call
            collector.GetData().Should().NotBeEmpty();
            // second call
            collector.GetData().Should().BeNullOrEmpty();

            // Make change
            collector.IntegrationRunning(IntegrationId);

            collector.HasChanges().Should().BeTrue();
            collector.GetData().Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = IntegrationName });

            // Make identical change
            collector.IntegrationRunning(IntegrationId);
            collector.GetData().Should().BeNullOrEmpty();

            // new change
            collector.IntegrationGeneratedSpan(IntegrationId);
            collector.GetData().Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = IntegrationName });
            collector.GetData().Should().BeNullOrEmpty();

            // new change
            collector.IntegrationDisabledDueToError(IntegrationId, error);
            collector.GetData().Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = IntegrationName });
            collector.GetData().Should().BeNullOrEmpty();
        }
    }
}
