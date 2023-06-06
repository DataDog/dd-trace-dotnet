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
    private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    private const BindingFlags AllVisibilityFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    // These properties are present on TracerSettings, but don't need to be recorded in the snapshot
    private static readonly string[] ExcludedTracerSettingsProperties =
    {
        nameof(TracerSettings.DiagnosticSourceEnabled),
    };

    private static readonly string[] ExcludedExporterSettingsProperties =
    {
        nameof(TracerSettings.DiagnosticSourceEnabled),
    };

    [Fact]
    public void OnlyHasReadOnlyProperties()
    {
        var type = typeof(TracerSettingsSnapshot);

        using var scope = new AssertionScope();

        var properties = type.GetProperties(AllVisibilityFlags);
        foreach (var propertyInfo in properties)
        {
            propertyInfo.CanWrite.Should().BeFalse($"{propertyInfo.Name} should be read only");
        }

        var fields = type.GetFields(AllVisibilityFlags);
        foreach (var field in fields)
        {
            field.IsInitOnly.Should().BeTrue($"{field.Name} should be read only");
        }
    }

    [Fact]
    public void RecordsMutablePropertiesOnTracerSettings()
    {
        var settingsProperties = typeof(TracerSettings)
                                      .GetProperties(PublicFlags)
                                      .Where(
                                           x => !ExcludedTracerSettingsProperties.Contains(x.Name)
                                             && !x.HasAttribute<GeneratePublicApiAttribute>());

        // We only record settings that are public, which can be mutated by classes
        // external to TracerSettings, or which are collections
        var externallyMutable = settingsProperties
                               .Where(
                                    x => (x.SetMethod is { } method && (method.IsPublic || method.IsAssembly))
                                      || x.PropertyType.IsAssignableTo(typeof(ICollection)))
                               .Select(x => x.Name);

        var snapshotProperties = typeof(TracerSettingsSnapshot)
                                 .GetProperties(AllVisibilityFlags)
                                 .Select(x => x.Name);

        snapshotProperties.Should().Contain(externallyMutable);
    }

    [Fact]
    public void RecordsMutablePropertiesOnExporterSettings()
    {
        var settingsProperties = typeof(ExporterSettings)
                                      .GetProperties(PublicFlags)
                                      .Where(
                                           x => !ExcludedExporterSettingsProperties.Contains(x.Name)
                                             && !x.HasAttribute<GeneratePublicApiAttribute>());

        // We only record settings that are public, which can be mutated by classes
        // external to ExporterSettings, or which are collections
        var externallyMutable = settingsProperties
                               .Where(
                                    x => (x.SetMethod is { } method && (method.IsPublic || method.IsAssembly))
                                      || x.PropertyType.IsAssignableTo(typeof(ICollection)))
                               .Select(x => x.Name);

        var snapshotProperties = typeof(TracerSettingsSnapshot.ExporterSettingsSnapshot)
                                 .GetProperties(AllVisibilityFlags)
                                 .Select(x => x.Name);

        snapshotProperties.Should().Contain(externallyMutable);
    }

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
        settings.Exporter.AgentUri = new Uri("http://localhost:1234");

        snapshot.RecordChanges(settings, collector);
        collector.GetData().Should().ContainSingle(x => x.Name == ConfigurationKeys.AgentUri);
    }

    [Fact]
    public void Snapshot_RecordsUpdatesToIntegrationSetting()
    {
        var collector = new ConfigurationTelemetry();
        var settings = new TracerSettings(NullConfigurationSource.Instance);
        var snapshot = new TracerSettingsSnapshot(settings);
        settings.Integrations[nameof(IntegrationId.Grpc)].Enabled = false;

        snapshot.RecordChanges(settings, collector);
        var configKey = string.Format(ConfigurationKeys.Integrations.Enabled, nameof(IntegrationId.Grpc));
        collector.GetData().Should().ContainSingle(x => x.Name == configKey);
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
