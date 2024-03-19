// <copyright file="XUnitTestOutputHelperQueueTestOutputIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Logging.DirectSubmission;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Xunit.Sdk.TestOutputHelper.QueueTestOutput calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyNames = ["xunit.execution.dotnet", "xunit.execution.desktop"],
    TypeName = "Xunit.Sdk.TestOutputHelper",
    MethodName = "QueueTestOutput",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.String],
    MinimumVersion = "2.2.0",
    MaximumVersion = "2.*.*",
    IntegrationName = XUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class XUnitTestOutputHelperQueueTestOutputIntegration
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="output">Output string</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string output)
    {
        if (!XUnitIntegration.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        var tracer = Tracer.Instance;
        if (tracer.TracerManager.DirectLogSubmission.Settings.MinimumLevel < DirectSubmissionLogLevel.Information)
        {
            return CallTargetState.GetDefault();
        }

        if (Test.Current?.GetInternalSpan() is { } span)
        {
            TelemetryFactory.Metrics.RecordCountDirectLogLogs(MetricTags.IntegrationName.XUnit);
            tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new CIVisibilityLogEvent("xunit", "info", output, span));
        }

        return CallTargetState.GetDefault();
    }
}
