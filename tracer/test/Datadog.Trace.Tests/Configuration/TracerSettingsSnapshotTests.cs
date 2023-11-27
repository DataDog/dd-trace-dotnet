// <copyright file="TracerSettingsSnapshotTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgileObjects.NetStandardPolyfills;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class TracerSettingsSnapshotTests
{
    [Fact]
    public void Snapshot_HasNoChangesWhenNoChanges()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().BeNullOrEmpty();
    }

    [Fact]
    public void Snapshot_RecordsBasicSettingChange()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.ServiceName = "New service";

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.ServiceName);
    }

    [Fact]
    public void Snapshot_RecordsChangeToHashSet()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.DisabledIntegrationNames.Add("Testing");

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.DisabledIntegrations);
    }

    [Fact]
    public void Snapshot_RecordsReplacementOfHashSet()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.DisabledIntegrationNames = new HashSet<string> { "Testing" };

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.DisabledIntegrations);
    }

    [Fact]
    public void Snapshot_RecordsUpdatesToExporterSettings()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.Exporter.DogStatsdPort = 1234;

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.DogStatsdPort);
    }

    [Fact]
    public void Snapshot_RecordsUpdatesToLogSettings()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod = TimeSpan.FromSeconds(1);

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds);
    }
}
