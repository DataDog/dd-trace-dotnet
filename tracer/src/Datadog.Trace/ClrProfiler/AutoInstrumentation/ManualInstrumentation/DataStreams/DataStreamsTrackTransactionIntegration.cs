// <copyright file="DataStreamsTrackTransactionIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.DataStreams;

/// <summary>
/// Instrumentation for <c>Datadog.Trace.DataStreams.TrackTransaction</c>
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.DataStreams",
    MethodName = "TrackTransaction",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Datadog.Trace.ISpan", "System.String", "System.String" },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DataStreamsTrackTransactionIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpan>(TSpan span, string transactionId, string checkpointName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.DataStreams_TrackTransaction);
        Invoke(Datadog.Trace.Tracer.Instance, span, transactionId, checkpointName);
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Separated from <see cref="OnMethodBegin{TTarget,TSpan}"/> so tests can call it directly
    /// without going through the CallTarget machinery.
    /// </summary>
    /// <typeparam name="TSpan">
    /// Typically a duck-typed <c>Datadog.Trace.ISpan</c> proxy from <c>Datadog.Trace.Manual</c>,
    /// a real <see cref="Span"/>, or null (bad usage — transaction is still tracked, tag is skipped).
    /// </typeparam>
    internal static void Invoke<TSpan>(Datadog.Trace.Tracer tracer, TSpan span, string transactionId, string checkpointName)
    {
        var dsm = tracer.TracerManager.DataStreamsManager;
        if (dsm is null || !dsm.IsEnabled)
        {
            return;
        }

        if (span is IDuckType { Instance: Span s })
        {
            s.SetTag("dsm.transaction.id", transactionId);
        }
        else if (span is Span autoSpan)
        {
            autoSpan.SetTag("dsm.transaction.id", transactionId);
        }
        else if (span is null)
        {
            // bad usage, but catering to it just in case
        }
        else
        {
            span.DuckCast<ISpanSetTagProxy>()!.SetTag("dsm.transaction.id", transactionId);
        }

        dsm.TrackTransaction(transactionId, checkpointName);
    }
}
