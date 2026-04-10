// <copyright file="DataStreamsTrackTransactionIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;

/// <summary>
/// System.Void Datadog.Trace.DataStreams::TrackTransaction(Datadog.Trace.ISpan,System.String,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.DataStreams",
    MethodName = "TrackTransaction",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Datadog.Trace.ISpan", ClrNames.String, ClrNames.String],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DataStreamsTrackTransactionIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpan>(ref TSpan span, string transactionId, string checkpointName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.DataStreams_TrackTransaction);

        var manager = Datadog.Trace.Tracer.Instance.TracerManager.DataStreamsManager;

        if (span is IDuckType { Instance: Span s })
        {
            s.TrackTransaction(manager, transactionId, checkpointName);
        }
        else if (span is Span autoSpan)
        {
            autoSpan.TrackTransaction(manager, transactionId, checkpointName);
        }
        else
        {
            manager.TrackTransaction(transactionId, checkpointName);
        }

        return CallTargetState.GetDefault();
    }
}
