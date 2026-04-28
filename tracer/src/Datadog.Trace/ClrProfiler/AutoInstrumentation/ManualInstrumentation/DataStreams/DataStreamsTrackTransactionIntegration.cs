// <copyright file="DataStreamsTrackTransactionIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;

/// <summary>
/// System.Void Datadog.Trace.DataStreams::TrackTransactionInternal(System.String,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.DataStreams",
    MethodName = "TrackTransactionInternal",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String, ClrNames.String],
    MinimumVersion = "3.43.0",
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DataStreamsTrackTransactionIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string transactionId, string checkpointName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.DataStreams_TrackTransaction);

        var tracer = Datadog.Trace.Tracer.Instance;
        var manager = tracer.TracerManager.DataStreamsManager;
        var activeSpan = tracer.InternalActiveScope?.Span;

        if (activeSpan is not null)
        {
            activeSpan.TrackTransaction(manager, transactionId, checkpointName);
        }
        else
        {
            manager.TrackTransaction(transactionId, checkpointName);
        }

        return CallTargetState.GetDefault();
    }
}
