// <copyright file="XUnitTestOutputHelperQueueTestOutputIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Text;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.PlatformHelpers;

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
            if (!XUnitIntegration.IsEnabled)
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;
            if (tracer.TracerManager.DirectLogSubmission.Settings.MinimumLevel < DirectSubmissionLogLevel.Information)
            {
                return CallTargetState.GetDefault();
            }

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
                using var writer = LogFormatter.GetJsonWriter(sb);

                // Based on JsonFormatter
                writer.WriteStartObject();

                writer.WritePropertyName("ddsource", escape: false);
                writer.WriteValue("xunit");

                if (HostMetadata.Instance.Hostname is { } hostname)
                {
                    writer.WritePropertyName("hostname", escape: false);
                    writer.WriteValue(hostname);
                }

                writer.WritePropertyName("timestamp", escape: false);
                writer.WriteValue(DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() / 1_000_000);

                writer.WritePropertyName("message", escape: false);
                writer.WriteValue(_message);

                writer.WritePropertyName("status", escape: false);
                writer.WriteValue("info");

                string env = string.Empty;
                if (_span is { } span)
                {
                    env = span.GetTag(Tags.Env) ?? string.Empty;

                    writer.WritePropertyName("service", escape: false);
                    writer.WriteValue(span.ServiceName);

                    writer.WritePropertyName("dd.trace_id", escape: false);
                    writer.WriteValue($"{span.TraceId}");

                    writer.WritePropertyName("dd.span_id", escape: false);
                    writer.WriteValue($"{span.SpanId}");

                    if (span.GetTag(TestTags.Suite) is { } suite)
                    {
                        writer.WritePropertyName(TestTags.Suite, escape: false);
                        writer.WriteValue(suite);
                    }

                    if (span.GetTag(TestTags.Name) is { } name)
                    {
                        writer.WritePropertyName(TestTags.Name, escape: false);
                        writer.WriteValue(name);
                    }

                    if (span.GetTag(TestTags.Bundle) is { } bundle)
                    {
                        writer.WritePropertyName(TestTags.Bundle, escape: false);
                        writer.WriteValue(bundle);
                    }
                }

                // spaces are not allowed inside ddtags
                env = env.Replace(" ", string.Empty);
                writer.WritePropertyName("ddtags", escape: false);
                writer.WriteValue($"env:{env},datadog.product:citest");

                writer.WriteEndObject();
            }
        }
    }
}
