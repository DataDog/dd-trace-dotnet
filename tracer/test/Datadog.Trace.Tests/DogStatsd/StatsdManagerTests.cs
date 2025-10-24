// <copyright file="StatsdManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DogStatsd;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DogStatsd;

public class StatsdManagerTests
{
    private static readonly TracerSettings TracerSettings = new();
    private static readonly MutableSettings PreviousMutable = MutableSettings.CreateForTesting(TracerSettings, []);
    private static readonly ExporterSettings PreviousExporter = new ExporterSettings(null);

    [Fact]
    public void HasImpactingChanges_WhenNoChanges()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: null,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeFalse();
    }

    [Fact]
    public void HasImpactingChanges_WhenNoChanges2()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: PreviousMutable,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeFalse();
    }

    [Fact]
    public void HasImpactingChanges_WhenExporterChanges()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: null,
            updatedExporter: PreviousExporter, // We don't check for "real" differences, assume all changes matter
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesEnv()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.Environment, "new" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesServiceName()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.ServiceName, "service" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesServiceVersion()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.ServiceVersion, "1.0.0" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesGlobalTags()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.GlobalTags, "a:b" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }
}
