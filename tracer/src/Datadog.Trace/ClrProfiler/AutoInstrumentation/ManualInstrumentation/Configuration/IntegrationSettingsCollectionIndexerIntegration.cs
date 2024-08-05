// <copyright file="IntegrationSettingsCollectionIndexerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration;

/// <summary>
/// Datadog.Trace.Configuration.IntegrationSettings Datadog.Trace.Configuration.IntegrationSettingsCollection::get_Item(System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Configuration.IntegrationSettingsCollection",
    MethodName = "get_Item",
    ReturnTypeName = "Datadog.Trace.Configuration.IntegrationSettings",
    ParameterTypeNames = [ClrNames.String],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class IntegrationSettingsCollectionIndexerIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string integrationName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettingsCollection_Indexer_Name);
        return CallTargetState.GetDefault();
    }
}
