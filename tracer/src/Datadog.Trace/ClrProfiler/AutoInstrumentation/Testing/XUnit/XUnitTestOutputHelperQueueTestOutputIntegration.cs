// <copyright file="XUnitTestOutputHelperQueueTestOutputIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestOutputHelper.QueueTestOutput calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestOutputHelper",
        MethodName = "QueueTestOutput",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String },
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
            var tracer = Tracer.Instance;
            var span = tracer.ActiveScope?.Span as Span;
            tracer.TracerManager.DirectLogSubmission.Sink.EnqueueLog(new XUnitLogEvent(output, span));
            return CallTargetState.GetDefault();
        }

        private class XUnitLogEvent : DatadogLogEvent
        {
            private readonly string _message;
            private readonly Span _span;

            public XUnitLogEvent(string message, Span span)
            {
                _message = message;
                _span = span;
            }

            public override void Format(StringBuilder sb, LogFormatter formatter)
            {
                formatter.FormatLog<Span>(
                    sb,
                    _span,
                    DateTime.UtcNow,
                    _message,
                    eventId: null,
                    DirectSubmissionLogLevelExtensions.Information,
                    null,
                    (JsonTextWriter writer, in Span state) =>
                    {
                        if (state is not null)
                        {
                            writer.WritePropertyName("dd.trace_id");
                            writer.WriteValue($"{state.TraceId}");
                            writer.WritePropertyName("dd.span_id");
                            writer.WriteValue($"{state.SpanId}");
                        }

                        return default;
                    });
            }
        }
    }
}
