// <copyright file="OtlpMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Drawing;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.VendoredMicrosoftCode.System;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.OpenTelemetry.Traces;

internal static class OtlpMapper
{
    public static void WriteDatadogResourceAttributes(JsonTextWriter writer, in TraceChunkModel traceChunk)
    {
        if (traceChunk.DefaultServiceName is string service)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("service.name", service));
        }

        if (traceChunk.ServiceVersion is string version)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("service.version", version));
        }

        if (traceChunk.Environment is string environment)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("deployment.environment.name", environment));
        }

        if (traceChunk.GitCommitSha is string gitCommitSha)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("git.commit.sha", gitCommitSha));
        }

        if (traceChunk.GitRepositoryUrl is string gitRepositoryUrl)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue("git.repository_url", gitRepositoryUrl));
        }

        OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Language, TracerConstants.Language));

        var testOptimization = Ci.TestOptimization.Instance;
        if (!testOptimization.IsRunning || !testOptimization.Settings.Agentless)
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
        }
    }

    public static void WriteDatadogSpanAttributes(JsonTextWriter writer, in SpanModel spanModel)
    {
        if (!string.IsNullOrEmpty(spanModel.Span.Context.LastParentId))
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.LastParentId, spanModel.Span.Context.LastParentId));
        }

        // add "_dd.origin" tag to all spans
        if (!string.IsNullOrEmpty(spanModel.TraceChunk.Origin))
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Origin, spanModel.TraceChunk.Origin));
        }

        // TODO: Only write these as resource attributes
        // add "runtime-id" tag to service-entry (aka top-level) spans
        var testOptimization = Ci.TestOptimization.Instance;
        if (spanModel.Span.IsTopLevel && (!testOptimization.IsRunning || !testOptimization.Settings.Agentless))
        {
            OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.RuntimeId, Tracer.RuntimeId));
        }

        // add "env" to all spans
        OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Env, spanModel.TraceChunk.Environment));
        // add "language=dotnet" tag to all spans
        OtlpTracesSerializer.WriteKeyValue(writer, new KeyValue(Trace.Tags.Language, TracerConstants.Language));
        // add "version" tags to all spans whose service name is the default service name
        // add _dd.base_service tag to spans where the service name has been overrideen
        // Process tags will be sent only once per buffer/payload (one payload can contain many chunks from different traces)
        // SCI tags will be sent only once per trace
        // if (Security.Instance.AppsecEnabled && model.IsLocalRoot && span.Context.TraceContext?.WafExecuted is true)
        // AAS tags need to be set on any span for the backend to properly handle the billing.
    }
}
