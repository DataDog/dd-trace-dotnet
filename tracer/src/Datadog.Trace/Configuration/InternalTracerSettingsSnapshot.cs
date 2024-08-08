// <copyright file="InternalTracerSettingsSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Internal.Configuration;

namespace Datadog.Trace.Internal.Configuration;

/// <summary>
/// Holds a snapshot of the mutable <see cref="InternalTracerSettings"/> values after
/// initial creation. Used for tracking which properties have been changed in code
/// subsequently. Only properties which can be modified externally (both publicly, and
/// outside the mutable settings objects should be recorded. Mutation of settings
/// _inside_ <see cref="InternalTracerSettings"/> et al should be explicitly tracked when
/// they are mutated.
/// </summary>
internal partial class InternalTracerSettingsSnapshot : InternalSettingsSnapshotBase
{
    private InternalExporterSettingsSnapshot Exporter { get; set; }

    private TimeSpan DirectLogSubmissionBatchPeriod { get; set; }

    [MemberNotNull(nameof(Exporter), nameof(DirectLogSubmissionBatchPeriod))]
    partial void AdditionalInitialization(InternalTracerSettings settings)
    {
        // Record the "extra" snapshot properties
        Exporter = new InternalExporterSettingsSnapshot(settings.ExporterInternal);

        // Bit of a weird one
        DirectLogSubmissionBatchPeriod = settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod;
    }

    partial void RecordAdditionalChanges(InternalTracerSettings settings, IConfigurationTelemetry telemetry)
    {
        RecordIfChanged(telemetry, ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, (int)DirectLogSubmissionBatchPeriod.TotalSeconds, (int)settings.LogSubmissionSettings.DirectLogSubmissionBatchPeriod.TotalSeconds);

        Exporter.RecordChanges(settings.ExporterInternal, telemetry);
    }
}
