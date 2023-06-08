// <copyright file="TracerSettingsSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Holds a snapshot of the mutable <see cref="TracerSettings"/> values after
/// initial creation. Used for tracking which properties have been changed in code
/// subsequently. Only properties which can be modified externally (both publicly, and
/// outside the mutable settings objects should be recorded. Mutation of settings
/// _inside_ <see cref="TracerSettings"/> et al should be explicitly tracked when
/// they are mutated.
/// </summary>
internal partial class TracerSettingsSnapshot : SettingsSnapshotBase
{
    private ExporterSettingsSnapshot Exporter { get; set; }

    private ImmutableIntegrationSettingsCollection Integrations { get; set; }

    private TimeSpan DirectLogSubmissionBatchPeriod { get; set; }

    [MemberNotNull(nameof(Exporter), nameof(Integrations), nameof(DirectLogSubmissionBatchPeriod))]
    partial void AdditionalInitialization(TracerSettings settings)
    {
        // Record the "extra" snapshot properties
        Exporter = new ExporterSettingsSnapshot(settings.ExporterInternal);
        // this is the easiest way to create a "snapshot" of these settings
        Integrations = new ImmutableIntegrationSettingsCollection(settings.IntegrationsInternal, EmptyHashSet);

        // Bit of a weird one
        DirectLogSubmissionBatchPeriod = settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod;
    }

    partial void RecordAdditionalChanges(TracerSettings settings, IConfigurationTelemetry telemetry)
    {
        RecordIfChanged(telemetry, ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, (int)DirectLogSubmissionBatchPeriod.TotalSeconds, (int)settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod.TotalSeconds);

        for (var i = settings.IntegrationsInternal.Settings.Length - 1; i >= 0; i--)
        {
            var newValue = settings.IntegrationsInternal.Settings[i];
            var oldValue = Integrations.Settings[i];
            var integrationName = newValue.IntegrationNameInternal;
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.Enabled, integrationName), oldValue.EnabledInternal, newValue.EnabledInternal);
#pragma warning disable 618 // App analytics is deprecated, but still used
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName), oldValue.AnalyticsEnabledInternal, newValue.AnalyticsEnabledInternal);
            RecordIfChanged(telemetry, string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName), oldValue.AnalyticsSampleRateInternal, newValue.AnalyticsSampleRateInternal);
#pragma warning restore 618
        }

        Exporter.RecordChanges(settings.ExporterInternal, telemetry);
    }
}
